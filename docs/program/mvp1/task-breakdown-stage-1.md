# Task breakdown MVP-1. Stage 1

## Назначение

Документ фиксирует маленькие implementation tasks только для этапа 1 из `docs/program/mvp1/implementation-plan.md`: "Модель и контракты engine mode и retrieved context".

Цель этапа - подготовить минимальное расширение существующей модели результата, чтобы приложение могло различать direct LLM и external AI/RAG режимы без привязки к Dify или конкретному внешнему response format.

Этот breakdown не разрешает реализацию автоматически. Каждая task должна проходить отдельный цикл:

```text
task review -> implementation -> code review -> commit -> next task
```

## Границы Stage 1

Входит:

- доменное или application-level представление режимов `DirectLlm` и `ExternalRag`;
- metadata результата анализа: analysis mode, engine name, provider name, adapter name, model/workflow/profile name, retrieved context state, warnings, признак передачи manual context во внешний AI/RAG-контур;
- состояния retrieved context: `Available`, `MetadataOnly`, `Unavailable`, `Partial`;
- минимальная модель retrieved context item или metadata;
- обратная совместимость с MVP-0 результатами без полной metadata MVP-1;
- отсутствие искусственного retrieved context для direct LLM;
- возможность сохранить external AI/RAG результат с retrieved context, metadata или limitation.

Не входит:

- export;
- UI;
- mock external RAG adapter;
- Dify adapter;
- реальный внешний вызов;
- собственный RAG, embeddings, rerank, vector database или retrieval trace;
- история всех запусков анализа, новая audit-система или расширение workflow;
- изменение документов MVP-0.

## Task 1. Ввести режим анализа и состояние retrieved context

**Цель:** зафиксировать минимальные типы Stage 1, чтобы дальнейшие изменения опирались на единый vocabulary и не размазывали строки по коду.

**Зависимости:** нет.

**Входит:**

- `AnalysisMode` или согласованный аналог с минимумом значений `DirectLlm` и `ExternalRag`;
- `RetrievedContextState` или согласованный аналог со значениями `Available`, `MetadataOnly`, `Unavailable`, `Partial`;
- при необходимости - небольшие helper-методы для безопасного преобразования в строку и обратно, если существующее хранение работает со строковыми значениями;
- unit tests на допустимые значения и обратимое преобразование, если в проекте уже есть подходящие тесты для доменных value objects/enums.

**Не входит:**

- изменение UI и выбора режима пользователем;
- selector/registry `IAiAnalysisEngine`;
- external adapter contract;
- сохранение retrieved context items;
- миграция БД;
- Dify-specific значения или поля.

**Ожидаемый diff:**

- небольшие новые файлы в domain/application area для enum/value object типов;
- точечные тесты на новые типы;
- без изменений Razor Pages, exporters, adapters и MVP-0 документов.

**Проверки:**

- `dotnet test` или более узкий тестовый проект, если в репозитории принят scoped запуск;
- ручная проверка diff на отсутствие Dify, UI, export и external-call кода.

**Критерии Done:**

- в коде есть единое представление двух режимов анализа;
- в коде есть единое представление четырех состояний retrieved context;
- новые типы не зависят от Dify, SDK, endpoint, response format или внешнего provider;
- существующие тесты MVP-0 проходят.

**Red flags для review:**

- значения enum названы через конкретный provider, например `Dify`;
- в task появился выбор режима в UI;
- добавлены сетевые зависимости, настройки provider или adapter;
- direct LLM начинает требовать retrieved context state для выполнения анализа.

## Task 2. Описать metadata результата анализа на уровне модели результата

**Цель:** расширить модель результата так, чтобы один результат мог явно нести сведения о mode, engine/provider/adapter и ограничениях, не меняя смысл существующей карты влияния и не вводя еще модель retrieved context items.

**Зависимости:** Task 1.

**Входит:**

