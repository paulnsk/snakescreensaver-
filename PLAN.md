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

- **Магический сфинктер и Check.exe 2026-10-29:** пользователь согласовал портал
  и отправку результата в GitHub вместе с Magic Poop. В SnakeGamePoop добавлена
  общая поза выхода: _tailOutlet, направление и раскрытие рассчитываются в
  TurboUpdatePoop даже без ожидающей еды, чтобы закрытая розетка оставалась видимой.
  Длина Ribbon обходится только при непустой очереди. Перед достижением хвоста
  волной кольцо раскрывается, при выпуске открыто полностью, затем сжимается
  за 0,6 с. Кучка использует тот же _tailOutlet; цвет портала совпадает с палитрой
  переработанной еды. TurboDrawTailPortal рисует после тела, учитывает viewport,
  MAGIC POOP и DropShadow; рендер не изменяет игровое состояние.
  Временный стенд проверил общую точку выхода, раскрытие, прежние тайминги/однократность,
  on/off и UI. Контрольные кадры обоих режимов в фазах idle/ready/release/closed просмотрены.
  Игровой цикл без окон: Balls 8 с, Ribbon 12 с (пики 1/5 кучек), без исключений.
  Debug/Release: 0 warnings/errors; временное предупреждение NU1900 ушло после
  принудительного restore с доступом к сети. Лог прочитан, стенд его не изменял.
  Найден источник жалобы на Check.exe: временные стенды в TEMP раньше оставляли
  ошибки отражения и assertions необработанными. Текущих процессов при проверке нет.
  Оба стенда получили try/catch с check-error.log и exit 1, SetErrorMode(2), пересобраны;
  контролируемый сбой проверен. Стенды не входят в репозиторий, конфиг пользователя
  не менялся. Полноэкранная оценка портала пользователем ожидается.

- **Magic Poop 2026-10-29 — реализовано:** `Snake/SnakeGamePoop.cs` хранит очередь
  перевариваемой еды независимо от визуальных Meal (их лимит 12 и затухание 4 с сохранены).
  TurboEatFood ставит одну запись на фигурку; TurboRedraw обновляет очередь и выбросы
  единожды для всех мониторов. TurboReset очищает эффект при новой игре.
  Общая TurboDigestionDistance совпадает с прежним центром утолщения первые 4 с;
  после затухания продолжает путь на 178,5 px/s, чтобы догонять растущий хвост.
  Длина Balls = (число сегментов − 1) × Cell; Ribbon = длина текущей полилинии.
  При достижении хвоста запись удаляется, появляется одна кучка в текущей позиции
  хвоста с направлением выхода по последним сегментам. Подскок/лёгкое покачивание,
  три цветных яруса с завитком, блики и мерцающие звёздочки, полное исчезновение
  за 3,8 с. Геометрии frozen; кисти/перья из кешей Turbo, отсев по viewport.
  Settings.MagicPoop по умолчанию true; загрузка старого конфига тоже включает эффект.
  Пользователь явно разрешил отдельный чекбокс MAGIC POOP в SettingsWindow;
  он доступен для Turbo независимо от HUNGRY SNAKE, сохранение/Cancel через Settings.
  Debug/Release: 0 warnings/errors. Временный стенд в TEMP проверил тайминг 90/600 px
  в Balls/Ribbon при LivingSnake on/off, однократность выброса, отсутствие генерации
  без еды, очистку при отключении/рестарте и исчезновение; контрольные кадры просмотрены.
  Реальный игровой цикл без окон: Balls 8 с (пик 1 кучка), Ribbon 12 с (пик 4),
  без исключений. UI on/off/доступность/Cancel проверены без записи конфига.
  Лог приложения прочитан, новых записей от стенда нет. Полноэкранная оценка ожидается.

