# PWA Todos MVP Scope and Constraints

## Purpose
This document defines a strict MVP boundary for the PWA Todos implementation so delivery remains focused and testable.

## 1) In-Scope Features (MVP)
Only the following todo operations are in scope:

- **List todos**
  - Display all existing todo items.
- **Add todo**
  - Create a new todo item.
- **Edit todo**
  - Update the text/content of an existing todo item.
- **Toggle todo completion**
  - Mark a todo as complete or incomplete.
- **Delete todo**
  - Remove an existing todo item.

### Acceptance Criteria (In Scope)
- The application exposes user flows for **list, add, edit, toggle, and delete** only.
- Each flow can be executed independently without requiring any non-MVP feature.
- No additional task metadata is required beyond what is needed to support CRUD + completion state.

## 2) Out-of-Scope Features (Explicitly Excluded)
The following are not part of v1 and must not be implemented for MVP:

- Labels/tags/categories
- Reminders/notifications
- Recurring tasks
- Multi-user real-time collaboration/sync

### Acceptance Criteria (Out of Scope)
- No UI, API, model fields, or storage schema are added for excluded features.
- Pull requests adding excluded features are deferred to post-v1 backlog.

## 3) Storage Layout Decision
For MVP, todo storage must use **one** of the following locations:

- `todos.json` at repository root, **or**
- `data/todos.json`

### Decision Rule
- Prefer **`data/todos.json`** if a `data/` directory already exists or data files are grouped there.
- Otherwise use **`todos.json` at repository root**.
- Do not support both paths simultaneously in v1.

### Acceptance Criteria (Storage)
- Exactly one storage path is active in code and documentation.
- All list/add/edit/toggle/delete operations read/write the same chosen file.
- Path choice is documented in README or implementation notes when coding begins.

## 4) Branch Strategy
MVP v1 development uses a single long-lived branch policy:

- **`main` only for v1**

### Acceptance Criteria (Branching)
- v1 commits are merged directly into `main`.
- No parallel long-lived release/feature branches are introduced for MVP scope management.
- Any experimental work is deferred until after v1 scope is complete.

## 5) Commit Message Format for CRUD Operations
Use a consistent commit message style for todo CRUD-related changes.

### Format
`<type>(todo): <operation> <short description>`

Where:
- `<type>` is one of: `feat`, `fix`, `refactor`, `test`, `docs`
- `<operation>` is one of: `list`, `add`, `edit`, `toggle`, `delete`

### Examples
- `feat(todo): add create-todo action and validation`
- `feat(todo): list render persisted todos on load`
- `feat(todo): edit update todo title in place`
- `feat(todo): toggle mark todo as complete/incomplete`
- `feat(todo): delete remove todo from storage`
- `fix(todo): edit preserve completion state after title change`
- `test(todo): toggle add coverage for complete/incomplete transitions`
- `docs(todo): delete document removal behavior`

### Acceptance Criteria (Commits)
- Commits that implement CRUD behavior follow the format above.
- Commit subject contains exactly one primary operation from the approved list.
- Non-CRUD work uses the same conventional format but omits operation terms where not applicable.

---

## Overall MVP Guardrails
Implementation remains constrained when all conditions below are true:

1. Feature set is limited to list/add/edit/toggle/delete.
2. Excluded features are absent from UI, API, and storage.
3. A single storage file path is selected and used consistently.
4. v1 work lands on `main` only.
5. CRUD commits follow the documented message format.

If any condition is violated, the change is **out of MVP scope** and must be deferred.
