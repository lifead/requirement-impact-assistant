# План реализации SDD MVP-1 программы анализа влияния проектных запросов

## Назначение документа

Документ фиксирует содержательный план реализации SDD MVP-1 после завершения и review требований MVP-1 и технического проекта MVP-1. Если требования или технический проект изменяются, этот implementation plan подлежит отдельному review перед началом реализации.

Основание:

- `docs/program/mvp1/mvp1-strategy.md`;
- `docs/program/mvp1/clarification-decisions.md`;
- `docs/program/mvp1/requirements-draft.md`;
- `docs/program/mvp1/technical-design.md`;
- зафиксированная реализация и проектные рамки MVP-0.

Документ не является реализацией, не создает task/story-файлы и не разрешает автоматический переход к написанию кода. Код, project files, тесты и документы MVP-0 сейчас не изменяются.

Перед началом любой конкретной реализации требуется отдельное явное решение пользователя о старте соответствующей task.

Главный принцип реализации:

```text
одна task -> один проверяемый результат -> один обозримый diff -> один review -> один commit
```

Commit и push выполняются только по отдельной явной команде пользователя. Перед каждым commit должен быть показан `git diff --stat`.

## Цель реализации MVP-1

Цель MVP-1 - развить MVP-0 за счет второго режима интеллектуального анализа через внешний AI/RAG-контур, сохранив существующий direct LLM режим и архитектурную границу `IAiAnalysisEngine`.

MVP-1 должен дать возможность:

- выбрать режим анализа: direct LLM или external AI/RAG;
- выполнить выбранный режим через application-level boundary;
- сохранить сведения о режиме, engine/provider/adapter и основании результата;
- отличать manual context от retrieved context;
- сохранить retrieved context или metadata, если внешний AI/RAG-движок их вернул;
- явно показать ограничение, если retrieved context недоступен или неполон;
- расширить Markdown и JSON export данными, нужными для воспроизводимости и будущего сравнения режимов;
- сохранить экспертную оценку и экспертное заключение как отдельный человеческий слой.

MVP-1 не должен превращать программу в RAG-платформу, workflow-систему, dashboard или оболочку над одним внешним продуктом.

## Границы реализации MVP-1

Входит:

- развитие существующего контура анализа MVP-0 без переписывания проекта с нуля;
- сохранение `DirectLlmAnalysisEngine` как отдельного поддерживаемого режима;
- добавление engine mode и metadata результата анализа;
- добавление модели retrieved context и состояния его полноты;
- расширение сохраненного результата анализа данными MVP-1;
- расширение Markdown и JSON export;
- добавление нейтрального контракта external AI/RAG adapter;
- добавление mock external RAG adapter для воспроизводимых локальных проверок;
- добавление минимального Dify adapter как первой практической реализации контракта;
- UI для выбора режима анализа и просмотра retrieved context;
- один smoke-сценарий MVP-1;
- тесты на архитектурные границы, воспроизводимость и отсутствие прямых вызовов внешних AI/RAG-компонентов из UI.

Не входит:

- собственный RAG;
- собственные embeddings;
- собственный rerank;
- собственная vector database;
- Jira, Confluence, GitLab, ALM, mail или chat integrations;
- workflow согласования;
- dashboard;
- PDF export;
- автоматическое создание задач;
- автоматическое изменение требований, документов, API или кода;
- автоматическое утверждение, отклонение или эскалация проектного запроса;
- превращение Dify в обязательную архитектурную основу проекта;
- детальный task breakdown, story-файлы или отдельные task-файлы на этом этапе.

## Общий порядок реализации

Реализация MVP-1 должна идти крупными проверяемыми этапами. Каждый этап в дальнейшем может быть разбит на одну или несколько маленьких task, но такое разбиение выполняется отдельно перед стартом конкретной реализации.

Рекомендуемый порядок:

1. Подготовка модели и контрактов для engine mode и retrieved context.
2. Расширение Markdown и JSON export.
3. Добавление neutral external RAG adapter contract.
4. Добавление mock external RAG adapter.
5. Добавление минимального Dify adapter.
6. UI для выбора режима и просмотра retrieved context.
7. Smoke-сценарий MVP-1.
8. Тесты на границы архитектуры и воспроизводимость.

Перечисленные этапы не являются готовыми implementation task. Перед началом каждого этапа выбирается одна маленькая task с отдельным проверяемым результатом, отдельно подтверждается ее scope и только после этого разрешается реализация.