- **Пакмановская голова 2026-10-29:** по согласованному запросу изменён только
  `Snake/SnakeGameFeeding.cs` для Turbo Balls/Ribbon. Радиус головы 0,78 Cell
  вместо 0,57 (ширина в 1,7 раза больше шариков тела); челюсти постоянно работают
  на 3,1 Гц, раскрываются до 52° с каждой стороны, резко смыкаются с короткой
  закрытой фазой. Прищур, наклонная бровь, два клыка, ухмылка; румянец убран.
  Перед едой открывается пасть; при глотке голова раздувается по Growth, фигурка
  сохраняет размер большую часть 460 мс и втягивается за контур челюстей.
  Закрытие происходит в конце глотка; раздувание плавно уходит в прежнюю волну тела.
  Общая поза вычисляется в TurboUpdateFeeding один раз для всех viewport.
  UI, сохранённые настройки и остальные рендереры не менялись; звука нет.
  Debug/Release: 0 warnings/errors. Временный стенд в TEMP отрисовал контрольные
  кадры и выполнил по 6 с реального игрового цикла Balls/Ribbon с поеданием еды,
  без исключений. Лог приложения прочитан, новых записей от стенда нет.
  Стенд не измеряет FPS приложения; пользователь подтвердил внешний вид и разрешил
  коммит с отправкой в GitHub.

- **Живая змейка 2026-10-14 — реализована и проверена:** новый `Snake/SnakeGameFeeding.cs`
  реализует живую голову Turbo, глоток 220 мс, до 12 затухающих пищевых волн на 4 с,
  плавный поворот, глаз/бровь/язык/щурение, небольшие цветные крошки. Balls масштабируют
  прежние кешированные sprites; Ribbon получает контур переменной толщины и блик.
  `Settings.LivingSnake=true` сохраняется в settings.json; HUNGRY SNAKE выключает
  весь новый эффект, доступен в UI при Turbo. Пользователь прямо разрешил переключатель.
  Контрольные кадры обоих режимов отрисованы/просмотрены. Release замеры 2+10 с на
  5280×1080 / 3 мониторах, rainbow+тень: Balls 100→111 сегментов 54,80 FPS (5 активных
  глотков в конце), Ribbon 3000→3270 px 40,50 FPS (1 глоток). Все измеренные кадры
  с живой длинной змейкой. Esc с другого монитора закрывает все окна. Debug/Release
  builds: 0 warnings/errors. Чекбокс on/off, доступность только при Turbo и Cancel
  прошли отдельный тест с настоящими SettingsWindow/стилями. Сохранённый конфиг
  не изменялся. Отключённая живая лента: 37,62 FPS, 3000→3240 px, активных глотков 0;
  возвращается прежняя геометрия/еда/вспышки. Настоящий Release exe smoke 8 с:
  новый участок лога 20:36:49–20:36:50 без исключений. `git diff --check` чистый.
  Новых NuGet/bitmap-ассетов нет: морда и лента рисуются WPF, тело использует прежние
  кешированные sprites. Углы пасти/взгляд/поворот общие для всех viewport; пищевые
  волны очищаются при respawn/отключении. UI изменён только для разрешённого чекбокса.
  Старые WPF/Skia не получили эту анимацию, коммита нет.
