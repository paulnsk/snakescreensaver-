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
- ❓ Производительность змейки при ~50 сегментах — применены простые фиксы (BlurRadius↓).
  Финальное решение — добавить **переключатель режимов рендера** (Settings.Renderer: Wpf / Skia),
  реализовать режим Skia (SkiaSharp.Views.WPF, SKGLEView), НЕ ломая существующий WPF.
  См. AGENTS.md "План миграции".

## План миграции рендера змейки на SkiaSharp

**Цель:** убрать дорогие WPF-визуалы (Ellipse + DropShadowEffect на каждый сегмент) и перенести
рендер на SkiaSharp GPU (SKGLEView), сохранив всю графику (HSL/gradient/blur/частицы/еду).

**Принципы:**
- UI (трей, меню, настройки) — остаётся на WPF, не трогается.
- В **опции** (Settings) добавить переключатель режимов рендера змейки: **Skia** / **WPF**.
  Оба режима должны работать; существующий WPF-рендер НЕ ломать — он остаётся как fallback,
  по умолчанию можно Skia (после реализации и проверки).
- Змейка изолирована в `Snake/SnakeGame.xaml(.cs)` (UserControl, хостится в MainWindow.xaml).
  Меняется содержимое этого UserControl'а (или сам класс) на SKGLEView-реализацию для режима Skia;
  WPF-реализация остаётся в том же файле или отдельной ветке через флаг.

**План шагов:**
1. Добавить NuGet `SkiaSharp.Views.WPF` (+ native bundled через
   `IncludeNativeLibrariesForSelfExtract` в csproj для single-file publish).
2. Добавить `Settings.Renderer` enum { Wpf, Skia } (по умолчанию Wpf — пока не подтверждена
   стабильность Skia) + запись/чтение из settings.json + UI в SettingsWindow.
3. Создать `Snake/SnakeGameSkia.cs` — partial class `SnakeGame` со встроенным SKGLEView ребёнком,
   реализация `OnPaintSurface(SKPaintSurfaceEventArgs)` — рисование кружков/полосы/частиц/еды
   одним `SKCanvas`. Логику ИИ (BFS) и игровое состояние — переиспользуется из текущего кода
   (вынести в `SnakeEngine.cs`, не привязанный ни к WPF, ни к Skia).
4. В `MainWindow.xaml` (или в `SnakeGame` xaml.cs) подменять рендер по `Settings.Renderer`,
   не ломая существующий WPF-путь.
5. Проверить что SingleFile publish включает native Skia libraries
   (`IncludeNativeLibrariesForSelfExtract=true`, размер вырастет ~10 МБ).
6. Сравнить перф: BFS + 100 сегментов — должно быть в несколько раз быстрее на GPU в режиме Skia.

**ПЕРЕД началом миграции:**
- Прочитать этот файл и PLAN.md полностью.
- Прогнать текущую WPF-версию с длинной змейкой (50+ сегментов), снять baseline fps.
- Зафиксировать все правила в AGENTS.md (этот файл) — обновлять после каждого шага.
- После каждого шага миграции — build + smoke-test + запись в лог + обновить PLAN.md.

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

## Полезные ссылки

- PLAN.md — детальный план/история багов и фиксов.
- `%TEMP%\SnakeScreensaver.log` — живой лог работы приложения.
- `ПУБЛИШ.bat` — скрипт публикации single-file (пользовательский, не удалять).