Такой порядок сначала стабилизирует внутреннюю модель результата и export, затем добавляет внешний adapter boundary, после этого проверяет boundary на mock-реализации и только затем подключает минимальный Dify adapter.

## Этап 1. Модель и контракты engine mode и retrieved context

Цель: подготовить минимальное расширение существующей модели результата, чтобы приложение могло различать direct LLM и external AI/RAG режимы без привязки к Dify или конкретному внешнему response format.

Входит:

- `AnalysisMode` или аналогичное представление режимов:
  - `DirectLlm`;
  - `ExternalRag`;
- сведения о выбранном analysis engine;
- provider/adapter metadata, если применимо;
- retrieved context state:
  - available;
  - metadataOnly;
  - unavailable;
  - partial;
- минимальная модель retrieved context item или metadata:
  - source title;
  - source id или external reference;
  - fragment/chunk id, если доступен;
  - text или excerpt, если доступен;
  - url/reference, если доступен;
  - rank/score, если доступен;
  - provider/adapter;
  - признак полноты;
  - warning или limitation note;
- признак того, передавался ли manual context во внешний AI/RAG-контур;
- обратная совместимость с результатами MVP-0 без полной metadata MVP-1.

Не входит:

- Dify-specific поля в доменной модели как обязательная часть программы;
- собственный retrieval trace;
- история всех запусков анализа;
- новая audit-система;
- изменение документов MVP-0.

Проверяемый результат:

- приложение может сохранить и прочитать результат с mode и retrieved context metadata;
- legacy-результаты MVP-0 не считаются поврежденными;
- direct LLM результат не получает искусственный retrieved context;
- external AI/RAG результат может сохранить retrieved context, metadata или limitation.

## Этап 2. Расширение Markdown и JSON export

Цель: расширить экспорт до подключения реального внешнего adapter, чтобы формат результата MVP-1 был стабилен и пригоден для будущего сравнения режимов.

Входит:

- добавление в Markdown export сведений о:
  - режиме анализа;
  - analysis engine;
  - provider/adapter, если применялся;
  - использовании manual context;
  - retrieved context или metadata;
  - ограничении отсутствия или неполноты retrieved context;
  - предупреждениях engine/adapter;
- добавление в JSON export смысловых блоков:
  - `analysisMode`;
  - `analysisEngine`;
  - `provider`;
  - `adapter`;
  - `manualContextUsage`;
  - `retrievedContext`;
  - `retrievedContextState`;
  - `retrievedContextLimitations`;
  - `warnings`;
  - расширенный `exportMetadata`;
- поддержка legacy-результатов MVP-0 без полной metadata MVP-1;
- сохранение человекочитаемости Markdown export;
- сохранение пригодности JSON export для последующего исследовательского сравнения.

Не входит:

- PDF export;
- опубликованная отдельная JSON Schema;
- повторный вызов AI/RAG/LLM при экспорте;
- Dify-specific JSON как публичный формат программы.

Проверяемый результат:

- Markdown export показывает режим и основание результата;
- JSON export содержит стабильные смысловые блоки MVP-1;
- export строится только из сохраненных данных;
- export не требует сетевого доступа, Dify, DeepSeek, user secrets или внешних ключей.

## Этап 3. Neutral external RAG adapter contract

Цель: добавить нейтральную границу внешнего AI/RAG adapter, через которую `ExternalRagAnalysisEngine` сможет обращаться к внешнему контуру без утечки provider-specific деталей в UI и доменную модель.

Входит:

- application/infrastructure-level контракт external AI/RAG adapter;
- request model для передачи:
  - исходных данных анализа;
  - optional manual context при явном выборе пользователя;
  - correlation id;
  - ожидаемой структуры результата, совместимой с `ImpactMap`;
  - execution metadata без секретов;
- response model для возврата:
  - статуса выполнения;
  - structured result или данных для нормализации в `ImpactMap`;
  - provider/adapter metadata;
  - retrieved context state;
  - retrieved context items или metadata;
  - warnings;
  - error information без раскрытия секретов;
  - sanitized diagnostic snapshot или metadata response без секретов и чувствительного содержимого;
- `ExternalRagAnalysisEngine` как реализация `IAiAnalysisEngine`, использующая neutral adapter contract;
- engine selector или registry, выбирающий реализацию `IAiAnalysisEngine` по mode.

Не входит:

- реальный Dify вызов;
- собственный retrieval pipeline;
- embeddings, vector database, rerank;
- прямые вызовы adapter из Razor Pages;
- provider-specific payload в UI.

Проверяемый результат:

