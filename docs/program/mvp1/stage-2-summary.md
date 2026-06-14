# Stage 2 summary MVP-1

## Назначение

Stage 2 MVP-1 завершена как export-only расширение Markdown и JSON export поверх данных, сохраненных на Stage 1.

Export теперь фиксирует MVP-1 metadata результата анализа и retrieved context так, чтобы direct LLM, legacy MVP-0 и external-shaped результаты можно было воспроизводимо сравнивать без повторного запуска интеллектуального анализа.

## Основание

- `docs/program/mvp1/task-breakdown-stage-2.md`
- `docs/program/mvp1/implementation-plan.md`

## Что было реализовано

- task breakdown Stage 2;
- Markdown export metadata результата анализа: mode, engine/provider/adapter/model-workflow-profile, usage manual context, retrieved context state, limitations и warnings;
- JSON export metadata результата анализа в стабильных camelCase полях;
- Markdown export retrieved context: state, items, source/reference metadata, completeness, rank/score, text/excerpt и limitation notes;
- JSON export блока `retrievedContext` со state, items, limitations и сохраненными item metadata;
- service-level round-trip проверки, что Markdown и JSON export читают saved Stage 1 data из SQLite, включая owned `RetrievedContextItems`;
- regression/architecture checks, что export остается read-only слоем и не зависит от analysis engine/provider/adapters/network.

## Выполненные tasks Stage 2

1. Добавить в Markdown блок происхождения результата.
2. Добавить в JSON стабильные блоки metadata результата.
3. Вывести retrieved context items в Markdown.
4. Вывести retrieved context в JSON.
5. Закрыть service-level export round-trip для saved Stage 1 данных.
6. Финальная regression-проверка Stage 2 и smoke/checklist.

## Покрытые контракты и данные

- Markdown и JSON export используют сохраненные Stage 1 metadata и retrieved context, а не запускают анализ повторно.
- Direct LLM результат показывает `DirectLlm` metadata и отсутствие retrieved context как состояние/limitation, без synthetic items.
- Legacy MVP-0 результат остается совместимым и экспортируется без требования ручного исправления данных.
- External-shaped saved result экспортируется с retrieved context state/items/metadata/text/excerpt/limitations/warnings.
- Manual context отделен от retrieved context; retrieved context остается основанием предварительного AI/RAG результата, а не экспертным заключением.
- Stable JSON export не превращает provider-specific response в публичный контракт.

## Коммиты Stage 2

- `f1303ff` - Add MVP-1 stage 2 task breakdown
- `f3cbbc5` - Add Markdown analysis result metadata export
- `c60e0fd` - Add JSON analysis result metadata export
- `f9d7905` - Add Markdown retrieved context export
- `0c92dda` - Add JSON retrieved context export
- `c7363c7` - Add export round-trip tests for saved Stage 1 data
- `10bc9f1` - Add Stage 2 export regression checks

## Проверки

- `dotnet build` прошел.
- `dotnet test` прошел.
- Targeted export tests по Markdown, JSON и service-level round-trip сценариям прошли по соответствующим tasks.
- Regression/architecture checks для export boundary прошли.
- `git diff --check` по task выполнялся без ошибок.
- Stage 2 tests проходили без сети, Dify, user secrets и внешних ключей.

## Что Stage 2 НЕ делала

- UI не добавлялся и не менялся.
- Mock external RAG adapter не добавлялся.
- Dify adapter не добавлялся.
- Real external calls не добавлялись.
- RAG, embeddings, rerank и vector database не добавлялись.
- Analysis engine selector/registry не добавлялся.
- PDF export, dashboard и workflow не добавлялись.
- MVP-0 документы не менялись.

## Готовность к Stage 3

Stage 2 подготовила export-слой для следующего этапа: нейтральный контракт external RAG adapter сможет опираться на уже стабильное представление metadata и retrieved context в сохраненном результате и export.

## Следующий шаг

Следующий шаг - подготовить task breakdown для Stage 3 из `docs/program/mvp1/implementation-plan.md`: `Neutral external RAG adapter contract`.

Реализацию Stage 3 не начинать без отдельного task breakdown, review и явной команды пользователя.
