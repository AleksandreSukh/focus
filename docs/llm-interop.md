# Focus LLM Interop

Focus exposes LLM work through normal mindmap nodes plus sidecar job files. The app never stores model API keys and never calls an LLM directly.

## Prompt Nodes

- A prompt is a normal task node whose text starts with `@ai `.
- The prompt node remains the user-visible source of truth.
- When an answer is completed, the prompt task is marked done and the answer is appended as a child `TextBlockItem` node.

Answer child shape:

```json
{
  "nodeType": 2,
  "name": "multiline answer text",
  "taskState": 0,
  "metadata": {
    "source": "llm:Codex",
    "device": "Codex"
  }
}
```

## Job Files

Jobs live under:

```text
FocusMaps/_llm/jobs/<jobId>.json
```

Each job references a map and node:

```json
{
  "version": 1,
  "id": "job-id",
  "status": "pending",
  "mode": "subtree-links",
  "mapFilePath": "FocusMaps/Alpha.json",
  "nodeId": "node-guid",
  "prompt": "Summarize this branch",
  "createdAt": "2026-05-18T08:00:00Z",
  "updatedAt": "2026-05-18T08:00:00Z"
}
```

Supported statuses are `pending`, `claimed`, `completed`, and `failed`.

## CLI

Run with Node:

```powershell
& "C:\Program Files\nodejs\node.exe" tools/focus-interop jobs list --maps-dir C:\path\to\FocusMaps
```

Commands:

- `context --map <map.json> --node <nodeId> [--format json|markdown]`
- `jobs list [--status open|pending|claimed|completed|failed|all]`
- `jobs claim --agent <name> [--job <id>] [--format json|markdown]`
- `jobs complete --job <id> --answer-file <path> [--agent <name>]`
- `jobs fail --job <id> --message <text>`

The CLI resolves maps from `--maps-dir`, then `~/focus-config.json`, then `./FocusMaps`.

## Console Codex Commands

The console app can process the same `@ai` prompt nodes and sidecar jobs with the local Codex CLI.

- `ai <prompt>` creates a Todo child named `@ai <prompt>`, runs `codex exec`, appends the answer as a text block, marks the prompt Done, and saves the map.
- `ai` runs the current node when it is an open `@ai` prompt.
- `ai <child>` runs a visible child node using that node's text as the prompt. Open `@ai` children keep their prompt-node behavior; unmatched text is treated as a new prompt.
- `aijobs` lists pending and claimed sidecar jobs.
- `aijobs run [jobId]` claims and processes one pending job, defaulting to the oldest pending job.

Codex is invoked read-only by default with context on stdin and `--output-last-message`; Focus remains the only writer to map and job files.
