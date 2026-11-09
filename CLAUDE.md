# PersonaChat

An English-practice chat app (C# / .NET 10) where the user talks with fictional AI characters (personas). Portfolio project, built as a guided learning build.

## How we work

- The developer is a junior C#/.NET developer.
- Explanations to the developer: Turkish. Code, comments, commit messages, README: English.
- Do not add features that were not asked for. If something seems useful, suggest it, don't build it.
- Do not add NuGet packages without saying why. Prefer what .NET already provides.
- At the end of a stage: `dotnet build` (0 warnings, 0 errors), run the app, summarize, explain key concepts in Turkish, suggest a Conventional Commits message. Do not commit unless asked.

## Code style

- Simple, readable code, like a careful human wrote it. No clever tricks.
- Short clear names: `LoadAll`, `BuildPrompt`, `SendAsync`, `TrimHistory`.
- Small methods (under ~25 lines), one job each, early returns.
- Few, short comments, only for the "why". XML doc comments only on public interfaces.
- File-scoped namespaces, nullable enabled, `record` for data, primary constructors where natural, `async`/`await` all the way, pass `CancellationToken`.
- Dependency injection and the options pattern for config. No magic strings or numbers.
- `TreatWarningsAsErrors` is on.

## Security rules

- Secrets only in `dotnet user-secrets` or environment variables. `appsettings.json` is git-ignored; commit `appsettings.example.json`.
- Never log secrets or full message content at Information level.
- User input: trim, reject empty, enforce max length, strip control characters.
- User text always goes in the `user` role, never in the system prompt. Memory notes are untrusted: sanitize them and wrap them as data.
- Persona ids must match `^[a-z0-9-]{1,40}$`. Build paths with `Path.Combine` under the fixed data folder only.
- `System.Text.Json` only, no polymorphic handling of untrusted input. Validate loaded personas and fail with a clear error.
- One `HttpClient` via `IHttpClientFactory`, explicit timeout, check status codes. The Ollama URL stays `localhost` by default.
- Show friendly errors; details go to the log. No stack traces in the UI.
- EF Core only, no raw SQL string concatenation.
- Web: bind to localhost, keep `AllowedHosts` and the Content-Security-Policy, render model output with `textContent` only.
- Telegram (later): bot token from secrets, allow-list of chat IDs, per-user rate limit.
- Docker (later): non-root user, do not expose the Ollama port publicly.
- Run `dotnet list package --vulnerable` after adding packages.

## Product

- Personas live in `src/PersonaChat.Web/Data/personas.json`. They must stay in character.
- Modes: Chat (natural texting that grows from first meeting to friendship, see `Chat:IntroMessages` and `Chat:FriendMessages`) and Ask (structured expert answer with an example). User level is around B1.
- Simulations: 100+ workplace role-plays in `Data/simulations.json`. Email wheel: 100 scenarios in `Data/email-scenarios.json`.
- Reports: leaving a chat, finishing a simulation or submitting an email queues a background review by `ReportAnalyzer`. Quotes the model did not copy from the learner are dropped. Reports download as PDF (QuestPDF, Community license).
- Short-term memory: the last 20 messages. Long-term: every 20 messages, facts about the user are extracted as notes in the background.
- One persona must never see what the user told another persona.

## Architecture

```
src/PersonaChat.Core/    business logic: Models, Llm, Services, Storage (EF Core + SQLite)
src/PersonaChat.Web/     minimal API, background memory worker, wwwroot (vanilla HTML/CSS/JS)
tests/PersonaChat.Tests/ xUnit with a fake ILlmClient and in-memory SQLite
```

The web UI replaces the planned console app. The original commands map to UI controls: `/personas` is the card grid, `/chat` and `/ask` the mode switch, `/memory` and `/reset` buttons.

`ILlmClient` implementations, chosen by `Llm:Provider`: `Ollama` (default, any local model), `Claude`, `OpenAI` (Responses API, including Codex models), `Gemini`, `OpenAICompatible` (any chat/completions server: OpenRouter, Groq, LM Studio, LiteLLM). Cursor has no public model API, so it is not a provider.

New POST endpoints must take a JSON body so that browsers send a CORS preflight.

Packaging: `packaging/build.sh` makes double-click builds in `dist/`. A `desktop.json` next to the executable turns on desktop mode (`Desktop.cs`). Templates (`Data/*.json`) ship read-only inside the app; each person's database and optional `settings.json` live in their own app-data folder. Never ship a `.db` file or a developer's `appsettings.json`.

## Commands

```bash
cp src/PersonaChat.Web/appsettings.example.json src/PersonaChat.Web/appsettings.json
dotnet run --project src/PersonaChat.Web
dotnet test
packaging/build.sh
dotnet tool run dotnet-ef migrations add <Name> --project src/PersonaChat.Core --startup-project src/PersonaChat.Web --output-dir Storage/Migrations
dotnet user-secrets set "Llm:Claude:ApiKey" "<key>" --project src/PersonaChat.Web
dotnet user-secrets set "Llm:OpenAi:ApiKey" "<key>" --project src/PersonaChat.Web
dotnet user-secrets set "Llm:Gemini:ApiKey" "<key>" --project src/PersonaChat.Web
```

## Remaining stages

- Telegram bot (Telegram.Bot) with buttons for persona selection.
- Dockerfile + docker-compose (app + Ollama), README with features, architecture diagram, setup steps and a demo GIF placeholder.
