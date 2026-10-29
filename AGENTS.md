# AGENTS.md — SnakeScreensaver

## Контекст для следующего агента

WPF-приложение (.NET 10), сидит в трее (WinForms NotifyIcon в WPF-процессе). По idle-таймеру
запускает змейку-скринсейвер на полноэкранном прозрачном окне. Никакого виндового
screensaver-API — чисто tray-app.

## Сборка и запуск

```bat
dotnet build -c Debug --nologo
dotnet run --project D:\d\SnakeScreensaver\SnakeScreensaver.csproj
:: либо запустить bin\Debug\net10.0-windows\SnakeScreensaver.exe

:: Publish single-file (он же ПУБЛИШ.bat):
dotnet publish -c Release -p:PublishMode=singlefile -o D:\d\SnakeScreensaver\publish
```

.NET 10.0 (TargetFramework=net10.0-windows, UseWPF=true, UseWindowsForms=true).
Build должен быть **0 warning / 0 error** — следи за nullable.

## Логи и диагностика

- **Лог:** `%TEMP%\SnakeScreensaver.log`. В него пишут AppDomain.UnhandledException,
  DispatcherUnhandledException, TaskScheduler.UnobservedTaskException, а также трейсы
  из TrayHost / TrayMenuWindow.
- **После каждого ручного теста приложения** — читай лог, ищи исключения.
- Для свежего лога: удали `SnakeScreensaver.log` перед запуском, и/или
  `Get-Process SnakeScreensaver | Stop-Process -Force`.

## Архитектура

```
App.xaml(.cs)             — OnStartup: TrayHost.Start, ShutdownMode=OnExplicitShutdown.
                            Здесь же глобальные обработчики необработанных исключений → лог.
TrayHost.cs               — NotifyIcon (WinForms), idle-поток, открытие меню/настроек/змейки, Quit.
TrayMenuWindow(.xaml|.cs) — китчевое popup-меню.
                            Якоря (_anchorRight/_anchorBottom) фиксируются ОДИН РАЗ в InitAnchor
                            (вызывается из TrayHost.ShowMenu сразу после MeasureWindow).
                            Дальше ComputeRestLocation/Reposition считают Left/Top из якорей
                            и ActualWidth/ActualHeight — один и тот же путь для показа и для
                            раскрытия Expander'ов. Подписки на Expander.Expanded/Collapsed →
                            Reposition через DispatcherPriority.Background.
SettingsWindow(.xaml|.cs) — китчевое окно настроек. Показывается корректно. Чекбоксы/слайдер
                            пишут в Settings.cs, Apply → Settings.Save(). Cancel → Settings.Load().
Settings.cs               — статичный конфиг → %AppData%\SnakeScreensaver\settings.json + HKCU Run.
IdleMonitor.cs            — GetLastInputInfo.
MainWindow(.xaml|.cs)     — полноэкранное прозрачное окно, хостит SnakeGame,
                            выход по Esc или mouse-move.
Snake/SnakeGame(.xaml|.cs) — ИЗОЛИРОВАННЫЙ игровой движок + WPF-рендер в одном UserControl.
                            Хостится через `<snake:SnakeGame x:Name="Game"/>` в MainWindow.xaml.
Usings.cs                 — глобальные алиасы (Point/Color/etc. из WPF, не WinForms).
Log.cs                    — потокобезопасный логгер (File.AppendAllText + lock).
RadioCheckedToColorBrush.cs — IMultiValueConverter для заливки кружка радио-батона.
App.xaml                  — стили (TrayBtn, TrayBtnQuit, TrayExpander, TrayRadio, Settings*).
```

## Текущее состояние багов

- ✅ **Пакмановская голова 2026-10-29:** Turbo Balls/Ribbon: голова в 1,7 раза шире
  шариков тела, постоянные челюсти ~3,1 Гц с резким закрытием, клыки и смешной злой
  прищур без румянца. Перед едой пасть раскрывается, при глотке голова раздувается
  по Growth, фигурка втягивается за 460 мс и скрывается контуром челюстей; затем
  прежняя волна проходит по телу. Поза вычисляется один раз на кадр для всех мониторов.
  UI/конфиг не менялись, звука нет. Debug/Release: 0 warnings/errors. Временный WPF
  стенд вне репозитория: по 6 с Balls/Ribbon с реальным игровым циклом и глотками,
  без исключений; контрольные кадры просмотрены. Пользователь подтвердил внешний вид.
  FPS в этом стенде не является замером производительности приложения.