- **Turbo оптимизирован 2026-10-14:** внешний стенд в `%TEMP%\snake-perf-20261014`
  использует настоящее MainWindow/SnakeGame, 100 начальных сегментов, rainbow и цветную
  тень; 2 с прогрев + 10 с измерения уникальных CompositionTarget.Rendering событий.
  Исходный fullscreen 5280×1080: 9,94 FPS, p95 кадра 214 мс, p95 логики 0,36 мс.
  После исправления времени: 9,50 FPS. Одно окно 1920×1080 с тенью: 29,33 FPS;
  fullscreen без тени: 10,01 FPS. Это указывает на вывод большого прозрачного окна,
  а не BFS/тень как главное узкое место в этом тесте. Основной эффект дало разделение
  Turbo на окна по мониторам с общей игровой логикой. Стенд не сохраняет настройки.
  Реализовано в `MainWindowTurbo.cs` только для Turbo: основное окно на главном
  мониторе содержит Game с прежним полным размером мира; дополнительные owned-окна
  содержат SnakeTurboCanvas, рисующие тот же Game со смещением/обрезкой по монитору.
  Невидимые шарики, еда и частицы отсекаются; отдельные hue-кеши frozen bitmap
  для шариков и градиентных ореолов убирают повторную растеризацию их градиентов.
  После разделения окон: 28,09 FPS; после bitmap-кеша: 36,43 FPS / 100 сегментов,
  43,50 FPS / 300 сегментов. Финальный повтор после исправления старта нового
  сегмента при росте: 35,05 FPS / 100 сегментов, p95 кадра 43,34 мс.
  Лента с тенью прошла 12-секундный smoke (~37,66 FPS; отдельная гарантия длительного
  удержания заданной длины не проверялась). Esc из дополнительного окна закрывает
  все три окна — проверено для Balls/Ribbon. Все мониторы в тесте DPI=100%; смешанный
  DPI не проверен. Rendering события измеряют каденс WPF, не аппаратный present DWM.
  Skia/Turbo получили расписание шагов с сохранением остатка времени (догон ограничен
  250 мс), линейную интерполяцию с clamp и правильную стартовую позицию нового
  сегмента при росте. Turbo учитывает Rainbow=false; отрицательный возраст частиц/
  pop ограничен нулём. Turbo Ribbon: frozen StreamGeometry/brush/pen.
  Debug/Release builds: 0 warnings/errors; `git diff --check` чистый. Запуск настоящего
  Debug exe 8 с: новый участок `%TEMP%\SnakeScreensaver.log` без исключений.
  Rider сообщил 0 ошибок в новых рендер-файлах; в старом общем файле ложные сообщения
  о Deconstruct в неизменённых tuple-выражениях (компилятор их принимает).
  UI трея/меню/настроек, сохранённый конфиг и пользовательский publish-батник не менялись;
  legacy WPF/Skia сохранены. Коммита нет. Визуальную проверку проводит пользователь.
- Инструкция исправления Enter сокращена до пяти шагов; конфиг и игровой код не изменены.
- **Перед перезапуском 2026-10-14:** исправлены привязки Enter в пользовательском
  `C:\Users\Paul\.codex\config.toml` по локальной инструкции; резервная копия
  `config.toml.before-enter-fix-20261014-192512-517139.bak`. TOML валиден, остальные
  настройки сохранены. Enter/Shift+Enter — перенос, Ctrl+Enter — отправка, очередь
  по Tab отключена. Проверка клавиш после `codex --no-daemon resume --last`.
  Следующая работа после перезапуска: добиться плавности хотя бы одного рендера,
  приоритет WPF Turbo с цветной тенью и цель минимум 30 FPS. Пользователь разрешил
  запуски приложения и замеры; сообщил о включённом Rider MCP. Избегать чрезмерных
  затрат времени/токенов; оптимизировать подтверждённые узкие места. Legacy WPF можно
  удалить по необходимости после достижения результата. В этом шаге змейка не менялась.