- модель metadata результата анализа, включающая:
  - analysis mode;
  - engine name;
  - provider name, если применимо;
  - adapter name, если применимо;
  - model/workflow/profile name, если применимо;
  - retrieved context state;
  - warnings;
  - признак того, передавался ли manual context во внешний AI/RAG-контур;
- default/empty-представление metadata для direct LLM и legacy MVP-0 результата без полной metadata;
- минимальные правила metadata: mode и retrieved context state должны быть представлены нейтрально, без требования к concrete retrieved context item model.

**Не входит:**

- изменение prompt, provider call или поведения `DirectLlmAnalysisEngine`, кроме заполнения metadata результата;
- external adapter request/response contract;
- export;
- UI;
- миграция БД, если она требует отдельного diff;
- история всех запусков анализа.

**Ожидаемый diff:**

- расширение существующей модели `AiAnalysisResult` или близкого application/domain DTO;
- новые тесты на default metadata для direct LLM и legacy-результата;
- тесты на нейтральное представление retrieved context state без item model.

**Проверки:**

- `dotnet test`;
- targeted тесты модели результата, если они выделены отдельно;
- review diff на то, что `ImpactMap` не переписывается и не получает обязательные ссылки на retrieved context.

**Критерии Done:**

- результат анализа может представить mode и engine/provider/adapter metadata;
- отсутствие MVP-1 metadata не делает legacy MVP-0 результат ошибочным;
- direct LLM metadata не создает искусственное основание retrieved context;
- warnings можно сохранить как часть metadata без привязки к конкретному внешнему формату.

**Red flags для review:**

- обязательные Dify/workflow-specific поля попали в доменную модель;
- metadata смешана с экспертной оценкой или экспертным заключением;
- legacy MVP-0 результат начинает считаться invalid;
- metadata требует concrete retrieved context item до появления отдельной модели retrieved context.

## Task 3. Добавить минимальную модель retrieved context item

**Цель:** подготовить нейтральную модель основания external AI/RAG результата, отделенную от manual context и пригодную для сохранения полного, частичного или metadata-only основания.

**Зависимости:** Task 1, Task 2.

**Входит:**

- модель `RetrievedContextItem` или аналог с минимальными полями:
  - source title;
  - source id или external reference;
  - fragment/chunk id, если доступен;
  - text или excerpt, если доступен;
  - url/reference, если доступен;
  - rank/score, если доступен;
  - provider/adapter;
  - признак полноты;
  - warning или limitation note;
- явное задание полноты item: full text, excerpt only, metadata only, unavailable, без отдельного retrieval trace или внутреннего state machine;
- тесты для full, metadata-only, unavailable и partial item/state сценариев на уровне модели;
- явное отделение `RetrievedContextItem` от существующего manual/context fragment.

**Не входит:**

- собственный retrieval trace;
- связи retrieved context item с каждым элементом `ImpactMap`;
- редактирование retrieved context как manual context;
- UI просмотра retrieved context;
- mock adapter или реальный provider.

**Ожидаемый diff:**

- новый domain/application model файл для retrieved context item;
- тесты полноты item и связи item со state результата;
- без изменений existing context fragment модели, кроме ссылок, необходимых для компиляции.

**Проверки:**

- `dotnet test`;
- review diff на отсутствие попытки строить RAG, embeddings, rerank или поиск;
- review, что retrieved context не становится subtype существующего manual context.

**Критерии Done:**

- external AI/RAG результат может представить full, metadata-only, unavailable и partial retrieved context;
- item может хранить источник, ссылку, rank/score и limitation только если они доступны;
- provider/adapter у item нейтральны и не требуют Dify-specific payload;
- direct LLM результат по-прежнему не получает retrieved context items.

**Red flags для review:**

- модель требует обязательный текст фрагмента и не позволяет metadata-only;
- в модель попали внутренние поля конкретного внешнего response format;
- появилась логика поиска или ранжирования внутри приложения;
- retrieved context item связан с управленческим решением, а не с предварительным AI/RAG результатом.