- ✅ **Живая змейка 2026-10-14:** Turbo Balls/Ribbon получили пасть,
  следящий глаз, втягивание еды и затухающие волны утолщения после глотка.
  Settings.LivingSnake (по умолчанию true) + HUNGRY SNAKE в SettingsWindow;
  изменение UI прямо разрешено пользователем для переключателя. Замеры с тенью:
  100+ Balls ~54,8 FPS, лента 3000+ px ~40,5 FPS; 12-секундные тесты без исключений.
  Debug/Release: 0 warnings/errors. Чекбокс on/off, доступность только для Turbo и
  Cancel проверены; сохранённый конфиг не менялся. Выключенный эффект возвращает
  классический рисунок (лента ~37,6 FPS, активных глотков 0). Настоящий Release exe
  smoke 8 с: свежий лог без исключений. Контрольные кадры обоих режимов просмотрены;
  глаз/бровь/язык/щурение, цветные крошки, блик ленты. Esc закрывает все мониторы.
- ✅ **Turbo perf 2026-10-14:** Release, 5280×1080, три монитора, rainbow + цветная тень:
  было ~10 FPS; стало ~35–36 FPS со 100 начальными сегментами и ~43 FPS с 300.
  Это частота уникальных Rendering событий после 2 с прогрева, а не независимый замер
  кадров, представленных DWM. Turbo выводит общую игру через отдельное owned-окно
  на каждый монитор (`MainWindowTurbo.cs`), с обрезкой/отсевом невидимых объектов.
  Шарики/ореолы кешируются frozen RenderTargetBitmap по hue, градиентная тень сохранена.
  Исправлены расписание шагов/линейная интерполяция/позиция нового сегмента при росте
  в Skia/Turbo, Rainbow в Turbo; лента использует frozen StreamGeometry.
  Esc из дополнительного окна закрывает все окна. Debug/Release: 0 warnings/errors;
  настоящий exe smoke 8 с, свежий лог без исключений. Проверены мониторы с DPI=100%;
  смешанный DPI и визуальное качество на движущейся змейке ждут проверки пользователем.
  Legacy/Skia не удалены, UI трея/меню/настроек не менялся, коммита нет.
- Инструкция `КАК ЧИНИТЬ ЭНТЕР В КОДЕКСЕ.md` сокращена до пяти шагов без истории диагностики.
- ✅ **Codex Enter 2026-10-14:** в пользовательский config.toml добавлены composer.submit
  Ctrl+Enter, composer.queue=[], editor.insert_newline Enter/Ctrl+M/Ctrl+J/Shift+Enter.
  Резервная копия создана, TOML проверен; клавиши проверить после перезапуска CLI.
  Пользователь просит сначала перезапуск, затем оптимизацию змейки: приоритет WPF Turbo
  с цветной тенью, цель хотя бы 30 FPS в одном рендере. Запуски и замеры разрешены;
  не тратить чрезмерно время/токены. Legacy WPF можно удалить позднее по необходимости,
  но сейчас игровой код не менялся. Команда возврата: `codex --no-daemon resume --last`.
- ⚠️ **Ревью плавности 2026-10-14 (без исправления игрового кода):** пользователь видит
  рывки во всех трёх рендерах; 4–6 FPS — субъективная оценка, замера нет.
  Skia/Turbo Balls используют cubic EaseOut вместо линейного движения. Общий OnRender
  сбрасывает `_lastStepTime = t`, теряя остаток интервала и добавляя паузы между шагами.
  В Skia/Turbo `t` берётся до StepBalls, а MoveStart — после: первый кадр шага может иметь
  отрицательный progress (нет нижнего clamp). Legacy WPF анимирует Canvas.Left/Top,
  меняет градиенты/тени каждого сегмента каждый rainbow-кадр. Skia дважды копирует весь
  bitmap и помечает его целиком dirty; Windows сообщает VirtualScreen 5280×1080
  (при bitmap такого размера ~43,5 МиБ копирований/кадр). Rainbow=false игнорируется
  в Skia/Turbo. BFS выполняется на UI-потоке с новыми Dictionary/Queue/HashSet;
  его вклад и общий прозрачный полноэкранный compositor требуют измерения.
  Ribbon пересоздаёт геометрию каждый кадр; Turbo без кеширования геометрии ленты.
  Debug build: 0 warnings / 0 errors. Приложение вручную не запускалось.
- ✅ Меню накрывало таскбар — якорь `bottom = wa.Bottom` (верх таскбара), нижний край меню
 (chain aligning) к верху таскбара.
- ✅ Скруглённые рамки грызли текст — Padding/CornerRadius увеличены (TrayBtn 24,14 / TrayRadio
  18,10 / TrayExpander 34,14; CornerRadius 10/8/10).
- ✅ Раскрытие Expander'a уползало за край — Reposition по якорям.
- ✅ Settings_Click / Quit_Click роняли приложение: отписка от Deactivated перед действием +
  OpenSettings до Close; Quit использует _menu.Hide() вместо Close() и
  Dispatcher.BeginInvoke(Shutdown) вместо Invoke.