- **Ревью производительности 2026-10-14:** игровой код не изменён. Пользователь сообщает
  рывки во всех трёх рендерах; оценка 4–6 FPS пока не измерена. Подтверждено кодом:
  - Skia/Turbo Balls: `1 - (1 - progress)^3` даёт разгон/торможение на каждом шаге
    (87,5% пути за первую половину интервала); нужна линейная интерполяция для
    постоянной скорости.
  - Общий Balls OnRender: `_lastStepTime = t` теряет остаток времени. При 30 кадрах/с
    интервал 45 мс приводит к шагам через ~66,7 мс и паузам после завершения анимации.
    Кроме того, `t` кадра предшествует `SyncSegs.MoveStart = Now`, а progress ограничен
    только сверху: первый кадр Skia/Turbo шага может отрисовать отрицательное смещение.
  - Legacy WPF: Canvas.Left/Top-анимации всех сегментов, покадровое изменение
    GradientStops/Stroke/DropShadow.Color, отдельный DropShadowEffect на сегмент.
  - Skia: полный Clear + CopyTo(byte[]) + Marshal.Copy(BackBuffer) + полный dirty rect;
    новые градиентные shaders на каждый сегмент/кадр и CPU blur сегментов/частиц.
    Windows VirtualScreen = 5280×1080; при таком bitmap две копии — ~43,5 МиБ/кадр
    (~1,27 ГиБ/с при 30 FPS, без учёта чтения/записи, очистки и вывода).
  - Skia/Turbo продолжают покадровое изменение hue при Rainbow=false; Skia частицы
    используют blur даже при DropShadow=false. Эти переключатели не полностью
    выключают соответствующую работу.
  - Ribbon: Insert(0), полный проход TrimRibbonTo, новая геометрия каждый кадр;
    Turbo создаёт PathGeometry/LineSegment/brush/pen заново. dt ограничен 50 мс,
    поэтому при редких кадрах лента дополнительно замедляется.
  - BFS: общий UI-поток, Dictionary/Queue/HashSet создаются каждый поиск. Возможный
    источник задержек/GC, но без профиля нельзя назвать главным узким местом.
  - Общая для трёх рендеров прозрачная WPF-поверхность всего VirtualScreen остаётся
    кандидатом на узкое место вывода; программный рендер и реальный FPS не измерены.
  Рекомендуемый порядок: измерить интервалы кадров отдельно от времени логики/рисования,
  исправить время шагов и интерполяцию, затем оптимизировать подтверждённые затраты.
  Debug build (`--no-restore`): 0 warnings / 0 errors; ручного запуска не было.
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
- **Skia миграция — реализована** (CPU backend через WriteableBitmap, т.к.
  `AllowsTransparency=True` блокирует GPU `SKGLEView`):
  - `Settings.Renderer` enum {Wpf, Skia} + сохранение в settings.json.
  - В SettingsWindow добавлен раздел "🖥️ RENDERER" с двумя радио (WPF/Skia),
    вверху перед RENDER MODE.
  - `Snake/SnakeGameSkia.cs` (partial class `SnakeGame`): `SKBitmap` + `WriteableBitmap`,
    per-frame `Marshal.Copy` BGRA → WPF `Image`. `SKCanvas.Clear(transparent)` →
    рисование еды/сегментов/ленты/частиц одним батчем.
  - `SnakeGame.xaml.cs` — `_isSkia` флаг; `OnRender`/`Start`/`SyncSegs`/`AddSegVisual`/
    `Die*`/`EatFoodShared`/`TriggerBurst`/`CreateFood`/`CreateSeg`/`EnsureRibbonPath`/
    `UpdateBallsColors` — ветвятся по `_isSkia`. Skia-сегменты интерполируются вручную
    (Stored/To/MoveStart/MoveDuration), WPF-сегменты — через Storyboard-анимации как раньше.
  - `App.xaml.cs` — CLI `--snake` для прямого запуска змейки в обход idle-таймера
    (полезно для тестов/демо, не ломает основной поток).
  - `TrayHost.LaunchSnake` теперь логирует renderer и show-event.
  - Skia 4.150.0 + `SkiaSharp.NativeAssets.Win32`, `NoWarn=CS0618` (deprecated SKPath API).
  - По умолчанию `Renderer = Wpf` как fallback; после визуальной проверки можно переключить на Skia.

## Open issues / next steps

- **РЕАЛИЗОВАНО:** переключатель `Settings.Renderer { Wpf, Skia, WpfTurbo }` + чекбокс
  `Settings.DropShadow`. Высота SettingsWindow 540→980. Баг WPF `_foods.Add(food)` в `CreateFood` починен.
  **Apply в Settings перезапускает змейку** (`TrayHost.RestartSnakeIfVisible`).