## Task 4a. Добавить persistence для analysis metadata с EF migration

**Цель:** добавить коммитопригодное persistence-представление metadata результата анализа вместе с EF/SQLite migration, чтобы mainline не содержал EF-visible mapping, ожидающий отсутствующие колонки.

**Зависимости:** Task 2.

**Входит:**

- EF/entity configuration для metadata-level fields результата анализа;
- EF/SQLite migration и snapshot/schema changes для metadata columns;
- schema-level представление:
  - analysis mode;
  - engine/provider/adapter/model/workflow/profile metadata;
  - retrieved context state как metadata-level значение;
  - warnings;
  - manual-context-to-external flag;
- model/migration tests, если в проекте уже есть соответствующий уровень проверки или он нужен для покрытия migration;
- `dotnet build` и полный `dotnet test`.

**Не входит:**

- retrieved context item storage;
- repository/application-service round-trip;
- legacy-read tests;
- UI;
- export;
- adapters;
- Dify/mock adapter;
- реальный внешний вызов;
- RAG, embeddings, rerank, vector database или retrieval trace;
- история всех запусков анализа или новая audit-система;
- изменения MVP-0 документов.

**Ожидаемый diff:**

- изменения persistence entity/configuration только для analysis metadata Stage 1;
- новая EF/SQLite migration и связанное snapshot/schema-изменение только для metadata columns;
- model/migration tests или минимальная schema/build проверка;
- без изменений Razor Pages, exporters, adapters и MVP-0 документов.

**Проверки:**

- `dotnet build`;
- полный `dotnet test`;
- targeted model/migration tests, если они выделены отдельно;
- ручная проверка diff на отсутствие retrieved context item storage, repository/application-service round-trip, legacy-read tests, UI/export/adapter кода, Dify-specific schema, RAG, embeddings, rerank и vector database.

**Критерии Done:**

- persistence mapping и schema готовы представить analysis metadata Stage 1;
- mapping не требует retrieved context item storage;
- migration добавляет только metadata columns Stage 1 и не оставляет модель в состоянии ожидания будущей схемы;
- существующие MVP-0 mapping/build сценарии не ломаются.

**Red flags для review:**

- вместе с metadata mapping добавлено хранение retrieved context items;
- task затронула repository/application-service round-trip или legacy-read тесты;
- migration содержит retrieved context item storage, audit/history/retrieval trace или provider-specific raw response как обязательную штатную модель;
- схема жестко кодирует Dify-specific поля или provider-specific payload.

## Task 4b. Добавить persistence для retrieved context с EF migration

**Цель:** добавить коммитопригодное persistence-представление retrieved context items/limitations вместе с EF/SQLite migration, сохранив repository/application-service round-trip для отдельной Task 5.

**Зависимости:** Task 3, Task 4a.

**Входит:**

- EF/entity configuration для retrieved context items/limitations;
- EF/SQLite migration и snapshot/schema changes для item storage;
- schema-level представление:
  - retrieved context state;
  - retrieved context items или limitation;
  - provider/adapter metadata у item, если она нужна для уже согласованной модели;
  - полнота item: full, excerpt only, metadata only, unavailable/limitation;
- `dotnet build` и полный `dotnet test`.

**Не входит:**

- repository/application-service round-trip;
- external adapter;
- Dify/mock adapter;
- RAG, embeddings, rerank, vector database или retrieval trace;
- export;
- UI;
- реальный внешний вызов;
- история всех запусков анализа или новая audit-система.

**Ожидаемый diff:**

- изменения persistence entity/configuration только для retrieved context Stage 1;
- новая EF/SQLite migration и связанное snapshot/schema-изменение только для item storage/limitations;
- model/migration tests или минимальная schema/build проверка;
- без изменений Razor Pages, exporters, adapters и MVP-0 документов.

**Проверки:**

