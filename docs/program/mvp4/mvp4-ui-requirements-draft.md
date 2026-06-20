# MVP-4 UI Requirements Draft

## Статус

Этот документ является requirements draft для MVP-4.

Документ не является техническим проектом, implementation plan, task breakdown или решением о начале реализации.

Документ не фиксирует UI layout, component structure, routes, PageModel, storage/API/DB/migrations/state-machine, RAG pipeline, provider-specific DTO, CSS или code tasks.

Требования описывают проверяемое поведение и смысловую структуру UI на уровне пользователя и демонстрационного сценария.

## Назначение

MVP-4 должен упростить пользовательский маршрут и визуальную иерархию существующего приложения вокруг одного сохраненного `analysis`, не меняя предметную логику программы.

Основная цель требований - зафиксировать, что пользователь должен понимать и различать при работе с сохраненным анализом:

- что анализируется;
- что является текущим состоянием;
- что является проектным изменением;
- какой контекст добавлен человеком;
- какой контекст найден или не найден внешним AI/RAG-контуром;
- что является preliminary AI material;
- что проверяет эксперт;
- какое expert conclusion зафиксировано человеком;
- какой результат можно экспортировать без повторного анализа.

## Scope

В scope MVP-4 входит:

- уточнение смысловых ролей существующих UI-областей приложения;
- упрощение пользовательского понимания одного сохраненного `analysis`;
- различение входных данных, manual context, retrieved context, preliminary AI material, expert evaluation и expert conclusion;
- сохранение регистрационно безопасного разделения AI-материала и человеческого экспертного действия;
- проверяемая поддержка демонстрационного сценария MVP-3;
- отображение существующих результатов, ограничений и export без расширения предметной логики;
- улучшение читаемости и смысловой приоритетности информации на уровне требований.

## Non-Scope

В MVP-4 не входит:

- новая доменная модель;
- новая БД, migrations или изменение хранения;
- новый workflow согласования или state machine;
- task board, backlog, sprint management или approval workflow;
- автоматическое принятие, отклонение или эскалация проектного изменения;
- создание задач, pull request или поручений к реализации;
- новая RAG-платформа;
- embeddings, vector database, rerank или новый retrieval pipeline;
- новая внешняя интеграция;
- расширение Dify adapter;
- перевод UI или export на provider-specific DTO;
- новый export-формат;
- PDF-генерация;
- фиксация layout, components, routes, PageModel, CSS или code tasks.

## Actors

- Пользователь: человек, который открывает сохраненный `analysis`, просматривает входные данные, результаты, ограничения и export.
- Эксперт: человек, который выполняет expert evaluation и фиксирует expert conclusion.
- Демонстратор / исследователь: человек, который проводит воспроизводимый показ MVP на обезличенном сценарии.
- AI/RAG/LLM-контур: вспомогательный программный контур, формирующий preliminary analytical material. Он не является субъектом управления и не принимает экспертных или управленческих решений.

## Terms

- `analysis`: сохраненный артефакт анализа влияния одного проектного изменения.
- `input`: исходные данные проектного запроса: текущее состояние, проектное изменение, ситуация и причина изменения, источник изменения.
- `manual context`: контекстные материалы, явно добавленные человеком.
- `retrieved context`: материалы или metadata, сохраненные как результат работы external AI/RAG adapter для конкретного анализа.
- `preliminary AI material`: предварительный аналитический материал, сформированный AI/RAG/LLM-контуром.
- `preliminary impact map`: предварительная карта влияния как часть preliminary AI material.
- `grounds/limitations`: основания, ограничения, warnings и сведения о границах применимости preliminary result.
- `expert evaluation`: человеческая проверка, дополнение и корректировка preliminary AI material.
- `expert conclusion`: итоговое человеческое экспертное заключение, отличное от AI-result и expert evaluation.
- `export`: сохраненный Markdown/JSON результат без повторного анализа.

## Functional Requirements

### FR-001 Central Analysis Artifact

Приложение должно представлять один сохраненный `analysis` как центральный рабочий артефакт MVP-4.

Проверка: пользователь может объяснить, что текущая работа относится к одному проектному изменению, а не к задаче, тикету, workflow-item, approval item или автоматическому решению.

### FR-002 Saved Analyses Orientation

Приложение должно позволять пользователю выбрать сохраненный `analysis` и понять его текущее состояние на смысловом уровне.

