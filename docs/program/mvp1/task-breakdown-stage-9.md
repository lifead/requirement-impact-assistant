# Task breakdown MVP-1. Stage 9

## Назначение

Документ фиксирует финальный gate MVP-1 после завершения Stage 1-8.

Stage 9 не добавляет новый пользовательский функционал, production code, RAG pipeline, внешние интеграции или новые обязательные provider checks. Его задача - подтвердить, что реализованный MVP-1 соответствует утвержденным требованиям, technical design и implementation plan, а затем зафиксировать итоговое состояние отдельным summary.

Stage 9 запускается отдельным решением пользователя после Stage 8 summary. Каждая task проходит стандартный цикл:

```text
task review -> execution -> review -> full checks -> git diff --stat -> explicit user command for commit -> explicit user command for push -> next task
```

## Основание

- `docs/program/mvp1/mvp1-strategy.md`
- `docs/program/mvp1/clarification-decisions.md`
- `docs/program/mvp1/requirements-draft.md`
- `docs/program/mvp1/technical-design.md`
- `docs/program/mvp1/implementation-plan.md`
- `docs/program/mvp1/stage-1-summary.md`
- `docs/program/mvp1/stage-2-summary.md`
- `docs/program/mvp1/stage-3-summary.md`
- `docs/program/mvp1/stage-4-summary.md`
- `docs/program/mvp1/stage-5-summary.md`
- `docs/program/mvp1/stage-6-summary.md`
- `docs/program/mvp1/stage-7-summary.md`
- `docs/program/mvp1/stage-8-summary.md`

## Границы Stage 9

Входит:

- финальный review соответствия MVP-1 требованиям, technical design и implementation plan;
- проверка, что Stage 1-8 summaries согласованы с кодом и тестами;
- проверка application-level boundary для интеллектуального анализа;
- проверка разделения `DirectLlm` и `ExternalRag`;
- проверка, что UI/PageModels не зависят напрямую от external adapter/provider;
- проверка, что export строится из сохраненного результата и не вызывает `IAiAnalysisEngine`, selector, adapter или provider;
- проверка, что default build/test не требует Dify/DeepSeek secrets, real network, real corporate data или обязательной внешней конфигурации;
- проверка, что legacy MVP-0 results остаются совместимы;
- фиксация итоговых build/test/diff-check результатов;
- создание `docs/program/mvp1/stage-9-summary.md`.

Не входит без отдельного решения:

- новый production feature behavior;
- собственный RAG/retrieval pipeline;
- embeddings, rerank, vector database или agentic workflow;
- новые Jira/Confluence/ALM/mail/chat integrations;
- dashboard или workflow согласования;
- обязательный real Dify/DeepSeek network gate;
- новые secrets/config в репозитории;
- browser/E2E, load, performance или production integration test suite;
- изменение MVP-0 документов как части MVP-1 closure.

## Task 1. Stage 9 gate breakdown review

**Цель:** подтвердить, что Stage 9 оформлен как финальный review/gate, а не как новый implementation stage.

**Входит:**

- review этого task breakdown;
- проверка, что Stage 9 не расширяет утвержденный scope MVP-1;
- проверка, что final gate не требует real external provider, secrets или network;
- `git diff --check`;
- `git diff --stat`;
- подготовка документа к commit после успешного review; commit выполняется только по отдельной явной команде пользователя.

**Не входит:**

- production code changes;
- test code changes;
- запуск full MVP-1 gate до review breakdown.

**Критерии Done:**

- breakdown принят review-субагентом;
- diff содержит только `docs/program/mvp1/task-breakdown-stage-9.md`;
- `git diff --check` проходит;
- breakdown готов к commit после успешного review;
- commit и push breakdown выполнены только по отдельным явным командам пользователя.

## Task 2. Full MVP-1 gate review and summary

**Цель:** провести финальную проверку MVP-1 и зафиксировать итоговый статус.

**Входит:**

- отдельный code review-субагент для всего MVP-1 состояния;
- проверка соответствия requirements/technical design/implementation plan;
- проверка ключевых boundaries:
  - UI/PageModels -> application services;
  - application services -> selector/`IAiAnalysisEngine`;
  - external provider details stay behind adapter boundary;
  - direct LLM does not depend on external adapter;
  - export -> saved result graph only;
  - mock/default tests -> no network/secrets;
- targeted source scan по forbidden boundaries and non-goals;
- `dotnet build RequirementImpactAssistant.sln`;
- `dotnet test RequirementImpactAssistant.sln`;
- `git diff --check`;
- `git status --short --branch`;
- создание `docs/program/mvp1/stage-9-summary.md`;
- review итогового summary/diff;
- подготовка summary к commit после успешного gate;
- commit и push summary выполняются только по отдельным явным командам пользователя.

**Не входит:**

- исправления без отдельного implementation cycle, если review найдет дефект;
- новые guards, если текущие Stage 8 guards уже покрывают final boundary;
- обязательная ручная Dify/DeepSeek проверка;
- расширение MVP-1 за пределы утвержденных документов.

**Критерии Done:**

- финальный review MVP-1 вернул APPROVED или все замечания исправлены отдельным циклом;
- build проходит;
- full test suite проходит;
- `git diff --check` проходит;
- `stage-9-summary.md` точно фиксирует выполненные проверки, сохраненные non-goals и остаточные optional/manual checks;
- working tree clean после commit/push;
- `git log --oneline -5` показывает финальный Stage 9 commit.

## Итоговый критерий закрытия MVP-1

MVP-1 считается полностью закрытым только если:

- Stage 1-8 завершены, reviewed, committed and pushed;
- Stage 9 breakdown reviewed и, после отдельных явных команд пользователя, committed and pushed;
- финальный Stage 9 review не выявил blocking defects;
- `dotnet build RequirementImpactAssistant.sln` и `dotnet test RequirementImpactAssistant.sln` проходят в default contour;
- `git diff --check` чистый;
- `docs/program/mvp1/stage-9-summary.md` создан, reviewed и, после отдельных явных команд пользователя, committed and pushed;
- финальный `git status --short --branch` показывает clean working tree.