- `ExternalRagAnalysisEngine` доступен как отдельный engine mode;
- UI и page models не зависят от external adapter;
- direct LLM режим продолжает работать независимо от external AI/RAG режима;
- ошибки adapter возвращаются как controlled failed/partial state без раскрытия секретов.

## Этап 4. Mock external RAG adapter

Цель: получить воспроизводимую локальную реализацию external AI/RAG adapter без сети и секретов.

Входит:

- deterministic mock external RAG adapter;
- предсказуемый structured result, приводимый к `ImpactMap`;
- mock retrieved context items;
- варианты mock-ответов:
  - full retrieved context;
  - metadata only;
  - unavailable retrieved context;
  - partial result with warnings;
  - failed response;
- тестовые данные без конфиденциальной информации;
- возможность выполнить external AI/RAG сценарий локально без Dify.

Не входит:

- реальный внешний AI/RAG вызов;
- Dify configuration;
- network access;
- реальные корпоративные источники;
- собственный retrieval.

Проверяемый результат:

- external AI/RAG режим проходит локально через mock adapter;
- retrieved context отображается и экспортируется;
- неполный retrieved context сохраняется как limitation;
- failed/partial состояния проверяются тестами;
- сценарий не требует user secrets и сети.

## Этап 5. Минимальный Dify adapter

Цель: добавить первую практическую реализацию neutral external RAG adapter через Dify, не превращая Dify в публичную модель приложения.

Входит:

- реализация Dify adapter выполняется только в пределах утвержденных требований и технического проекта MVP-1 либо после отдельного явного решения пользователя о старте соответствующей task;
- минимальный Dify adapter за neutral adapter contract;
- configuration binding для endpoint/workflow/app id и секретов через окружение или согласованный механизм секретов;
- запрет хранения секретов в репозитории;
- mapping Dify response в neutral response;
- сохранение provider metadata, если она доступна и допустима;
- сохранение только sanitized diagnostic snapshot или metadata response без секретов, токенов, персональных данных и чувствительного содержимого; raw response не становится публичной моделью программы и не сохраняется по умолчанию;
- controlled handling ошибок, timeout, неполного response и отсутствия retrieved context;
- возможность отключить или не настраивать Dify без поломки direct LLM и mock external RAG режимов.

Не входит:

- зависимость архитектуры от Dify;
- Dify-specific поля как обязательные поля доменной модели или export;
- подключение реальных корпоративных источников напрямую из приложения;
- управление knowledge base Dify из приложения;
- создание embeddings, rerank или vector database в приложении.

Проверяемый результат:

- при наличии конфигурации Dify adapter может выполнить минимальный вызов external AI/RAG режима;
- при отсутствии конфигурации приложение сообщает о недоступности режима без поломки остальных сценариев;
- Dify-specific детали остаются внутри adapter;
- тесты по умолчанию не требуют Dify, сети и секретов;
- реальная проверка Dify выполняется только как явно включаемая manual/integration проверка.

## Этап 6. UI для выбора режима и просмотра retrieved context

Цель: расширить пользовательский сценарий MVP-0 так, чтобы пользователь мог выбрать режим анализа и увидеть основание external AI/RAG результата.

Входит:

- выбор режима перед запуском анализа:
  - direct LLM;
  - external AI/RAG;
- отображение доступности external AI/RAG режима;
- direct LLM остается доступным default/fallback режимом, если external AI/RAG недоступен;
- явное указание выбранного режима до запуска;
- отдельный выбор, передавать ли manual context во внешний AI/RAG-контур, если этот вариант поддержан реализацией;
- отображение mode, engine/provider/adapter metadata после анализа;
- отдельный блок retrieved context;
- отдельный блок retrieved context metadata, если полного текста нет;
- явное limitation-сообщение, если retrieved context unavailable или partial;
- отображение warnings adapter/engine;
- сохранение позиции проекта: AI/RAG/LLM результат является предварительным аналитическим материалом, а экспертное заключение остается за человеком.

Не входит:

- UI для управления Dify knowledge base;
- UI для настройки endpoint, workflow, API key;
- dashboard;
- workflow согласования;
- автоматическое создание задач;
- автоматическое изменение требований или кода.

Проверяемый результат:

- пользователь может выбрать режим и запустить анализ;
- direct LLM сценарий остается понятным и рабочим;
- external AI/RAG сценарий через mock adapter показывает retrieved context;
- отсутствие retrieved context видно как ограничение, а не как экспертное заключение;
- UI не вызывает Dify или retrieval API напрямую.

## Этап 7. Smoke-сценарий MVP-1

