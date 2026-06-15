# MVP1-STAB-004. Временное удаление загрузки контекстного фрагмента через файл

Статус: реализовано в текущем рабочем diff.

Severity / area: Medium / context fragments, local browser compatibility.

## Проблема

В MVP-1 пользовательский сценарий загрузки контекстного фрагмента через `input type="file"` в Details UI конфликтовал с локальным тестированием в окружении Kaspersky/Yandex Browser. Для стабилизации демонстрационного сценария live upload через файл временно убран из страницы деталей и PageModel.

## Scope decision

В рамках item удалены только:

- upload form на странице `Analyses/Details`;
- `UploadContextFragment` POST handler;
- upload-specific input model, file validation и создание stored upload path;
- upload-specific tests, которые проверяли live загрузку файла.

Ручное добавление контекста через textarea остается основным MVP-сценарием.

## Сохраненный extension point

Поля `ContextFragment.FileName` и `ContextFragment.FilePath` не удаляются из домена, EF-модели, БД, snapshots, export contracts и Details display model. Эти поля сохраняются как extension point / future file-context metadata для будущего возврата file-context функциональности в другом формате.

Это не legacy-deletion: существующие и будущие metadata-backed фрагменты по-прежнему могут отображать имя файла, участвовать в export/snapshot контракте и удаляться с cleanup сохраненного файла при наличии безопасного `FilePath`.

## Не меняется

- Миграции не создаются.
- Export contract не меняется.
- Analysis flow, LLM/provider boundary и RAG behavior не меняются.
- Новый file picker, drag-and-drop, storage service, embeddings/indexing или RAG-интеграция не добавляются.

## Проверки

- В production `Details.cshtml` нет `asp-page-handler="UploadContextFragment"`, `enctype="multipart/form-data"` и `type="file"`.
- В production `Details.cshtml.cs` нет `OnPostUploadContextFragmentAsync`, `UploadContextFragmentInput`, `FileContextFragmentInput` и `IFormFile`.
- Ручной `AddContextFragment` сценарий остается покрыт page tests.
- `FileName` и `FilePath` остаются в Details model и продолжают покрываться тестом существующих metadata-фрагментов.