Проверка: по списку сохраненных анализов пользователь понимает, какой анализ открыть и требует ли он проверки входных данных, просмотра результата, expert evaluation или expert conclusion.

### FR-003 Review As Preflight

Приложение должно сохранять смысловую роль `Review` как preflight-проверки перед получением preliminary AI material.

Проверка: пользователь понимает, что `Review` отвечает на вопрос: достаточно ли входных данных и контекста, чтобы запускать preliminary analysis в выбранном режиме.

### FR-004 Input Readiness

Приложение должно помогать пользователю проверить наличие и достаточность ключевых входных данных до запуска preliminary analysis.

Проверка: пользователь может проверить текущее состояние, проектное изменение, ситуацию и причину изменения, источник изменения и manual context.

### FR-005 Analysis Mode Clarity

Приложение должно показывать выбранный режим анализа на provider-neutral уровне.

Проверка: пользователь понимает, используется ли `Direct LLM` или `External AI/RAG`, без раскрытия секретов, private endpoint, bearer token, cookies, CSRF или raw provider payload.

### FR-006 Details As Saved Analysis Overview

Приложение должно сохранять смысловую роль `Details` как обзорной точки сохраненного `analysis`.

Проверка: пользователь понимает, что уже известно по анализу и какое человеческое действие логически остается следующим.

### FR-007 Details Must Not Equal Raw Data Dump

`Details` не должен представлять все данные анализа как равнозначное полотно без смысловой приоритетности.

Проверка: пользователь различает основные смысловые слои анализа, а не воспринимает input, AI material, diagnostics и expert conclusion как один общий технический блок.

### FR-008 Preliminary AI Material Visibility

Приложение должно показывать preliminary AI material как предварительный аналитический материал.

Проверка: пользователь видит preliminary result и preliminary impact map, но не воспринимает их как expert conclusion, управленческое решение или поручение к реализации.

### FR-009 Expert Evaluation Role

Приложение должно сохранять expert evaluation как человеческую проверку preliminary AI material.

Проверка: эксперт может определить, что он подтверждает, исправляет, дополняет или считает недостаточно обоснованным в preliminary impact map.

### FR-010 Expert Conclusion Separation

Приложение должно отделять expert conclusion от preliminary AI material и expert evaluation.

Проверка: пользователь понимает, что expert conclusion фиксируется человеком и не является результатом AI, workflow approval, task assignment или автоматической командой к реализации.

### FR-011 Manual Context Origin

Приложение должно показывать manual context как материалы, добавленные человеком.

Проверка: пользователь отличает manual context от retrieved context и не воспринимает manual context как найденный external provider-ом источник.

### FR-012 Retrieved Context Origin And State

Приложение должно показывать retrieved context как результат external AI/RAG adapter-а или mock adapter-а для конкретного анализа.

Проверка: пользователь понимает, является ли retrieved context available, partial, metadata-only или unavailable.

### FR-013 No Fabricated Retrieved Context

Приложение не должно маскировать отсутствие retrieved context выдуманными источниками.

Проверка: если retrieved context отсутствует, недоступен или представлен только metadata, это состояние видно пользователю как ограничение.

### FR-014 Grounds And Limitations

Приложение должно показывать grounds/limitations как объяснение границ preliminary result.

Проверка: пользователь понимает режим анализа, наличие или отсутствие retrieved context, warnings, limitation notes и сведения, требующие человеческой проверки.

### FR-015 Diagnostics Are Not Main Scenario

Приложение должно сохранять диагностические сведения как вспомогательные материалы для проверки корректности MVP, troubleshooting и review.

Проверка: raw response, provider metadata, adapter diagnostics, internal identifiers и low-level retrieval metadata не воспринимаются как основной пользовательский маршрут.

### FR-016 Export Without Reanalysis

Приложение должно позволять получить сохраненный Markdown/JSON export без повторного запуска preliminary analysis.

Проверка: export отражает сохраненные слои анализа и не запускает новый AI/RAG/LLM-вызов.

### FR-017 Export Layer Separation

Export должен сохранять различимость input, manual context, retrieved context, preliminary AI material, grounds/limitations, expert evaluation, expert conclusion и decision boundary.

Проверка: по export можно подтвердить, что AI-result и человеческое заключение не смешаны.

### FR-018 Demo Scenario Completeness

Приложение должно поддерживать демонстрационный сценарий MVP-3 про уведомление заявителя после изменения статуса заявки.

