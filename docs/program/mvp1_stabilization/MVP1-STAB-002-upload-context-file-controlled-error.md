# MVP1-STAB-002. Контролируемая ошибка при сбое загрузки файла контекста

Статус: реализовано в текущем рабочем diff.

Severity / area: Medium / context upload, controlled error.

## Проблема

При ожидаемом сбое записи uploaded context file или сохранения `ContextFragment` handler страницы деталей мог пробрасывать исключение наружу. Для MVP-1 stabilization это ухудшало локальную демонстрацию: пользователь видел аварийный сбой вместо короткого сообщения о проблеме загрузки.

## Scope decision

В рамках item изменяется только поведение `OnPostUploadContextFragmentAsync` при ожидаемых upload/persistence failures.

Не входит:

- изменение пути хранения uploaded files;
- изменение Razor UI;
- изменение схемы БД, migrations, startup или DI;
- добавление LLM/RAG/export behavior.

## Внесенное решение

Handler сохраняет best-effort cleanup через `DeleteStoredFileBestEffort(relativePath, analysis.Id)`.

Для ожидаемых `IOException`, `UnauthorizedAccessException` и `DbUpdateException` handler добавляет user-facing ошибку на `UploadContextFragmentInput.File`, повторно загружает `Analysis` и возвращает `Page()`.

Сообщение: "Не удалось загрузить файл. Проверьте файл и повторите попытку."

## Проверки

- При сбое сохранения `ContextFragment` возвращается `PageResult`.
- `ModelState` становится invalid и содержит ошибку на `UploadContextFragmentInput.File`.
- `Analysis` загружается для повторного отображения страницы.
- Временный stored upload file удаляется best-effort cleanup.
- `ContextFragments` не сохраняются при сбое.