- **WPF TURBO — РЕАЛИЗОВАН** (третий режим): `DrawingContext`-based `SnakeTurboCanvas`,
  переиспользование frozen `RadialGradientBrush`/`Pen` через hue-keyed кеши, glow halo
  (RadialGradient без blur) вместо DropShadowEffect. Manual-интерполяция как в Skia
  (Stored/To/MoveStart/MoveDuration). Тени ЦВЕТНЫЕ (в отличие от серых Skia теней),
  дешёвые (DrawEllipse halo O(1) на сегмент без ImageFilter/blur).
  - `SnakeTurboCanvas.cs` (new): Canvas subclass, OnRender → `Host.TurboRender`. Кеши
    `_glow`/`_fill` (RadialGradientBrush), `_strokeColors` (SolidColorBrush),
    `_pens` (Pen), `_foodGeo` (Geometry). Все frozen.
  - `SnakeGameTurbo.cs` (new, partial class): `TurboInit/Resize/Reset/CreateSeg/AddFood/
    EatFood/Burst/Redraw`; `TurboRender` диспетчер → `TurboDrawFoods/Balls/Ribbon/Particles`.
    Manual interpolation по `Stored/To/MoveStart/MoveDuration` (= Skia путь).
  - `SnakeGame.xaml`: добавлен `<snake:SnakeTurboCanvas x:Name="TurboCanvas" Visibility="Collapsed"/>`
    в общий Grid (xmlns:snake объявлен).
  - `SnakeGame.xaml.cs`: `_isTurbo` флаг; ветки в constructor/OnLoaded/InitFromSize/OnRender/
    Start/StartBalls/SyncSegs/AddSegVisual/UpdateBallsColors/EnsureRibbonPath/DieBalls/
    DieRibbon/EatFoodShared/TriggerBurst/CreateFood. WPF/Skia пути не тронут.
  - `SettingsWindow.xaml/.cs`: третья радио "WPF TURBO".
  - Smoke-тесты: 0 exceptions за 8s для `Render=2+Mode=0+DropShadow=true`,
    `Render=2+Mode=1+DropShadow=true`, обратный smoke `Render=0+Mode=0+DropShadow=true`.
- **Вывод пользователя (визуально):**
  1. WPF быстрее Skia в этом проекте.
  2. Тень даёт большую нагрузку на производительность в обоих рендерах.
  3. Плавное движение (интерполяция) видно только в WPF при выключенной тени.
  4. В Skia движение рывками — рисуется сразу новый шарик, без интерполяции.
  5. В Skia тень СЕРАЯ, не цветная (`SKImageFilter.CreateBlur` теряет saturation).
  6. **Итог: Skia в текущем виде не подходит.** WPF (без тени) — почти ОК, короткая
     плавная, длинная — рывки.
  7. **WPF TURBO ждёт визуальной проверки пользователем** на длинной змейке.
- Не до конца подтверждён initial-позиционирование ShowMenu (нужен живой тест — открыть меню,
  раскрыть Expander — убедиться что якорь не дрейфит и низ на таскбаре).
- Padding экспандера был пофиксен пользователем: `TextBlock Padding="20"` внутри стиля TrayExpander.
- **Skia perf/jank** — Skia в текущем виде (CPU bitmap + Marshal.Copy) НЕ устраивает пользователя.
  Варианты "ускорить Skia" требуют профайлинга и кода. Пользователь ответил: НЕ заниматься
  профилированием Skia — добавлен Turbo вместо.
- **Skia DropShadow в Skia-режиме серый:** `SKImageFilter.CreateBlur` на `SKPaint_color.WithAlpha(120-160)`
  даёт бледно-серое. В Turbo — цветная тень (radial gradient halo). Не дублировать в Skia
  без запроса.
