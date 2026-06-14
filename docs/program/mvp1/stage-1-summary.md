# Stage 1 summary MVP-1

## Назначение

Stage 1 MVP-1 закрыла минимальную модель и контракты engine mode и retrieved context, чтобы приложение могло различать direct LLM и external AI/RAG результаты без привязки к Dify, внешнему response format или собственной RAG-реализации.

## Основание

- `docs/program/mvp1/task-breakdown-stage-1.md`
- `docs/program/mvp1/implementation-plan.md`

## Что было реализовано

- task breakdown Stage 1;
- `AnalysisMode` и `RetrievedContextState`;
- metadata результата анализа: mode, engine/provider/adapter/model/workflow/profile, retrieved context state, warnings и признак передачи manual context во внешний AI/RAG-контур;
- минимальная модель retrieved context item;
- persistence для analysis metadata;
- persistence для retrieved context;
- round-trip сохранения и чтения Stage 1 данных, включая legacy MVP-0 compatibility;
- заполнение metadata для direct LLM без изменения поведения анализа;
- проверка external AI/RAG-shaped persistence без production adapter, mock adapter, Dify adapter или реального внешнего вызова.

## Покрытые контракты и данные

- Direct LLM результат сохраняет `DirectLlm` metadata и не получает искусственный retrieved context.
- Legacy MVP-0 результат без полной MVP-1 metadata остается читаемым и не считается поврежденным.
- External AI/RAG-shaped результат может быть сохранен и прочитан с retrieved context `Available`, `MetadataOnly`, `Unavailable` и `Partial`.
- Manual context отделен от retrieved context; retrieved context представлен как основание предварительного AI/RAG результата, а не как экспертное заключение.
- Metadata и persistence остаются neutral/provider-agnostic и не требуют Dify-specific payload.

## Коммиты Stage 1

- `f8ed9ef` - docs: add MVP-1 stage 1 task breakdown
- `ad27357` - Add analysis mode and retrieved context state
- `ffa9d12` - Add analysis result metadata model
- `ee407be` - Add retrieved context item model
- `44996ce` - Split MVP-1 stage 1 persistence tasks
- `788aed3` - Revise MVP-1 persistence task split
- `4005bef` - Add analysis metadata persistence
- `7c282db` - Add retrieved context persistence
- `0dab46a` - Add stage 1 result persistence round trip
- `c8a1098` - Populate direct LLM analysis metadata
- `144980f` - Verify external RAG result persistence

## Проверки

- `dotnet build` прошел.
- `dotnet test` прошел.
- 181/181 tests passed.
- `git diff --check` без ошибок.
- final `git status` был clean.
- ветка `mvp1` синхронизирована с `origin/mvp1`.

## Важное решение по Task 4

Первоначальное разбиение Task 4 было остановлено на review: незакоммиченный diff откатили, task breakdown исправили, после чего Stage 1 продолжили через отдельные Task 4a и Task 4b с коммитопригодным разделением metadata persistence и retrieved context persistence.

## Что Stage 1 НЕ делала

- UI не менялся.
- Markdown/JSON export не менялся.
- Mock external RAG adapter не добавлялся.
- Dify adapter не добавлялся.
- Реальные external calls не добавлялись.
- RAG, embeddings, rerank и vector database не добавлялись.
- MVP-0 документы не изменялись.

## Готовность к Stage 2

Stage 1 подготовила основу для Stage 2: расширения Markdown и JSON export на базе сохраненных mode, metadata и retrieved context данных.

## Следующий шаг

Следующий шаг - подготовить task breakdown для Stage 2 из `docs/program/mvp1/implementation-plan.md`: `Этап 2. Расширение Markdown и JSON export`.

Реализацию Stage 2 не начинать без отдельного task breakdown, review и явной команды пользователя.