- ✅ Initial позиция ShowMenu теперь идёт через тот же ComputeRestLocation, что и Reposition
  (якоря фиксируются в ShowMenu, окно показывается в off-screen -10000, затем Reposition).
- ✅ **WPF TURBO — реализован** (третий рендер, DrawingContext-based). `SnakeTurboCanvas`
  (Canvas subclass, OnRender → `Host.TurboRender`); кеши frozen `RadialGradientBrush`
  (glow + fill) + `Pen` + `Geometry` по hue-key; manual-интерполяция как в Skia;
  цветная тень как RadialGradient halo (без blur-фильтра, без DropShadowEffect).
  - `Snake/SnakeTurboCanvas.cs` (new): Canvas + 5 кешей (`_glow`, `_fill`,
    `_strokeColors`, `_pens`, `_foodGeo`); все значения `Freeze()`. OnRender
    делегирует в `Host.TurboRender`.
  - `Snake/SnakeGameTurbo.cs` (new, partial): `TurboInit/Resize/Reset/CreateSeg/
    AddFood/EatFood/Burst/Redraw/Render`. `Render` диспетчер → `TurboDrawFoods/Balls/
    Ribbon/Particles`. Manual interpolation по `Stored/To/MoveStart/MoveDuration`.
  - `SnakeGame.xaml`: добавлен `<snake:SnakeTurboCanvas x:Name="TurboCanvas"
    Visibility="Collapsed"/>` в общий Grid (xmlns:snake объявлен).
  - `SnakeGame.xaml.cs`: `_isTurbo` флаг; ветки в 12 местах (constructor/OnLoaded/
    InitFromSize/OnRender/Start/StartBalls/SyncSegs/AddSegVisual/UpdateBallsColors/
    EnsureRibbonPath/DieBalls/DieRibbon/EatFoodShared/TriggerBurst/CreateFood).
    **Legacy WPF и Skia пути не изменены** — только новый `_isTurbo` добавлен в
    параллельных ветках.
  - `Settings.Renderer` enum: `Wpf=0, Skia=1, WpfTurbo=2`.
  - `SettingsWindow.xaml/.cs`: третья радио "WPF TURBO".
  - Smoke-тесты: 0 exceptions за 8s в трёх конфигах (Turbo+Balls+Shadow, Turbo+Ribbon+
    Shadow, legacy WPF+Balls+Shadow).
  - **Визуальная проверка пользователем — ожидается** на длинной змейке.
- ✅ **Apply в Settings теперь перезапускает змейку:** `TrayHost.RestartSnakeIfVisible()`
  вызывается из `SettingsWindow.OK_Click` после `Settings.Save()`. Только если змейка уже
  видна; если нет — no-op.
- ✅ **Skia миграция — реализована** (CPU backend через WriteableBitmap, т.к.
  `AllowsTransparency=True` блокирует GPU `SKGLEView` ≠ HwndHost в прозрачном WPF-окне).
  - `Settings.Renderer` enum {Wpf, Skia} + сохранение в settings.json (ключ `Render`).
  - В SettingsWindow добавлен раздел "🖥️ RENDERER" с двумя радио (WPF/Skia),
    вверху перед RENDER MODE.
  - `Snake/SnakeGameSkia.cs` (partial class `SnakeGame`): `SKBitmap` + `WriteableBitmap`,
    per-frame `Marshal.Copy` BGRA → WPF `Image`. `SKCanvas.Clear(transparent)` →
    рисование еды/сегментов/ленты/частиц одним батчем.
  - `SnakeGame.xaml.cs` — `_isSkia` флаг; ветвление в OnRender/Start/SyncSegs/AddSegVisual/
    Die*/EatFoodShared/TriggerBurst/CreateFood/CreateSeg/EnsureRibbonPath/UpdateBallsColors.
    Skia-сегменты интерполируются вручную (Stored/To/MoveStart/MoveDuration).
  - `App.xaml.cs` — CLI `--snake` для прямого запуска (тесты/демо).
  - `SnakeGame.xaml` — обёрнут в Grid с Canvas (WPF путь) + Image (Skia путь), Visibility
    переключается в конструкторе по `_isSkia`.
  - Skia 4.150.0 + `SkiaSharp.NativeAssets.Win32`, `NoWarn=CS0618` (устаревший SKPath API).
  - По умолчанию `Renderer = Wpf` (безобидный fallback).
  - **Визуальная проверка пользователем — проведена.** Skia движется рывками (нет
    плавности у одиночных шаров), тень серая не цветная (blur с низкой alpha теряет
    saturation). WPF — плавнее, но при длинной змейке тоже рывки. **Skia НЕ подходит без
    серьёзного ускорения** — см. раздел "Skia perf/jank" в PLAN.md.

