# AcPortal

Enterprise portal: modular ASP.NET Core (.NET 10) backend + Angular 19 (standalone components) frontend, with Dapr, Kafka, Keycloak, and a plugin system.

## Repo layout
- `Backend/` — ASP.NET Core Web API (`Backend.csproj`, entry point [Backend/Program.cs](Backend/Program.cs))
- `Backend.Tests/` — xUnit test project
- `Frontend/` — Angular 19 app ([Frontend/README.md](Frontend/README.md) has the default Angular CLI boilerplate; nothing project-specific there)

## Build / run / test
```bash
# Backend
dotnet build Backend/Backend.csproj
dotnet test Backend.Tests/Backend.Tests.csproj
dotnet run --project Backend                # needs Postgres/Kafka/Keycloak running (see docker-compose.yml)
dapr run -f Backend/dapr.yaml               # run with Dapr sidecar (appPort 5281, HTTP 3500, gRPC 50002)
docker compose -f Backend/docker-compose.yml up   # Kafka, Keycloak, Postgres(plugins-db), Redis, Gitea, Zipkin

# Frontend (from Frontend/)
npm start      # ng serve, http://localhost:4200
npm run build
npm test       # Karma + Jasmine
```
Backend listens on `http://localhost:5281`; frontend API base URL is `Frontend/src/environments/environment.ts` (`apiUrl`). CORS in [Program.cs](Backend/Program.cs) is hardcoded to `http://localhost:4200`.

## Backend architecture
Feature modules live under `Backend/Modules/{ModuleName}/` (Auth, Tasks, Projects, Events, Contracts, Chat, Notifications, AI, Teams, Tools, Git, Dashboard), each typically with `Controllers/`, `Services/`, `Models/`. There is no module auto-discovery — new services must be registered manually (mostly `AddScoped`) in [Backend/Program.cs](Backend/Program.cs).

Key patterns:
- **EF Core**: `AppDbContext` ([Backend/Data/AppDbContext.cs](Backend/Data/AppDbContext.cs)) is Postgres via Npgsql, registered Transient. Startup calls `EnsureCreated()`, *not* automatic migration application — when you change entities, add a migration (`dotnet ef migrations add ... --project Backend`) but know it won't run itself on startup.
- **Outbox + Kafka**: writes go through `OutboxMessage` rows; [Backend/Kafka/OutboxPublisherService.cs](Backend/Kafka/OutboxPublisherService.cs) polls every 5s and publishes to Kafka (retries up to 5x). [Backend/Kafka/KafkaConsumerService.cs](Backend/Kafka/KafkaConsumerService.cs) subscribes to per-project topics (`project.{projectId}`) plus `system.events`, refreshing every 30s. Dapr pub/sub config is separate: [Backend/dapr/components/pubsub.yaml](Backend/dapr/components/pubsub.yaml).
- **Workflow rules engine**: [Backend/workflow-config.json](Backend/workflow-config.json) maps event codes to actions (`CREATE_TASK`, `SUMMARIZE_CONTRACT`, etc.), loaded once by the singleton `WorkflowRulesService`. Actions are executed by `IActionHandler` implementations under `Backend/Modules/Events/Handlers/` — matching is case-insensitive on `EventCode`; when adding an action, add a handler whose `ActionType` matches the config string and register it in DI.
- **Auth**: JWT Bearer against Keycloak (realm imported from [Backend/acp-realm.json](Backend/acp-realm.json)); `KeycloakRoleTransformer` in `Modules/Auth` maps Keycloak roles to claims. SignalR hubs (`/hubs/notifications`, `/hubs/chat`) pull the bearer token from the query string since browsers can't set headers on WebSocket upgrades.
- **AI**: Semantic Kernel via pluggable `IKernelConfigurator` (Gemini/Google connector by default) — add new providers as configurators, not by hardcoding kernel setup.
- **Plugins**: plugin metadata lives in Postgres (`PluginDefinition`, `UserPlugin`); actual plugin code is hosted in an external Gitea instance (`docker-compose.yml`, port 3001) — see `Backend/plugins/*.json` for definitions.

## Testing
xUnit + FluentAssertions + Moq, EF Core InMemory for data-dependent tests. See [Backend.Tests/Workflow/WorkflowRulesServiceTests.cs](Backend.Tests/Workflow/WorkflowRulesServiceTests.cs) for the expected Arrange/Act/Assert style and how to fixture a temp `workflow-config.json`.

## Frontend architecture
Standalone Angular components (no NgModules), no state management library (RxJS + `BehaviorSubject` services) and no UI component library (hand-rolled SCSS, see [Frontend/src/styles.scss](Frontend/src/styles.scss) for CSS variables/theme). Feature pages are organized by role under `Frontend/src/app/pages/{admin,director,consultant,daf,project-manager,team-lead,super-admin,plugins}`, routed via `loadComponent()` in `Frontend/src/app/app.routes.ts`. Shared services/guards/interceptors live under `Frontend/src/app/core/`.

- Auth: `keycloak-angular`, with a custom `auth.interceptor.ts` (not Keycloak's built-in bearer interceptor) that refreshes the token and attaches `Authorization` — reuse this instead of adding ad-hoc token handling.
- Real-time: `@microsoft/signalr`, started from `AuthService` on login; matches the backend hubs above.
- i18n: `@ngx-translate/core`, JSON files under `assets/i18n/{lang}.json`.