- `dotnet build`;
- полный `dotnet test`;
- targeted model/migration tests, если они выделены отдельно;
- ручная проверка diff на отсутствие repository/application-service round-trip, UI/export/adapter кода, Dify-specific schema, RAG, embeddings, rerank и vector database.

**Критерии Done:**

- persistence mapping и schema готовы представить retrieved context state/items или limitation Stage 1;
- mapping поддерживает full, metadata-only, unavailable и partial/limitation сценарии без retrieval trace;
- migration добавляет только item storage/limitations Stage 1 и не оставляет модель в состоянии ожидания будущей схемы;
- direct LLM результат не получает synthetic retrieved context item.

**Red flags для review:**

- появилась repository/application-service round-trip логика;
- модель требует Dify-specific JSON или provider-specific raw response как штатное хранение;
- migration добавляет историю всех запусков, audit-систему или retrieval trace без отдельного решения;
- добавлены RAG, embeddings, rerank, vector database, mock adapter или внешний вызов.

## Task 5. Реализовать round-trip чтения и записи Stage 1 данных

**Цель:** обеспечить сохранение и чтение результата с mode, metadata и retrieved context state/items поверх подготовленной схемы, включая legacy compatibility.

**Зависимости:** Task 4b.

**Входит:**

- repository/application-service mapping для сохранения и чтения:
  - analysis mode;
  - engine/provider/adapter/model/workflow/profile metadata;
  - retrieved context state;
  - warnings;
  - manual-context-to-external flag;
  - retrieved context items или limitation;
- тест сохранения и чтения результата Stage 1;
- тест чтения legacy MVP-0 результата без новых полей;
- проверка, что отсутствие retrieved context для direct/legacy случаев читается как совместимое отсутствие данных, а не как повреждение.

**Не входит:**

- новая migration или изменение схемы сверх Task 4a и Task 4b;
- изменение поведения `DirectLlmAnalysisEngine`;
- тестовый external adapter или production external adapter;
- export;
- UI;
- история всех запусков анализа.

**Ожидаемый diff:**

- изменения mapping/repository/application persistence logic только для Stage 1 полей;
- repository/service tests на round-trip сохранение и legacy-read;
- без изменений Razor Pages, exporters, adapters и MVP-0 документов.

**Проверки:**

- `dotnet test`;
- `dotnet build`;
- targeted round-trip tests для metadata/retrieved context;
- review diff на отсутствие UI/export/adapter кода.

**Критерии Done:**

- приложение может сохранить и прочитать результат с mode и retrieved context metadata;
- legacy MVP-0 данные без полной metadata читаются без ошибки;
- отсутствие retrieved context для direct/legacy случаев не превращается в synthetic item;
- external-shaped результат может быть сохранен с retrieved context, metadata-only или limitation.

**Red flags для review:**

- round-trip логика требует Dify-specific JSON или provider-specific payload;
- legacy MVP-0 результат начинает считаться invalid;
- direct LLM результат получает пустой retrieved context item ради совместимости;
- появились изменения export/UI/adapter вне scope Stage 1.

## Task 6. Подключить заполнение metadata для direct LLM без изменения поведения анализа

**Цель:** сделать существующий direct LLM результат совместимым с новой моделью Stage 1, не меняя сам сценарий анализа и не добавляя ему retrieved context.

**Зависимости:** Task 2, Task 5.

**Входит:**

- заполнение `AnalysisMode.DirectLlm` для новых direct LLM результатов;
- сохранение существующих engine/provider/model metadata в новой или расширенной metadata модели;
- явная фиксация отсутствия retrieved context для direct LLM без создания items; если текущая модель требует state, это должно быть совместимое служебное значение, а не synthetic retrieved context;
- сохранение поведения `DirectLlmAnalysisEngine`, prompt и provider boundary без содержательных изменений;
- regression tests существующего direct LLM сценария на сохранение результата.

**Не входит:**