## План миграции рендера змейки на SkiaSharp — РЕАЛИЗОВАН, НО НЕ ПОДХОДИТ (без ускорения)

**Цель:** убрать дорогие WPF-визуалы (Ellipse + DropShadowEffect на каждый сегмент) и перенести
рендер на SkiaSharp CPU (`SKBitmap` → `WriteableBitmap`), сохранив всю графику (HSL/gradient/
blur/частицы/еду).

**Почему CPU, а не GPU:** MainWindow использует `AllowsTransparency=True` для прозрачного
fullscreen-оверлея поверх живого десктопа. GPU `SKGLEView` (через `HwndHost`) в таком окне не
работает — известное ограничение WPF. CPU `SKBitmap` + `WriteableBitmap` совместим с
прозрачностью и всё равно быстрее сотен WPF-визуалов (один батч-дрaw против N элементов с
DropShadowEffect).

**Что сделано:**
1. ✅ NuGet `SkiaSharp` 4.150.0 + `SkiaSharp.NativeAssets.Win32` (для single-file publish
   `IncludeNativeLibrariesForSelfExtract` уже включён в csproj).
2. ✅ `Settings.Renderer` enum { Wpf, Skia } (по умолчанию Wpf) + запись/чтение из settings.json
   (ключ `Render`) + UI в SettingsWindow (раздел "🖥️ RENDERER" вверху).
3. ✅ `Snake/SnakeGameSkia.cs` — partial class `SnakeGame` со встроенным `SKBitmap`+`WriteableBitmap`
   ребёнком (`Image x:Name="SkiaImage"` в SnakeGame.xaml). Per-frame `Marshal.Copy` BGRA.
   Рисование еды/сегментов/ленты/частиц одним `SKCanvas`.
4. ✅ В конструкторе `SnakeGame` по `_isSkia` переключается `Field.Visibility` (WPF Canvas)
   vs `SkiaImage.Visibility` (Skia Image). WPF-путь не сломан.
5. ✅ SingleFile publish — `SkiaSharp.NativeAssets.Win32` попадёт через
   `IncludeNativeLibrariesForSelfExtract` (уже в csproj).
6. ⏳ Сравнить перф (BFS + 100 сегментов) — после демо пользователя.

**Дым-тест:** `Settings.Render=1` + `SnakeScreensaver.exe --snake` → окно открылось, 0 exceptions
в логе за 8 секунд. **Визуальная проверка пользователем ещё не проведена.**

## Правила для агентов

1. **После каждого шага** — фиксируй состояние в PLAN.md (раздел "Текущее состояние") и в этом
   AGENTS.md (раздел "Текущее состояние багов"). Не оставляй наблюдений без записи.
2. **Build = 0 warning / 0 error.** Если не 0 — не закрывай шаг.
3. **Не пиши код "на будущее".** YAGNI. Комментарии не добавлять (кроме случаев когда просят).
4. **Логи:** `%TEMP%\SnakeScreensaver.log`. После ручного теста — читай.
5. **Не трогай UI (трей/меню/настройки)** при работе над змейкой — UI уже зафиксирован.
6. **Стиль кода:** смотри соседние файлы, копируй конвенции. Никакого рефакторинга без запроса.
7. **Git:** коммитить только когда явно просят. Сообщения — в стиле repo (см. git log).
8. **Settings/Config:** не дублировать — `Settings.cs` единственный источник.
9. **WinForms+WPF:** конфликты типов закрыты через `Usings.cs` global-алиасами. При добавлении
   WinForms-вызовов — проверь алиасы.
10. **Quit/Shutdown:** изнутри UI-окна — только `_menu?.Hide()` + `BeginInvoke(Shutdown)`,
    не `_menu.Close()` и не `Dispatcher.Invoke(Shutdown)`.
11. **TrayMenu positioning:** якоря фиксируются один раз в InitAnchor.
    ComputeRestLocation/Reposition — единый путь. Не вводи повторный вызов
    cursor-зависимого ComputeMenuLocation-style.
12. **CLI `--snake`** (в App.OnStartup) — режим отладки/демо для агента/тестов, не фича
    пользователя. Сохранять в репозитории, не упоминать в UI.
13. **Skia perf:** `SKBitmap` → `WriteableBitmap` per-frame `Marshal.Copy` BGRA — кандидат
    на оптимизацию. Профайлинг — после очистки контекста (см. PLAN.md "Skia perf/jank").

## Полезные ссылки

- PLAN.md — детальный план/история багов и фиксов.
- `%TEMP%\SnakeScreensaver.log` — живой лог работы приложения.
- `ПУБЛИШ.bat` — скрипт публикации single-file (пользовательский, не удалять).