Проверка: пользователь проходит путь от обезличенных input data и manual context до preliminary result, expert evaluation, expert conclusion и export.

### FR-019 Offline Default Demo Safety

Default demo-сценарий должен оставаться воспроизводимым без обязательного real Dify, DeepSeek, сети, user-secrets или API keys.

Проверка: Direct LLM работает через demo/mock provider, External AI/RAG работает через mock adapter или безопасный fallback, если real adapter не настроен.

### FR-020 No Automatic Management Decision

Приложение не должно представлять AI/RAG/LLM-result как управленческое решение.

Проверка: пользователь понимает, что программа готовит analytical material, а решение, оценка и заключение остаются за человеком.

## Information Hierarchy Requirements

### IH-001 Semantic Priority

Приложение должно различать primary, secondary и diagnostic information на смысловом уровне.

Проверка: при review требований можно отнести каждый блок к одной из категорий без проектирования layout или component structure.

### IH-002 Primary Information

К primary information относятся сведения, без которых пользователь не понимает суть анализа и человеческое решение:

- название анализа;
- тип проектного запроса;
- текущее состояние;
- проектное изменение;
- ситуация и причина изменения;
- источник изменения;
- manual context;
- состояние retrieved context;
- preliminary AI material;
- preliminary impact map;
- grounds/limitations;
- expert evaluation;
- expert conclusion;
- exportable result.

### IH-003 Secondary Information

К secondary information относятся сведения, которые помогают ориентироваться и проверять воспроизводимость:

- timestamps;
- analysis status;
- provider-neutral сведения о режиме анализа;
- summary по retrieved context;
- warnings summary;
- expert evaluation status;
- export availability;
- input snapshot.

### IH-004 Diagnostic Information

К diagnostic information относятся сведения для проверки корректности работы MVP, troubleshooting и review:

- raw response;
- detailed provider metadata;
- adapter diagnostics;
- technical payload fragments;
- detailed warnings;
- internal identifiers;
- low-level retrieval metadata;
- JSON-like technical fragments, если они присутствуют в текущем UI.

### IH-005 Classification Is Not UI Architecture

Классификация `primary / secondary / diagnostic` не должна становиться обязательной UI-архитектурой.

Проверка: требования не задают tabs, panels, routes, layout, component structure, display order, CSS или PageModel.

### IH-006 Immediate Orientation

При открытии одного `analysis` пользователь должен понимать:

- какой проектный запрос анализируется;
- что является текущим состоянием;
- что является проектным изменением;
- есть ли preliminary result;
- есть ли ограничения или неполнота контекста;
- была ли выполнена expert evaluation;
- есть ли expert conclusion;
- какой следующий человеческий шаг логически ожидается.

## AI And Expert Separation Requirements

### AE-001 AI Boundary

AI/RAG/LLM-контур должен описываться только как источник preliminary analytical material.

### AE-002 Human Expert Boundary

Expert evaluation и expert conclusion должны описываться как человеческие действия.

### AE-003 No AI Expert Conclusion

Приложение не должно создавать впечатление, что expert conclusion сформирован AI/RAG/LLM-контуром.

### AE-004 No Retrieved Context As Proof

Retrieved context не должен представляться как доказательство правильности preliminary AI material само по себе.

### AE-005 Human Verification Required

Preliminary AI material должен оставаться материалом, требующим человеческой проверки.

### AE-006 Corrections And Missed Items

Expert evaluation должна позволять смыслово различить подтвержденные элементы, исправленные элементы, missed items, comments, context sufficiency и usefulness.

## Demo Scenario Acceptance Examples

### Example A: Direct LLM Demo

Given пользователь создал `analysis` с обезличенными данными сценария уведомления заявителя  
And пользователь добавил manual context fragments `DEMO-API-NOTIFY-v1` и `DEMO-ADR-04`  
When пользователь выбирает режим `Direct LLM` и запускает preliminary analysis  
Then приложение показывает preliminary AI material как предварительный результат  
And retrieved context не подменяется искусственными источниками  
And пользователь может выполнить expert evaluation  
And пользователь может зафиксировать expert conclusion человеком  
And Markdown/JSON export содержит input, manual context, preliminary result, grounds/limitations, expert evaluation, expert conclusion и decision boundary без повторного анализа.

### Example B: External AI/RAG Mock Demo

