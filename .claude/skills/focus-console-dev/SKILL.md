---
name: focus-console-dev
description: Console (.NET 8) Focus app development — project layout, command/page system, how to add edit/home commands, build & test invocations, unit/E2E test harness. Use for any change under Focus/ (the C# console client).
---

# Focus console app development

Solution: `Focus/Focus.sln`. Projects:
- `Focus/Focus` (`Systems.Sanity.Focus.csproj`) — the app.
- `Focus/Systems.Sanity.Focus.Inrastructure` (sic, folder typo; assembly is `...Infrastructure`) — console rendering (`ColorfulConsole`), custom ReadLine (history/autocomplete), Git sync (`FileSynchronization/Git/GitHelper.cs`), input translation.
- `Focus/Systems.Sanity.Focus.Tests` — xUnit unit tests (in-process, `TestAppConsole`, `TestWorkspace`).
- `Focus/Systems.Sanity.Focus.E2E.Tests` — xUnit E2E: spawns real app process with `--test-host <pipeName>` (named pipe), drives it via `FocusTestHostClient`/`FocusScenario`, sandbox git remote via `GitSandbox`. App-side counterpart: `Application/Console/TestHostConsoleSession.cs`.
- `Focus/Focus.Tests` — empty placeholder, ignore.

## Build / test

```powershell
dotnet build Focus/Focus.sln
dotnet test Focus/Systems.Sanity.Focus.Tests/Systems.Sanity.Focus.Tests.csproj
dotnet test Focus/Systems.Sanity.Focus.E2E.Tests/Systems.Sanity.Focus.E2E.Tests.csproj
dotnet run --project Focus/Focus/Systems.Sanity.Focus.csproj
```

Build emits known warnings (nullable, CA1416, Microsoft.Alm.Authentication NU1900-ish) — not regressions. Filter tests with `--filter FullyQualifiedName~<Name>`.

## App layout (`Focus/Focus/`)

- `Program.cs` — entry: parses `AppRuntimeOptions` (`--config <path>`, `--test-host <pipe>`), Velopack startup, camelCase JsonConvert defaults, loads `~/focus-config.json`, shows `HomePage`.
- `Domain/` — `MindMap` (current-node cursor + mutations), `Node`, `NodeMetadata`, `TaskState`, `MapsStorage` (file list/recent + `Sync(commitMessage)`), `MapAttachmentStore`, `MapConflictResolver`.
- `DomainServices/` — printers/exporters (`NodePrinter`, `HtmlPrinter`, `MarkdownPrinter`, `PlainTextPrinter`), search (`MindMapSearchService`, `MapsSearchService`), `MapNormalizer`, `TaskQueryService`.
- `Application/` — workflows: `HomeWorkflow`, `EditWorkflow`, `CreateMapWorkflow`; `FocusAppContext` (repository, storage, link index); `ClipboardCaptureService`, `VoiceRecordingService`; `Llm/` (LlmAgentClient → codex exec, LlmJobStore, LlmContextBuilder).
- `Pages/` — `HomePage`, `Pages/Edit/EditMapPage` + dialogs; base classes `Pages/Shared/Page`, `PageWithOptions`.

## Command system (the main extension point)

Two parallel catalogs, same pattern:
- Edit mode: `Application/EditCommands/` — `EditCommandId` (enum), `EditCommandCatalog.CreateDefaultDescriptors()` (key, help group, help text, parameter-suggestion kind), `EditCommandHandlerRegistry.CreateDefault()` maps ids → feature handlers (`EditNodeCommandHandler`, `EditTaskCommandHandler`, `EditNavigationCommandHandler`, `EditLinkCommandHandler`, `EditLlmCommandHandler`, `EditCaptureCommandHandler`, `EditExportCommandHandler`, `EditSearchCommandHandler`, `EditAttachmentMetadataCommandHandler`, `EditSystemCommandHandler`). Unmatched input → `EditFallbackCommandProcessor` (child navigation by number/text key).
- Home mode: `Application/HomeCommands/` — same shape (`HomeCommandCatalog`, `HomeFileCommandHandler`, `HomeFindCommandHandler`, `HomeSystemCommandHandler`, fallback opens file).

**To add an edit command**: add `EditCommandId` member → descriptor in `EditCommandCatalog.CreateDefaultDescriptors()` → handle in the right feature handler (or new handler registered in `EditCommandHandlerRegistry`) → unit tests in `EditWorkflowTests`/`EditCommandCatalogTests` (+ E2E scenario in `ConsoleAppE2ETests` if user-visible flow).

Edit command keys: cd/up/ls; add/addblock/idea/edit/del/clearideas/slice/min/max/star/unstar/capture/voice; todo/doing/done/notask/toggle/hidedone/showdone/deldone/tasks; linkfrom/linkto/openlink/backlinks; ai/aijobs; search/export/meta/attachments; exit.

Commands go through `ToCommandKey()` (`CommandLanguageExtensions`) which applies keyboard-translation dictionaries from config.

## Persistence & sync

Every successful edit calls `EditWorkflow.Save(commitMessage)` → `MapRepository.SaveMap` + `MapsStorage.Sync(commitMessage)` → `FileSynchronizationHandlerGit`/`GitHelper` (Windows + configured `gitRepository` only; otherwise empty handler). Merge conflicts on pull go through `MapConflictResolver` auto-resolution; unresolved → `UnresolvedGitMergeException` with recovery flow (`MergeRecoveryResult`, tests in `GitHelperMergeRecoveryTests`).

## E2E test pattern

```csharp
await using var scenario = await FocusScenario.StartAsync(); // creates temp workspace+config, starts app
await scenario.SendInputAsync("add");                        // drive via pipe
await scenario.WaitForScreenAsync(s => s.Contains("..."));   // assert on rendered screen text
```
Env overrides for tests: `FOCUS_TEST_CLIPBOARD_MODE/_TEXT_BASE64/_IMAGE_BASE64/_ERROR`, `FOCUS_TEST_OPENED_FILES_LOG`, `FOCUS_TEST_EMIT_TITLES` (see `FocusAppProcessHarness.cs`, `TestHostAppOverrides.cs`).
