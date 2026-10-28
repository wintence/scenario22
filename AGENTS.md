# scenario22

scenario22 — настольное приложение для Windows на WPF, которое управляет сценариями запуска и закрытия программ. Пользователь создаёт сценарии, назначает им глобальные горячие клавиши и запускает одним нажатием. Интерфейс — на английском по умолчанию, русский включается в настройках (строки в `Themes/Strings.*.xaml`).

## Стек

- .NET 8, C# (nullable + implicit usings), WPF и Windows Forms (иконка в трее)
- System.Text.Json для хранения настроек
- Без сторонних NuGet-пакетов
- MVVM без фреймворка: `ObservableObject`, code-behind для окон и листов

## Команды

Запускать из корня `E:\Projects\Mode22`:

```powershell
dotnet build scenario22\scenario22.csproj
dotnet run --project scenario22\scenario22.csproj
dotnet publish scenario22\scenario22.csproj -c Release -r win-x64 --self-contained false
```

Тестов в проекте нет. После изменений обязательно проверять сборку.

## Структура

```text
scenario22/
├── App.xaml(.cs)          — точка входа, single-instance mutex, обработка исключений
├── MainWindow.xaml(.cs)   — главное окно и основная логика UI
├── ToastWindow.xaml(.cs)  — всплывающие уведомления
├── Models/                — Scenario, ScenarioAction, Hotkey, AppState, AccentPalette
├── Services/
│   ├── ScenarioRunner.cs  — последовательный запуск/закрытие действий
│   ├── SettingsService.cs — загрузка/сохранение %APPDATA%\scenario22\settings.json
│   ├── HotkeyService.cs   — регистрация глобальных горячих клавиш
│   ├── TrayService.cs     — иконка и меню в системном трее
│   ├── ThemeService.cs    — светлая/тёмная тема
│   ├── StartupService.cs  — автозапуск с Windows
│   ├── IconService.cs     — извлечение иконок приложений
│   └── NativeMethods.cs   — Win32 interop
├── Sheets/                — диалоги действий и сценариев
├── Controls/HotkeyBox     — ввод сочетания клавиш
├── Themes/                — Dark.xaml, Light.xaml, Styles.xaml, Icons.xaml
└── Utility/Converters.cs
```

## Как это работает

- `SettingsService` читает и пишет JSON в `%APPDATA%\scenario22\settings.json`; при повреждении файла создаётся резервная копия `settings.json.broken-*`, а состояние сбрасывается на значения по умолчанию.
- `ScenarioRunner.RunAsync` выполняет только включённые действия по порядку, с задержкой `DelaySeconds` (0–600), и возвращает отчёт об ошибках.
- Запуск выполняется через `ProcessStartInfo` с `UseShellExecute = true` и поддерживает переменные окружения.
- Закрытие ищет процессы по имени, сначала пытается `CloseMainWindow()`, затем `Kill(entireProcessTree: true)`. Системные процессы из `ProtectedProcesses` закрывать запрещено — эту защиту не ослаблять.
- `App` использует мьютекс `scenario22.SingleInstance`; повторный запуск активирует уже открытое окно. Поддерживается запуск в трей с аргументом `--tray`.

## Правила для агента

- Текст интерфейса пиши на русском; имена типов, методов и комментарии — на английском.
- Сохраняй существующий стиль: file-scoped namespace, `SetProperty`, свойства с вычисляемыми `[JsonIgnore]`-представлениями.
- Не редактируй `bin/` и `obj/` — это артефакты сборки.
- Настройки и сценарии пользователя не хранить в репозитории; работать с ними только через `SettingsService`.
- При изменении моделей учитывай сериализацию: вычисляемые свойства помечены `[JsonIgnore]`, `Scenario.Actions` требует подписки на изменения.
- Не добавляй сторонние пакеты без необходимости.
- После правок собирай проект и сообщай результат сборки.
