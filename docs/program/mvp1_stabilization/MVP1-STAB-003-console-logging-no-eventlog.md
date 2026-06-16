# MVP1-STAB-003. Консольное логирование без Windows Event Log

Статус: Реализовано в текущем рабочем diff.

Severity / area: Low / local diagnostics, startup logging.

## Проблема

Для локальной демонстрации и smoke-проверок MVP-1 приложение должно писать диагностические сообщения ASP.NET Core в консоль предсказуемым образом.

При использовании default logging providers в Windows окружении возможна регистрация Windows Event Log provider. Для локального MVP-сценария это лишний и менее прозрачный канал диагностики: сообщения могут уходить не туда, куда смотрит разработчик или эксперт при запуске приложения из консоли.

## Основание

Analysis и review для stabilization задачи завершены со статусом APPROVED.

Рекомендованная реализация: сразу после `WebApplication.CreateBuilder(args)` явно настроить logging providers:

```csharp
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
```

`Logging:LogLevel` в `appsettings` должны остаться фильтрами уровня логирования. Изменение provider list не требует изменения `appsettings`.

## Scope decision

В рамках item фиксируется только явный локальный logging setup ASP.NET Core приложения:

- очистить default logging providers;
- добавить console provider как обязательный локальный диагностический канал;
- добавить debug provider как допустимый вспомогательный локальный канал;
- не использовать Windows Event Log provider;
- сохранить существующие `Logging:LogLevel` фильтры из конфигурации.

## Non-goals

Не входит:

- изменение business logic;
- изменение upload flow или обработки файлов контекста;
- изменение EF, migrations, схемы БД или persistence lifecycle;
- изменение UI, Razor pages или пользовательских сообщений;
- изменение analysis flow, AI/provider boundary или экспертного workflow;
- добавление новых dependencies;
- добавление RAG, external AI/RAG engines, agentic workflow или внешних интеграций;
- настройка production observability, structured logging backend, telemetry или centralized log storage.

## Review note

`ClearProviders()` also removes `EventSourceLoggerProvider`.

Это принято для MVP-1 local diagnostic scope: текущая stabilization задача ограничена локальным консольным запуском и не требует EventSource/ETW diagnostic channel. При будущей production observability задаче provider list может быть пересмотрен отдельно.

## Task breakdown

### Task 1. Implementation

Цель: явно настроить локальные logging providers в `Program.cs`.

Шаги:

- В `Program.cs` после `WebApplication.CreateBuilder(args)` вызвать `builder.Logging.ClearProviders()`.
- Добавить `builder.Logging.AddConsole()` как обязательный provider для локальной диагностики.
- Добавить `builder.Logging.AddDebug()` как вспомогательный provider, если это остается совместимо с текущими package references и без добавления dependencies.
- Не добавлять `AddEventLog()` и не оставлять неявную зависимость от Windows Event Log provider.
- Не менять `appsettings` Logging section: `Logging:LogLevel` должны продолжать применяться как фильтры.
- При необходимости добавить статический regression test на source-level check:
  - проверяет наличие `ClearProviders()` и `AddConsole()`;
  - проверяет отсутствие `AddEventLog()`;
  - не делает `AddDebug()` жестким обязательным условием.

Ожидаемый результат: приложение при локальном запуске пишет ASP.NET Core логи в консоль и не использует Windows Event Log provider.

### Task 2. Review, verification and delivery

Цель: проверить минимальность изменения и зафиксировать результат после review.

Шаги:

- Выполнить review diff на отсутствие изменений вне logging setup и возможного статического regression test.
- Выполнить build/test, применимые для MVP-1 stabilization.
- Убедиться, что `Program.cs`, возможный test и документация не нарушают application-level AI/provider boundaries.
- Перед commit показать `git diff --stat`.
- Commit и push выполнять только по явной команде пользователя.

Консервативная альтернатива: если изменение ограничится тремя строками в `Program.cs` и не будет добавляться regression test, Task 1 может быть единственной implementation task, а Task 2 остается review/delivery gate без отдельного product behavior scope.

## Done criteria

- `Program.cs` явно вызывает `builder.Logging.ClearProviders()` после создания builder.
- `Program.cs` явно добавляет `builder.Logging.AddConsole()`.
- В production code отсутствует `builder.Logging.AddEventLog()` или аналогичное подключение Windows Event Log provider.
- `Logging:LogLevel` в `appsettings` не изменены и продолжают работать как фильтры.
- Если добавлен regression test, он статически проверяет `ClearProviders()`, `AddConsole()` и отсутствие `AddEventLog()`, но не требует `AddDebug()` как обязательное условие.
- Не изменены business logic, upload, EF/migrations, UI, analysis flow и dependencies.
- Review явно учитывает, что `ClearProviders()` удаляет также EventSource provider, и это приемлемо для MVP-1 local diagnostic scope.

## Implementation notes / current diff

- В `Program.cs` сразу после `WebApplication.CreateBuilder(args)` добавлены `ClearProviders()`, `AddConsole()` и `AddDebug()`.
- `AddEventLog()` не добавлен.
- Добавлен source-level regression test `StartupLoggingRegressionTests`, который проверяет `ClearProviders()`, `AddConsole()` и отсутствие `AddEventLog()`, но не делает `AddDebug()` обязательным условием.
- `appsettings`, business logic, upload flow, EF/migrations, UI, analysis flow и dependencies не изменялись.

## Checks

- Релевантный test filter: выполнен.
- `git diff --check`: выполнен.
- `dotnet test RequirementImpactAssistant.sln`: выполнен.
