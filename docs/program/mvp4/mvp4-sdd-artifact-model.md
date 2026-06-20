# MVP-4 SDD Artifact Model

## Статус

Этот документ является процессным SDD-артефактом для MVP-4.

Документ не является требованиями, техническим проектом, implementation plan или решением о начале реализации.

## Назначение

Документ фиксирует минимальную модель артефактов для доработки UI в MVP-4, чтобы не переходить к реализации преждевременно и не подменять анализ свободным "vibe coding".

Цель модели - сохранить управляемый staged workflow проекта и использовать внешние подходы только как источники полезных шаблонов, контрольных вопросов и review-практик.

## Основное решение

Текущий project SDD и правила `AGENTS.md` остаются главным управляющим процессом.

GitHub Spec Kit, OpenSpec, BDD и ADR/RFC-lite не заменяют проектный workflow. Они используются только как источники отдельных артефактов и проверочных вопросов.

MVP-4 продолжает двигаться через phase gates:

`analysis -> clarifications -> requirements -> requirements review -> technical design -> design review -> implementation plan -> plan review -> small implementation tasks`

Переход к следующему этапу разрешен только после review предыдущего этапа.

## Borrowed Elements

Из GitHub Spec Kit можно заимствовать:

- идею `constitution` как набора устойчивых правил и ограничений;
- явное разделение `spec`, `plan`, `tasks`;
- практику `clarify` перед фиксацией требований;
- практику `analyze/checklist` для проверки согласованности артефактов.

Из OpenSpec можно заимствовать:

- компактную модель change proposal;
- разделение proposal, requirements/spec delta, design и tasks;
- идею хранения артефактов изменения отдельно от общей документации до завершения change; это не означает обязательное внедрение директории `openspec/` или внешнего CLI.

Из BDD можно заимствовать:

- acceptance examples для демонстрационного сценария;
- проверочные сценарии в форме поведения системы;
- фокус на наблюдаемом результате, а не на внутренней реализации.

Из ADR/RFC-lite можно заимствовать:

- фиксацию значимых решений;
- описание контекста, альтернатив и последствий;
- отдельное документирование решений, которые затрагивают boundaries.

## Prohibited Elements

В MVP-4 запрещено использовать внешние подходы как основание для преждевременной реализации.

Запрещено фиксировать скрытые решения по:

- UI layout;
- PageModel;
- routes;
- storage;
- API;
- DB;
- migrations;
- state machine;
- новым workflow-механизмам;
- компонентной структуре;
- provider-specific DTO;
- RAG pipeline;
- embeddings;
- rerank;
- retrieval architecture.

Запрещено превращать MVP-4 в:

- ALM-систему;
- Jira/Confluence-аналог;
- task board;
- workflow platform;
- RAG-платформу;
- систему автоматического принятия управленческих решений.

LLM/AI/RAG формирует только preliminary analytical material. Expert conclusion остается отдельным человеческим действием и не смешивается с результатом AI-анализа.

## MVP-4 Artifact Chain

Минимальная цепочка артефактов для MVP-4:

1. `mvp4-ui-simplification-concept.md`  
   Концептуальная рамка UI-упрощения и границы MVP-4.

2. `mvp4-sdd-artifact-model.md`  
   Процессная модель артефактов и review gates для MVP-4.

3. `mvp4-ui-information-architecture-analysis.md`  
   Анализ текущих экранов, смысловых блоков, пользовательского маршрута и демонстрационного сценария.

4. `mvp4-ui-clarifications.md`  
   Зафиксированные уточнения после information architecture analysis и до черновика требований.

5. `mvp4-ui-requirements-draft.md`  
   Черновик требований после завершенного analysis и clarifications.

6. `mvp4-ui-requirements-review.md`  
   Review требований.

7. `mvp4-ui-technical-design.md`  
   Технический проект только после утверждения требований.

8. `mvp4-ui-technical-design-review.md`  
   Review технического проекта.

9. `mvp4-ui-implementation-plan.md`  
   План реализации только после review технического проекта.

10. Task-level implementation artifacts  
    Малые задачи реализации: одна task - один проверяемый результат - один review - один commit.

## Review Gates

Каждый этап должен проходить review до перехода дальше.

Review должен проверять:

- не подменяет ли артефакт текущий project SDD внешним фреймворком;
- не содержит ли premature implementation;
- не фиксирует ли скрытые UI/layout/PageModel/routes/components/storage/API/DB/migrations/state-machine/provider-specific DTO/RAG pipeline решения;
- сохраняет ли разделение preliminary AI material и expert conclusion;
- не расширяет ли MVP в ALM/Jira/RAG/workflow platform;
- остается ли документ в своем уровне: analysis, requirements, design или implementation plan;
- достаточно ли ясно указаны границы и следующий допустимый шаг.

Если review выявляет замечания, исправления вносятся до перехода к следующему этапу, после чего review повторяется.

## Связь с `mvp4-ui-simplification-concept.md`

`mvp4-ui-simplification-concept.md` задает концептуальную рамку: MVP-4 упрощает пользовательский маршрут и визуальную иерархию вокруг существующего сохраненного `analysis`.

Этот документ уточняет не UI-решение, а процесс движения от этой концепции к следующим SDD-артефактам.

Связка документов означает:

- концепция определяет направление;
- artifact model определяет допустимый процесс;
- information architecture analysis проверяет текущие экраны и смысловые блоки;
- requirements фиксируются только после analysis и review.

## Следующий Шаг

Следующий допустимый SDD-шаг: подготовить `mvp4-ui-information-architecture-analysis.md`.

Этот analysis-документ должен изучить текущие экраны и демонстрационный сценарий, но не проектировать layout, PageModel, storage, API, DB, state machine или implementation tasks.