Цель: зафиксировать один полный проверяемый сценарий MVP-1 на подготовленных обезличенных данных.

Входит:

- создание или открытие анализа;
- заполнение исходных данных;
- добавление manual context;
- запуск direct LLM режима;
- запуск external AI/RAG режима через mock adapter или отдельно разрешенный Dify adapter;
- просмотр карты влияния;
- просмотр retrieved context или limitation;
- экспертная оценка;
- экспертное заключение;
- Markdown export;
- JSON export;
- проверка, что export содержит mode, engine/provider/adapter и retrieved context state.

Не входит:

- нагрузочное тестирование;
- production integration tests;
- Playwright/browser E2E framework без отдельного решения;
- реальные корпоративные данные;
- обязательный сетевой вызов Dify;
- DeepSeek как обязательная зависимость smoke-сценария.

Проверяемый результат:

- один полный MVP-1 сценарий проходит от входного проектного запроса до экспортов;
- сценарий воспроизводим без секретов и сети на mock adapter;
- при ручной Dify-проверке явно указано, что она optional и зависит от локальной конфигурации.

## Этап 8. Тесты на архитектурные границы и воспроизводимость

Цель: защитить ключевые архитектурные решения MVP-1 от случайного размывания.

Входит:

- тесты selector/registry для выбора `IAiAnalysisEngine` по mode;
- тесты, что direct LLM режим не требует retrieved context;
- тесты, что external AI/RAG результат сохраняет retrieved context state;
- тесты full/metadataOnly/unavailable/partial retrieved context;
- тесты Markdown export и JSON export для direct LLM, external AI/RAG и legacy MVP-0 результатов;
- тесты, что export не вызывает `IAiAnalysisEngine` и внешние provider-ы;
- тесты, что UI/page models зависят от application services, а не от Dify adapter;
- тесты, что mock external RAG adapter не требует network access и secrets;
- тесты, что отсутствие Dify-конфигурации не влияет на `dotnet test`;
- тесты обработки ошибок external adapter без раскрытия секретов;
- тесты защиты воспроизводимости результата после экспертной оценки или export, если эта защита уже реализована в MVP-0 и требует расширения на MVP-1 metadata.

Не входит:

- тестирование качества retrieval;
- сравнение моделей и метрик главы 4;
- нагрузочные тесты;
- реальные интеграционные тесты с корпоративными источниками;
- обязательные сетевые тесты Dify.

Проверяемый результат:

- `dotnet build` проходит;
- `dotnet test` проходит без внешних ключей и сети;
- архитектурные границы проверены тестами;
- воспроизводимый сценарий MVP-1 не зависит от Dify;
- Dify adapter остается optional integration layer.

## Commit и review правила

Для каждого будущего implementation step:

- перед началом нужна явная команда пользователя на конкретную task;
- scope task должен быть маленьким и проверяемым;
- diff должен соответствовать только scope task;
- до commit выполняется review;
- перед commit показывается `git diff --stat`;
- commit выполняется только после явной команды пользователя;
- push выполняется только после явной команды пользователя;
- результат build/test фиксируется в ответе;
- unrelated changes не смешиваются с реализацией task.

Если постановка task требует уточнения, уточнение сначала фиксируется как документационное изменение и проходит review отдельно от реализации кода.

## Риски и меры снижения

Риск: Dify-specific детали могут попасть в UI, доменную модель или export.

Снижение: держать Dify только за neutral adapter contract, не делать Dify response публичной моделью программы и не выводить Dify-specific response в UI, domain model или стабильный JSON export как обязательную структуру.

Риск: внешний AI/RAG-движок может не вернуть retrieved context.

Снижение: сохранять retrieved context state и явно показывать limitation в UI и export.

Риск: пользователь может воспринять retrieved context как окончательное основание управленческого решения.

Снижение: сохранять формулировки о предварительном характере AI/RAG/LLM результата и отдельном экспертном заключении.

Риск: external AI/RAG режим может сломать direct LLM сценарий.

Снижение: реализовывать режимы через selector/registry, проверять direct LLM отдельно, не делать external provider обязательной конфигурацией.

Риск: тесты начнут зависеть от сети и секретов.

Снижение: основной тестовый контур строить на mock external RAG adapter; Dify-проверку держать optional/manual.

## Следующий шаг

Следующий шаг после утверждения этого implementation plan - выбрать первую маленькую task MVP-1 и явно разрешить ее реализацию отдельной командой.

До такой команды код, тесты, project files и документы MVP-0 не создаются и не изменяются. Commit и push не выполняются.