- выбор режима пользователем;
- selector/registry для нескольких engines;
- mock external RAG adapter;
- изменение prompt, структуры `ImpactMap` или экспертной оценки;
- UI/export.

**Ожидаемый diff:**

- точечная правка application service или `DirectLlmAnalysisEngine` в месте формирования результата;
- тесты на direct LLM metadata и отсутствие retrieved context items;
- без изменений provider adapter behavior.

**Проверки:**

- `dotnet test`;
- targeted regression test direct LLM save/read;
- review diff на отсутствие prompt churn и сетевой логики.

**Критерии Done:**

- новый direct LLM результат получает `DirectLlm` mode;
- metadata direct LLM сохраняет доступные engine/provider/model сведения;
- retrieved context items для direct LLM отсутствуют, а state не имитирует внешний retrieval;
- существующие MVP-0 анализы и тесты direct LLM не ломаются.

**Red flags для review:**

- direct LLM получает пустой или synthetic retrieved context item;
- direct LLM начинает зависеть от external AI/RAG доступности;
- prompt или provider request изменены без необходимости;
- legacy результаты мигрируются destructive-способом или требуют ручной правки данных.

## Task 7. Проверить сохранение external AI/RAG результата без adapter

**Цель:** доказать, что модель Stage 1 готова принять external AI/RAG результат с retrieved context или limitation, не реализуя external adapter, mock adapter, Dify adapter или реальный вызов.

**Зависимости:** Task 3, Task 5.

**Входит:**

- test-only factory/test fixture для результата `ExternalRag`, если это нужно для проверки модели хранения;
- сценарии сохранения и чтения external результата:
  - `Available` с retrieved context item;
  - `MetadataOnly` без текста фрагмента;
  - `Unavailable` с limitation;
  - `Partial` с warning;
- проверка manual-context-to-external flag: `true`, `false` и отсутствие значения для legacy/direct случаев;
- проверка, что provider/adapter/model/workflow/profile metadata опциональны и нейтральны.

**Не входит:**

- interface external AI/RAG adapter;
- `ExternalRagAnalysisEngine`;
- mock external RAG adapter;
- Dify adapter;
- network access;
- UI или export;
- собственный retrieval.

**Ожидаемый diff:**

- тесты round-trip для external-result data shape;
- при необходимости небольшая test helper/factory только в тестовом проекте;
- без production adapter classes и без provider-specific payload.

**Проверки:**

- `dotnet test`;
- review diff на отсутствие production external-call кода;
- review, что тестовые данные обезличены и не выглядят как реальные корпоративные материалы.

**Критерии Done:**

- external AI/RAG result shape сохраняется и читается во всех четырех состояниях retrieved context;
- metadata может быть сохранена без привязки к Dify;
- limitation и warnings не теряются при чтении;
- direct/legacy сценарии остаются совместимыми.

**Red flags для review:**

- ради проверки добавлен mock adapter или реальный adapter;
- тесты требуют сеть, user secrets, Dify или внешние ключи;
- external metadata моделируется через Dify-specific JSON как публичный контракт;
- partial/unavailable состояния блокируют сохранение пригодного аналитического результата.

## Итоговый критерий готовности Stage 1

Stage 1 считается готовым к переходу на следующий этап только если:

- все 8 tasks реализованы и прошли отдельные review: Task 1, Task 2, Task 3, Task 4a, Task 4b, Task 5, Task 6 и Task 7;
- `dotnet build` и `dotnet test` проходят без сети, Dify, user secrets и внешних ключей;
- можно сохранить и прочитать direct LLM результат, legacy MVP-0 результат и external AI/RAG-shaped результат;
- direct LLM не получает искусственный retrieved context;
- модель не содержит обязательных Dify-specific полей;
- в diff Stage 1 нет UI, export, mock adapter, Dify adapter, реального внешнего вызова, собственного RAG, embeddings, rerank или vector database.