Given пользователь создал `analysis` с тем же обезличенным сценарием  
And пользователь выбрал режим `External AI/RAG`  
When preliminary analysis выполнен через mock adapter или безопасный fallback  
Then приложение показывает provider-neutral сведения о режиме анализа  
And manual context отделен от retrieved context  
And retrieved context state показан как available, partial, metadata-only или unavailable  
And отсутствие retrieved context показано как limitation, а не как выдуманный источник  
And expert evaluation учитывает состояние retrieved context до expert conclusion.

### Example C: Expert Evaluation Separation

Given preliminary impact map уже сформирована  
When эксперт оценивает элементы карты влияния  
Then приложение различает, что предложил AI, что эксперт подтвердил, что эксперт исправил и что эксперт добавил как missed item  
And expert evaluation не воспринимается как повторный вызов provider-а  
And expert conclusion остается отдельным человеческим заключением.

### Example D: Export Boundary

Given по analysis сохранены preliminary result, expert evaluation и expert conclusion  
When пользователь получает Markdown или JSON export  
Then export отражает сохраненное состояние analysis  
And export не запускает повторный AI/RAG/LLM-анализ  
And export сохраняет разделение AI-result и человеческого expert conclusion.

## Quality And Traceability Requirements

### QR-001 Traceability To MVP-3 Demo

Каждое ключевое требование MVP-4 должно быть проверяемо на демосценарии изменения уведомления заявителя после смены статуса заявки или явно отмечено как вспомогательное.

### QR-002 Registration-Safe Language

UI и документация MVP-4 должны сохранять формулировки, в которых программа помогает готовить аналитический материал, но не принимает управленческие решения.

### QR-003 Provider Neutrality

Требования должны сохранять provider-neutral описание AI/RAG/LLM-режимов и не требовать раскрытия secrets, private endpoints, bearer values, cookies, CSRF или raw provider payload.

### QR-004 Existing Boundary Preservation

MVP-4 не должен требовать изменения domain/storage/AI boundary/export format без отдельного scope decision.

### QR-005 Reviewability

Requirements review должен иметь возможность проверить каждое требование по ID.

### QR-006 Phase Gate Compliance

После этого requirements draft следующий допустимый шаг - requirements review. Переход к technical design допустим только после review требований.

## Red Flags For Requirements Review

Requirements draft считается проблемным, если обнаружен хотя бы один признак:

- требования описывают конкретный layout, panels, tabs, components, CSS, PageModel или routes;
- требования фиксируют storage/API/DB/migrations/state-machine решения;
- требования проектируют RAG pipeline, embeddings, rerank, vector DB или provider-specific DTO;
- `Details` закрепляется как техническая витрина всех данных без смысловой приоритетности;
- `Review` превращается в повтор полного `Details`;
- `ExpertEvaluation` смешивает AI-result и экспертные действия;
- expert conclusion выглядит как результат AI или автоматическое решение программы;
- retrieved context описывается как доказательство правильности AI-result само по себе;
- absence/partial state retrieved context скрывается от пользователя;
- manual context и retrieved context теряют различимость;
- grounds/limitations исчезают или становятся только технической диагностикой;
- diagnostic blocks начинают управлять основным пользовательским сценарием;
- `primary / secondary / diagnostic` превращается в обязательную UI-архитектуру;
- появляются новые workflow/status/state-machine сущности;
- появляются task board, approval workflow, dashboard, PDF/export expansion, ALM/Jira/Confluence-аналог или RAG-платформа;
- requirements draft начинает формулировать implementation tasks.

## Open Assumptions For Requirements Review

1. `Details` остается обзорной точкой сохраненного `analysis` на уровне смысла, но review должен подтвердить, достаточно ли это корректно для MVP-4.
2. `Review` трактуется как preflight перед preliminary analysis, а не как экран экспертной оценки.
3. `ExpertEvaluation` трактуется как человеческая проверка preliminary AI material.
4. Expert conclusion остается отдельным человеческим действием.
5. Markdown/JSON export уже существует как сохраняемый результат и не расширяется новым форматом.
6. Direct LLM и External AI/RAG рассматриваются как существующие режимы демонстрации, без добавления новой интеграции.
7. `primary / secondary / diagnostic` используется только как язык смысловой приоритетности.
8. Требования не должны становиться technical design даже если review подтвердит их корректность.
9. Следующий SDD-шаг после утверждения требований - technical design, а не реализация.
10. Любое изменение domain/storage/AI boundary/export format требует отдельного scope decision.
