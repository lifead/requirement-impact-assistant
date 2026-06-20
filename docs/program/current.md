# Текущее состояние программы

Этот файл является кратким навигационным индексом по состоянию проекта. Он не заменяет staged workflow, не является roadmap и не открывает новые работы по реализации.

## Current state

Текущий этап проекта - `MVP-4` на ветке `mvp4`.

MVP-4 посвящен упрощению UI вокруг одного сохраненного `analysis`: от входных данных и manual context к preliminary AI material, expert evaluation, expert conclusion и Markdown/JSON export.

Ключевая граница сохраняется: LLM/AI/RAG формирует предварительный аналитический материал, а экспертная оценка, экспертное заключение и управленческое рассмотрение остаются за человеком.

Основные документы текущего этапа:

- [MVP-4 UI simplification concept](mvp4/mvp4-ui-simplification-concept.md)
- [MVP-4 UI Information Architecture Analysis](mvp4/mvp4-ui-information-architecture-analysis.md)
- [MVP-4 UI Clarifications](mvp4/mvp4-ui-clarifications.md)
- [MVP-4 UI Requirements Draft](mvp4/mvp4-ui-requirements-draft.md)
- [MVP-4 UI Requirements Review](mvp4/mvp4-ui-requirements-review.md)
- [MVP-4 UI Technical Design](mvp4/mvp4-ui-technical-design.md)
- [MVP-4 UI Technical Design Review](mvp4/mvp4-ui-technical-design-review.md)
- [MVP-4 UI Implementation Plan](mvp4/mvp4-ui-implementation-plan.md)
- [MVP-4 UI Implementation Plan Review](mvp4/mvp4-ui-implementation-plan-review.md)
- [MVP-4 SDD Artifact Model](mvp4/mvp4-sdd-artifact-model.md)

Статус MVP-4: requirements review, technical design review и implementation plan review зафиксированы как `PASS`. Реализация возможна только отдельными implementation tasks после явной команды пользователя на конкретную task.

## Historical artifacts

Документы ниже являются историческими артефактами завершенных или предшествующих этапов. Они сохраняют решения, ограничения и проверочные материалы, но не описывают текущий этап как "планируемый MVP-1".

### MVP-0

MVP-0 зафиксировал исходный MVP с direct LLM analysis через `DirectLlmAnalysisEngine`, ручным контекстом, экспертной оценкой, экспертным заключением, Markdown/JSON export и защитой воспроизводимости результата.

Основные документы:

- [Начальная концепция](mvp0/initial-concept.md)
- [Уточнения и решения](mvp0/clarification-decisions.md)
- [Черновик требований](mvp0/requirements-draft.md)
- [Технический проект](mvp0/technical-design.md)
- [План реализации](mvp0/implementation-plan.md)
- [UI-концепция](mvp0/ui-concept.md)
- [End-to-end smoke checklist](mvp0/end-to-end-smoke-checklist.md)

### MVP-1

MVP-1 закрыл staged workflow по external AI/RAG boundary за `IAiAnalysisEngine`: сохранен default direct path, добавлен provider-neutral external contour, optional Dify adapter behind boundary, retrieved context metadata, сохранение/экспорт external результата и regression checks.

Основные документы:

- [Стратегия MVP-1](mvp1/mvp1-strategy.md)
- [Уточнения и решения](mvp1/clarification-decisions.md)
- [Черновик требований](mvp1/requirements-draft.md)
- [Технический проект](mvp1/technical-design.md)
- [План реализации](mvp1/implementation-plan.md)
- [Stage 9 summary MVP-1](mvp1/stage-9-summary.md)
- [MVP-1 stabilization index](mvp1_stabilization/stabilization-index.md)

### MVP-2

MVP-2 содержит материалы по Dify Agent как optional concrete provider за external AI/RAG adapter boundary, manual smoke checklist и обезличенные demo RAG-документы. Эти материалы не превращают Dify или RAG в ядро программы.

Основные документы:

- [MVP-2 Dify Agent integration notes](mvp2/dify-agent-integration-notes.md)
- [MVP-2 Dify Agent manual smoke checklist](mvp2/dify-agent-manual-smoke-checklist.md)
- [MVP-2 Dify Agent streaming task breakdown](mvp2/task-breakdown-dify-agent-streaming.md)
- [Dify workflow contract](mvp2/demo-dify-rag-reports/dify-workflow-contract.md)

### MVP-3

MVP-3 зафиксировал демонстрационную и регистрационную готовность локального web-приложения: различение input/manual context/retrieved context/preliminary material/expert evaluation/expert conclusion, сохраненный `analysis` как проверяемый артефакт и offline/default-safe demo smoke.

Основные документы:

- [MVP-3 UX and registration strategy](mvp3/mvp3-ux-registration-strategy.md)
- [MVP-3 UI flow](mvp3/mvp3-ui-flow.md)
- [Черновик требований MVP-3](mvp3/mvp3-requirements-draft.md)
- [Технический проект MVP-3](mvp3/mvp3-technical-design.md)
- [MVP-3 implementation plan](mvp3/mvp3-implementation-plan.md)
- [MVP-3 demo smoke checklist](mvp3/mvp3-demo-smoke-checklist.md)
- [MVP-3 registration readiness notes](mvp3/mvp3-registration-readiness-notes.md)
