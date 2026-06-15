# MVP-1 stabilization index

| ID | Статус | Severity / area | Краткое описание | Item |
| --- | --- | --- | --- | --- |
| MVP1-STAB-001 | Реализовано в текущем рабочем diff | Medium / SQLite persistence, startup | Автоматическое восстановление локальной SQLite-схемы при старте приложения без создания пользовательских данных. | [MVP1-STAB-001-sqlite-schema-startup-migration.md](MVP1-STAB-001-sqlite-schema-startup-migration.md) |
| MVP1-STAB-002 | Реализовано в текущем рабочем diff | Medium / context upload, controlled error | Контролируемая ошибка при сбое загрузки или сохранения файла контекста без аварийного завершения страницы. | [MVP1-STAB-002-upload-context-file-controlled-error.md](MVP1-STAB-002-upload-context-file-controlled-error.md) |
| MVP1-STAB-003 | Реализовано в текущем рабочем diff | Low / local diagnostics, startup logging | Явная настройка локального консольного логирования ASP.NET Core без использования Windows Event Log provider. | [MVP1-STAB-003-console-logging-no-eventlog.md](MVP1-STAB-003-console-logging-no-eventlog.md) |
