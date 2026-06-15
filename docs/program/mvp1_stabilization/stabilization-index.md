# MVP-1 stabilization index

| ID | Статус | Severity / area | Краткое описание | Item |
| --- | --- | --- | --- | --- |
| MVP1-STAB-001 | Реализовано в текущем рабочем diff | Medium / SQLite persistence, startup | Автоматическое восстановление локальной SQLite-схемы при старте приложения без создания пользовательских данных. | [MVP1-STAB-001-sqlite-schema-startup-migration.md](MVP1-STAB-001-sqlite-schema-startup-migration.md) |
| MVP1-STAB-002 | Реализовано в текущем рабочем diff | Medium / context upload, controlled error | Контролируемая ошибка при сбое загрузки или сохранения файла контекста без аварийного завершения страницы. | [MVP1-STAB-002-upload-context-file-controlled-error.md](MVP1-STAB-002-upload-context-file-controlled-error.md) |
| MVP1-STAB-003 | Реализовано в текущем рабочем diff | Low / local diagnostics, startup logging | Явная настройка локального консольного логирования ASP.NET Core без использования Windows Event Log provider. | [MVP1-STAB-003-console-logging-no-eventlog.md](MVP1-STAB-003-console-logging-no-eventlog.md) |
| MVP1-STAB-004 | Реализовано в текущем рабочем diff | Medium / context fragments, local browser compatibility | Временное удаление live upload UI/PageModel ветки для контекстных файлов при сохранении `FileName`/`FilePath` как extension point / future file-context metadata. | [MVP1-STAB-004-remove-context-file-upload-ui.md](MVP1-STAB-004-remove-context-file-upload-ui.md) |
