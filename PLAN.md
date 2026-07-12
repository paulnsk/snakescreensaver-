# PLAN.md — SnakeScreensaver: текущее состояние и баги

## Что это

WPF-приложение (.NET 10), сидящее в трее. Змейка-скринсейвер играет сама в себя
поверх прозрачного окна на весь рабочий стол. Никакого виндового screensaver-API —
чисто tray-app с авто-запуском-змейки по idle-таймеру.

## Структура проекта

| Файл | Роль |
|---|---|
| `App.xaml` | стили (TrayBtn, TrayExpander, TrayRadio, Settings*, китч-градиенты) |
| `App.xaml.cs` | создаёт TrayHost, ShutdownMode=OnExplicitShutdown |
| `MainWindow.xaml(.cs)` | полноэкранное прозрачное окно со змейкой, выход по Esc (или mouse-move) |
| `Snake/SnakeGame.xaml(.cs)` | игровой движок: BFS-ИИ, Balls/Ribbon, радужный перелив, еда, частицы |
| `TrayHost.cs` | NotifyIcon (WinForms в WPF-процессе), idle-монитор, запуск/скрытие MainWindow, открытие меню/настроек |
| `TrayMenuWindow.xaml(.cs)` | китчевый popup-меню (Expander'ы, кнопки, радужные радио-батоны) |
| `SettingsWindow.xaml(.cs)` | китчевое окно настроек со слайдером/чекбоксами |
| `Settings.cs` | статический конфиг: Mode, Rainbow, Exit, IdleMinutes, RunAtStartup → `%AppData%\SnakeScreensaver\settings.json` и HKCU Run key |
| `IdleMonitor.cs` | GetLastInputInfo → секунды бездействия |
| `Usings.cs` | глобальные алиасы (WPF vs WinForms/Drawing — Point, Color, Application, MouseEventArgs, KeyEventArgs, UserControl, Cursors, RadioButton) |
| `RadioCheckedToColorBrush.cs` | IMultiValueConverter для радио-батона (заливка кружка при IsChecked) |
| `ПУБЛИШ.bat` | dotnet publish -p:PublishMode=singlefile (пользовательский, не удалять) |
| `SnakeScreensaver.csproj` | PublishMode=singlefile → self-contained single-file (~72МБ со встроенным .NET 10 + WPF + WinForms) |

## Git log

```
21cb0b2 Fix tray menu positioning: measure desired size before Show(), anchor above-left of cursor, adapt to taskbar via WorkArea
3a32c10 Drop screensaver/desktop-switch; tray-icon app (NotifyIcon), kitsch popup menu + settings window, idle-launch timer, auto-startup in HKCU Run, publish singlefile (~72MB)
0fcfa6b Spawn clean STA worker thread for SetThreadDesktop (ERROR_BUSY on main thread); remove StartupUri
b9287cc Move desktop switch to App.OnStartup (pre-window), shared DesktopHelper
c0f6eed SetThreadDesktop hack: switch /s thread from Screen-saver to Default desktop before window creation
85da0e6 Diag: log thread desktop name + taskbar/progman hwnd on /s to identify why black backdrop appears on auto activation
8396dde Disable Windows lock-screen (NoLockScreen=1) to fix black backdrop on auto screensaver activation
0b6e62b Single-install bat (auto-elevate, self-contained publish, .scr to System32); /p quits silently; /s cursor hidden, 15px threshold, 1.5s warmup; log to %TEMP%
c1206bb register-screensaver.bat: --system32 admin mode auto-elevates + copies .scr to System32 (appears in Control Panel dropdown)
102ecb7 Fix: Ribbon static colors via _hueOffset (refresh on eat/respawn); bat path resolution; .scr copy on register
9201afe Perf: rainbow toggle + perf flags; /s screensaver mode (mouse-move exit); register/unregister .bat
49963d7 Remove haze blobs (big non-clickable patches), add Ribbon render mode (continuous path), context menu on snake, settings.json in %AppData%
f8e1dd4 Smooth motion: linear interpolation (constant velocity, no easeInOut 'stomp')
aaf2fac First working version: rainbow self-playing snake screensaver (.NET10 WPF)
```

## Баги для починки (приоритет: высокий)

### Баг 1: Меню накрывает таскбар

**Симптом:** Popup-меню появляется рядом с курсором в трее, но нижняя часть заходит на таскбар (или уходит за нижний край).

**Причина:** Курсор находится ВНУТРИ таскбара (трей-иконка там). `cursor.Y` > `WorkArea.Bottom`. Формула `sy = cursor.Y - size.Height - 6` отсчитывает от cursor.Y, а не от `WorkArea.Bottom`. Если measured-размер неточный (too small), `sy` оказывается слишком низко.

**Фикс (`TrayHost.cs`, `ComputeMenuLocation`):**
- Заякорить не на `cursor.Y`, а на `Math.Min(cursor.Y, (int)wa.Bottom - 2)`.
- Затем `sy = anchorY - size.Height - 6`.
- Если `sy < wa.Top + 4` → меню выше не влезает → открыть ВНИЗ от якоря: `sy = anchorY + 4`.
- Финальный кламп по `WorkArea` (уже исключает таскбар — учитывает любой край).
- `Topmost=True` — ОК, не трогать (все tray-меню topmost).

### Баг 2: Скруглённые рамки кнопок подрезают текст

**Симптом:** Круглые бордеры `TrayBtn` и `TrayRadio` слегка грызут текст по краям.

**Фикс (`App.xaml`, стили `TrayBtn` и `TrayRadio`):**
- `TrayBtn`: `CornerRadius` 18→10, `Padding` `20,12`→`24,14`.
- `TrayRadio`: `CornerRadius` 14→8, `Padding` `14,6`→`18,10`.
- `TrayExpander`: `CornerRadius` 16→10, `Padding` `14,6`→`18,10` (по ACK стиля `TrayExpander`).

### Баг 3: Раскрытие Expander'a меняет размер окна, оно уползает за край

**Симптом:** Раскрыл `RenderExp`/`ExitExp`/`IdleExp` → высота растёт → нижний край за пределами экрана, позиция не корректируется.

**Фикс (`TrayMenuWindow.xaml.cs`):**
- Подписаться на `Expanded` и `Collapsed` у всех трёх Expander'ов.
- В обработчике — через `Dispatcher.BeginInvoke(DispatcherPriority.Background)` вызвать `Reposition()`:
  ```csharp
  void Reposition() {
      var loc = TrayHost.ComputeMenuLocation(this); // метод сделать internal static
      Left = loc.X; Top = loc.Y;
  }
  ```
- Придётся либо сделать `ComputeMenuLocation` internal/static (он уже private static в TrayHost — поменять на internal static), либо продублировать логику.

### Баг 4: Кнопка Settings роняет приложение

**Симптом:** Нажал Settings → приложение закрылось.

**Причина:** `Settings_Click` (TrayMenuWindow.xaml.cs:100-104):
```csharp
private void Settings_Click(object sender, RoutedEventArgs e)
{
    Close();                  // ← начинает закрывать menu window
    _host.OpenSettings();     // ← пытается показать SettingsWindow во время Close
}
```
`Close()` запускает асинхронный процесс закрытия: сначала `Deactivated` → `Window_Deactivated` → `Close()` (повторный вызов, re-entrant), затем фокус переходит к новому окну. В момент `OpenSettings()` `SettingsWindow` создаётся и `.Show()` вызывается, но menu-window всё ещё закрывается и его `Deactivated` фаерит повторно. `SettingsWindow.Show()` может бросить из-за перекрытого shutdown-цикла (возможно InvalidOperationException: "Cannot set Visibility or call Show... after window closed").

**Фикс (`TrayMenuWindow.xaml.cs`, `Settings_Click`):**
- Не закрывать меню синхронно. Вместо этого:
  ```csharp
  private void Settings_Click(object sender, RoutedEventArgs e)
  {
      _host.OpenSettings();    // сначала открываем настройки
      Close();                  // потом закрываем меню
  }
  ```
- ИЛИ через `Dispatcher.BeginInvoke` отложить `OpenSettings` на следующий frame:
  ```csharp
  Close();
  Dispatcher.BeginInvoke(new Action(() => _host.OpenSettings()));
  ```
- первый вариант надёжнее. Но! `OpenSettings` сам может вызвать `Deactivated` на меню → `Close()` → фолс-старт. Проверить и при необходимости временно отписаться от `Deactivated`:
  ```csharp
  private void Settings_Click(object sender, RoutedEventArgs e)
  {
      Deactivated -= Window_Deactivated;   // не закрываем по деактивации
      _host.OpenSettings();
      Close();
  }
  ```

## Сборка и запуск

```bat
:: Запуск (debug):
dotnet run --project D:\d\SnakeScreensaver\SnakeScreensaver.csproj

:: Publish single-file (пользовательский ПУБЛИШ.bat):
dotnet publish D:\d\SnakeScreensaver\SnakeScreensaver.csproj -c Release -p:PublishMode=singlefile -o D:\d\SnakeScreensaver\publish

:: Запуск published:
D:\d\SnakeScreensaver\publish\SnakeScreensaver.exe
```

## Текущее состояние (после фиксов багов 1-4 + Quit + позиционирования)

- Экранзейвер / desktop-switch / NoLockScreen / reg-батники — УДАЛЕНЫ (коммит 3a32c10).
- `Settings.RunAtStartup` пишет в `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run` из кода (Settings.cs), больше не нужен батник.
- WPF+WPF / WinForms-Drawing конфликты типов — закрыты через `Usings.cs` global-алиасами. требует внимания при добавлении новых WinForms-вызовов.
- NotifyIcon+WPF совместимы в одном процессе (WinForms NotifyIcon pump'ит через WPF Dispatcher).
- Корректность PublishMode.csproj свойств: PublishMode=singlefile → SelfContained+ PublishingSingleFile + Compression + Include NativeLibraries + embedded debug.
- Стили в App.xaml взаимозависимы (TrayBtn Базовый для TrayBtnQuit и SettingsOk/Cancel; SettingsCheckbox/Slider/Label независимы; TrayExpander standalone).
- **TrayMenuWindow positioning переписан на якоря** (`_anchorRight`/`_anchorBottom`):
  - `TrayHost.ShowMenu` вызывает `InitAnchor(right, bottom, size)` один раз;
    `bottom = wa.Bottom` (не `min(cursor.Y+8, wa.Bottom)` и не `cursor`-зависимый) — это фикс
    изначального бага, когда окно открывалось у физического края, а не у таскбара.
  - `ComputeMenuLocation` удалён; вместо него `TrayMenuWindow.ComputeRestLocation` — единый
    путь для initial Show и для Reposition (на Expander.Expanded/Collapsed).
  - `Left = _anchorRight - ActualWidth`, `Top = _anchorBottom - ActualHeight`, кламп по `wa`.
- **Bug 4 / Quit краш — починены** (см. TrayMenuWindow `Settings_Click`/`Quit_Click` и
  `TrayHost.Quit`): отписка от Deactivated, `OpenSettings` до `Close`,
  `_menu.Hide()` вместо `_menu.Close()`, `BeginInvoke(Shutdown)` вместо `Invoke`.
- **Стили (App.xaml)** после правок: TrayBtn CornerRadius=10 Padding=24,14;
  TrayRadio CornerRadius=8 Padding=18,10; TrayExpander CornerRadius=10 Padding=34,14
  (увеличено ~6px чтобы заголовок Expander'a получал боковое пространство сопоставимое с
  внутренними RadioButton'ами); arrow ▼ FontSize=16 Margin=10,0,14,0.
- **Перф Balls (промежуточный фикс):** `UpdateBallsColors(0)` убран из покадрового OnRender
  в non-rainbow режиме — вызывается только при Starе()/EatFoodShared/respawn;
  BlurRadius DropShadowEffect у сегментов 26→16 и еды 28→18 (cost ~ radius²).
- **Лог-инфраструктура:** `Log.cs` + хуки в `App.xaml.cs` (AppDomain/Dispatcher/TaskScheduler).
  Лог в `%TEMP%\SnakeScreensaver.log`. Трейсы в TrayHost (ShowMenu/Quit) и TrayMenuWindow
  (Expander_StateChanged/Reposition). `Qu` и `Setting`s клики логируются.
- **SkiaSharp миграция — планируется**, детали в AGENTS.md (раздел "План миграции").

## Open issues / next steps

- Не до конца подтверждён initial-позиционирование ShowMenu (нужен живой тест — открыть меню,
  раскрыть Expander — убедиться что якорь не дрейфит и низ на таскбаре).
- Padding экспандера был пофиксен пользователем: `TextBlock Padding="20"` внутри стиля TrayExpander
  (а не Padding на уровне Expander, как я пытался). Запомнить конвенцию: текст-контент экспандера
  требует собственного Padding, не	level-уровневого.
- Перф змейки при 50+ сегментах — временно смягчён. Финальное решение: **переключатель
  Settings.Renderer { Wpf, Skia }**, реализовать Skia-режим (SkiaSharp.Views.WPF, SKGLEView),
  НЕ ломая существующий WPF. Подробно — в AGENTS.md. Перед миграцией — снять baseline fps.
