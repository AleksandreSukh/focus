export interface Todo {
  id: string;
  text: string;
  completed: boolean;
  deleted?: boolean;
  createdAt?: string;
  updatedAt: string;
}

export interface TodosDocument {
  version: number;
  items: Todo[];
}

export interface TodosSnapshot {
  document: TodosDocument;
  revision: string;
  loadedAt: number;
}

export interface SaveRequest {
  document: TodosDocument;
  expectedRevision: string;
  commitMessage: string;
}

export type SaveOutcome =
  | { ok: true; revision: string }
  | { ok: false; reason: 'conflict'; revision?: string }
  | { ok: false; reason: 'unknown'; message: string };

export interface TodoProvider {
  load(): Promise<TodosSnapshot>;
  save(request: SaveRequest): Promise<SaveOutcome>;
}

export type TodoAction = 'add' | 'edit' | 'toggle' | 'delete';

export interface TodoMutationMeta {
  action: TodoAction;
  commitMessage: string;
  todoId?: string;
}

export interface TodoConflictDetails {
  latest: TodosDocument;
  attempted: TodosDocument;
  merged: TodosDocument;
}

export type TodoErrorCode =
  | 'STALE_STATE'
  | 'VALIDATION_ERROR'
  | 'NOT_FOUND'
  | 'CONFLICT_UNRESOLVED'
  | 'PERSISTENCE_ERROR';

export interface TodoError {
  code: TodoErrorCode;
  message: string;
  retriable: boolean;
  meta?: TodoMutationMeta;
  conflict?: TodoConflictDetails;
  cause?: unknown;
}

export type TodoResult<T> =
  | {
      ok: true;
      value: T;
      revision: string;
      mergedAfterConflict: boolean;
    }
  | {
      ok: false;
      error: TodoError;
    };
