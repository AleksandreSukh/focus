# Focus — guidance for Claude Code

Mind-map/task app with three clients editing the same `FocusMaps/*.json` files: .NET 8 console app (`Focus/`), static PWA (`pwa/`), Android Kotlin/Compose client (`android/`).

**Before exploring the repo, read the matching project skill — they replace broad scans:**

- `focus-architecture` — repo map, which client owns what, shared concepts. Start here.
- `focus-map-schema` — map JSON schema, mutation/merge/CAS sync rules, llm-interop format.
- `focus-console-dev` — console app layout, command catalogs, dotnet build/test, E2E pipe harness.
- `focus-pwa-dev` — PWA modules, main.js patterns, offline queue, service-worker cache bump rule, `node --test` invocation.
- `focus-android-dev` — Android packages, ViewModel/sync coordinator, gradlew commands (needs JAVA_HOME to JDK 26).

Cross-client rule: domain/sync behavior changes usually must be mirrored in all three clients (PWA is the reference implementation; Android ports from it).

Never search generated dirs: `.artifacts/`, `.dotnet/`, `.vs/`, `**/bin/`, `**/obj/`, `pwa/dist/`, `Focus/Focus/publish/`, `Releases/`. Root `*.log` files are stale build output.

Quick commands:

```powershell
dotnet build Focus/Focus.sln
dotnet test Focus/Systems.Sanity.Focus.Tests/Systems.Sanity.Focus.Tests.csproj
node --test "pwa/src/**/*.unit.test.js"
cd android; $env:JAVA_HOME='C:\Program Files\Java\jdk-26.0.1'; .\gradlew.bat --no-problems-report test
```
