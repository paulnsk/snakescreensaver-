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
