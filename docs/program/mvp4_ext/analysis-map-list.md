# MVP-4 Extension: Compact Saved Analysis Map

## Статус

Документ является project summary для документационного прохода `mvp4_ext`.

Код не реализован. Документ не является командой начинать implementation task.

## Назначение

`mvp4_ext` уточняет одну небольшую UI-доработку для сохраненного `analysis`: заменить карточную `Карту сохраненного анализа` на компактный вертикальный список/оглавление строк.

Цель: сделать блок компактнее, привычнее и понятнее пользователю MVP без изменения предметной логики.

## Согласованное Решение

Будущая UI-only правка должна представить элементы карты анализа как строки:

- `Раздел`;
- короткий `Статус` рядом или вторым компактным столбцом;
- краткое описание;
- действия, связанные с этой строкой.

Это решение касается только представления overview-блока. Оно не добавляет новую доменную сущность и не меняет lifecycle сохраненного анализа.

## Связанные Spec Kit Artifacts

- [Feature spec](../../../specs/001-analysis-map-list/spec.md)
- [Requirements checklist](../../../specs/001-analysis-map-list/checklists/requirements.md)
- [Research](../../../specs/001-analysis-map-list/research.md)
- [Plan](../../../specs/001-analysis-map-list/plan.md)
- [Quickstart](../../../specs/001-analysis-map-list/quickstart.md)
- [Tasks](../../../specs/001-analysis-map-list/tasks.md)

## Project Boundary

В рамках этой доработки запрещено менять:

- PageModel;
- handlers;
- routes;
- данные;
- storage/API/DB;
- AI/RAG/LLM behavior;
- export behavior или export format;
- project files;
- тесты без отдельного решения;
- `AGENTS.md`, `.specify/templates`, `.agents`.

LLM/AI/RAG material остается preliminary analytical material. Expert evaluation и expert conclusion остаются человеческими действиями. Программа не принимает управленческие решения.

## SDD Gate

Этот documentation pass завершает только подготовку документов.

Следующий допустимый шаг: review документации или явная команда пользователя на конкретную future implementation task.

Без такой команды код не создается и не меняется.
