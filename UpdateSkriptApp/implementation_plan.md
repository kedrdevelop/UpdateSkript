[ARTIFACT: implementation_plan]
# Full Implementation Plan: UpdateSkript Enterprise Deployment Tool (C#)

Этот план описывает поэтапный перенос логики автоматизации из .bat в профессиональное WPF-приложение с использованием асинхронности и событийно-ориентированной архитектуры.

## Архитектурные принципы
1.  **Decoupling (Заменяемость)**: Логика PowerShell отделена от UI.
2.  **Reactivity (Реактивность)**: Все изменения в системе (прогресс скачивания, строки лога) мгновенно отражаются в интерфейсе через Data Binding.
3.  **Robustness (Отказоустойчивость)**: Обработка исключений на уровне бизнес-логики, а не просто "скрипт упал".

## Этап 1: Фундамент и Инфраструктура (Core Infrastructure)
*Создание «нервной системы» приложения.*

### [1.1 Logging & Communication Service]
- Внедрение `ILoggerService`: перенаправление выпоков PowerShell и системных сообщений в `ObservableCollection` для UI и одновременно в файл.
- Реализация потокового вывода: строки лога должны появляться в UI **в момент их появления**, а не после завершения скрипта.

### [1.2 Hardware Discovery Service]
- Портирование логики `Get-CimInstance`: асинхронное определение Model/Manufacturer при запуске.
- Привязка данных к Header-блоку в `MainWindow`.

## Этап 2: PowerShell Bridge & Phase Management
*Перенос мозга автоматизации.*

### [2.1 Enhanced PowerShell Service]
- Настройка `RunspacePool` для стабильной работы.
- Подписка на стримы `Output`, `Error`, `Warning` и `Progress` объекта PowerShell.
- Реализация маппинга: `Write-Progress` из PowerShell должен автоматически обновлять `ProgressBar` в C#.

### [2.2 State & Resume Service (Persistent Flags)]
- Класс `DeploymentStateProvider`: чтение/запись флагов в `C:\Users\Public\`.
- Реализация логики "Reinstall": метод для рекурсивной очистки стейта.

## Этап 3: Реализация Фаз (Business Logic Porting)
*Сердце приложения.*

### [3.1 Phase 1: Windows Updates Manager]
- Обёртка над модулем `PSWindowsUpdate`.
- Логика фильтрации «бесконечных» обновлений (Defender Intelligence и т.д.) через C# LINQ.

### [3.2 Phase 2: Dell Catalog Engine]
- **XML Parser**: Переход с медленного `[xml]` PowerShell на быстрый `System.Xml.Linq` (LINQ to XML).
- **Download Manager**: Использование `HttpClient` с поддержкой докачки и расчетом скорости (MB/s).
- Интеграция `pnputil` с парсингом stdout для обновления счетчика "Installed X of Y".

### [3.3 Phase 3: Windows 11 Upgrade Orchestrator]
- Логика монтирования ISO через `Storage.Api` или CLI.
- **Log Watcher**: Асинхронное «следование» за файлом `setupact.log` (Tail -f) для отображения фаз установки (Preparing, Installing 35%...).

## Этап 4: UI/UX & Visual Excellence
*Эстетика и WOW-эффект.*

- **Animations**: Плавные переходы между фазами.
- **Visual States**: Цвет интерфейса меняется при ошибке (Красный) или успехе (Зеленый).
- **Glassmorphism**: Добавление полупрозрачных элементов в стиле Windows 11.

## Этап 5: Оптимизация и Деплой (CI/CD)
- Конфигурация `ReadyToRun` для мгновенного старта приложения.
- Настройка `PublishSingleFile` для получения одного `.exe`.
- Финальное тестирование в режиме Audit Mode.

## Открытые вопросы (Senior Review Require)
> [!IMPORTANT]
> **Авто-перезагрузка:** Будем ли мы позволять приложению самому перезагружать ПК в середине процесса (как делает скрипт), или выводить кнопку "Reboot Now" для контроля пользователем?
> **Обработка ошибок:** При критической ошибке (например, нет интернета) — останавливаем всё или даем кнопку "Retry"?
