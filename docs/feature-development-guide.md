# Feature Development Guide

Use this playbook when adding an end-to-end feature to 1CibiPlatform. It follows the repository's current vertical-slice API design and the modern ATS Blazor UI.

## How to use this guide with an AI assistant

At the beginning of every feature discussion, say:

> Read `docs/feature-development-guide.md` and follow it. Use `BackendAPI/Modules/ATS/Features/UserManagement` as the API vertical-slice reference and `UI/FrontendWebassembly/Component/ATS` as the current UI/theme reference. Preserve existing conventions and implement the feature end to end, including tests.

Then provide the feature brief from the template near the end of this document. The assistant should inspect the named reference files before editing because the codebase remains the source of truth.

## Confirmed architecture

The normal request path is:

```text
Blazor .razor
  -> .razor.cs partial class
  -> typed UI service interface/implementation
  -> IHttpClientFactory named client (usually "API")
  -> gateway/API route
  -> Carter endpoint
  -> MediatR command/query handler
  -> domain/application service
  -> repository interface
  -> Scrutor cache decorator (when appropriate)
  -> repository implementation / EF Core
  -> PostgreSQL
```

Important existing references:

- API slice: `BackendAPI/Modules/ATS/Features/UserManagement/`
- Carter/MediatR/service registration: `BackendAPI/Modules/ATS/ServiceConfig/ATSServiceConfiguration.cs`
- Service layer: `BackendAPI/Modules/ATS/Services/`
- Repository contract and implementation: `BackendAPI/Modules/ATS/Data/Repository/`
- Scrutor/HybridCache decorator: `BackendAPI/Modules/ATS/Data/Cache/ATSCacheRepository.cs`
- ATS database context: `BackendAPI/Modules/ATS/Data/Context/ATSDBContext.cs`
- Backend DTOs and entities: `BackendAPI/Modules/ATS/DTO/` and `BackendAPI/Modules/ATS/Data/Entities/`
- Modern ATS UI: `UI/FrontendWebassembly/Component/ATS/`
- UI HTTP services: `UI/FrontendWebassembly/Services/ATS/`
- Named HttpClient and UI DI: `UI/FrontendWebassembly/ServiceConfig/FrontendServiceConfig.cs`
- Unit and integration tests: `Test/Test/BackendAPI/Modules/ATS.*Tests/`
- ATS integration-test infrastructure: `Test/Test/BackendAPI/Infrastructure/ATS.Infrastracture/`

### Auth business-logic organization

Auth follows the same business-area segregation used by the focused ATS repositories. Keep a feature's repository contract, EF implementation, cache behavior, and service pair in matching folders rather than adding unrelated code to one large file.

For the current Auth session lifecycle, security decisions, and review checklist, also read `docs/authentication-session-security.md` before changing login, logout, JWT claims, refresh rotation, cookies, password recovery, or OTP behavior.

### AI features

ATS AI features use Semantic Kernel plugins: a plain class whose methods carry `[KernelFunction]` and `[Description]`, registered on a cloned kernel with `AddFromObject` and invoked through `FunctionChoiceBehavior.Auto()`. This is intentionally different from the older `AIAgent` module, which discovers `*.skill.yaml` manifests through a reflection registry and requires the user to pick a skill. Prefer the ATS pattern for new work, and read `docs/ats-ai-assistant.md` before adding a function, changing the system prompt, or letting a model reach a write path.

```text
BackendAPI/Modules/Auth/
  Data/
    Repository/
      AuthRepository.cs                 shared partial class and DbContext
      Login/                            login persistence
      Registration/                     registration and OTP persistence
      PasswordRecovery/                 reset-token and password persistence
      RefreshTokens/                    refresh-token persistence
      Lockout/                          failed-attempt and locked-user persistence
      UserManagement/                   Auth user administration
      UserDirectory/                    ATS-facing Auth user lookups
      Applications/ AppSubRoles/ Roles/ SubMenus/
    Cache/
      AuthCacheRepository.cs            shared partial decorator and cache fields
      <matching business area>/         cached reads and invalidation
  Services/
    Login/ Registration/ PasswordRecovery/ RefreshTokens/
    Lockout/ UserManagement/
    Applications/ AppSubRoles/ Roles/ SubMenus/ Caching/
```

`AuthRepository` and `AuthCacheRepository` are partial types. Each business folder owns a focused repository contract plus the corresponding partial implementation files. `IAuthRepository` remains only as the aggregate compatibility/decorator contract, while business services depend on focused contracts such as `ILoginRepository`, `IRegistrationRepository`, and `IUserRepository`. For example, a login repository operation belongs under `Data/Repository/Login`, its decorator method under `Data/Cache/Login`, and `ILoginService`/`LoginService` under `Services/Login`. Registration and OTP work belongs under `Registration`; password reset work belongs under `PasswordRecovery`; token renewal belongs under `RefreshTokens`.

When adding an Auth feature:

1. Put the Carter/MediatR slice under `Features/<BusinessArea>` using the existing Auth feature convention.
2. Add repository methods only to the matching focused contract, such as `ILoginRepository`, `IRegistrationRepository`, or `IApplicationRepository`; do not grow the aggregate `IAuthRepository` directly.
3. Add EF code to `AuthRepository.<BusinessArea>.cs` and cache/decorator behavior to the matching `Data/Cache/<BusinessArea>` file.
4. Add or extend the service interface and implementation inside `Services/<BusinessArea>`.
5. Keep namespaces stable (`Auth.Data.Repository`, `Auth.Data.Cache`, `Auth.Services`, or the existing `Auth.Service`) unless a deliberate namespace migration is part of the change. Folder segregation alone must not change DI resolution or public behavior.
6. Run the Auth unit/integration tests and build the solution after moving or adding code.

## Step-by-step procedure

### 1. Define the slice before coding

Write down:

- user and business goal;
- module (ATS, Auth, PhilSys, CNX, and so on);
- command or query;
- request fields, validation, and response contract;
- HTTP method and route;
- authorization permission/module requirements;
- database changes and transaction boundary;
- cache reads, keys/tags, and invalidation rules;
- UI states: loading, empty, success, validation, forbidden, and failure;
- acceptance criteria and tests.

Prefer one use case per vertical-slice folder. Do not create a generic controller containing unrelated operations.

### 2. Decide the API folder and names

For ATS, use this shape:

```text
BackendAPI/Modules/ATS/Features/<Area>/
  Command/<FeatureName>/
    <FeatureName>Endpoint.cs
    <FeatureName>Handler.cs
  Query/<FeatureName>/
    <FeatureName>Endpoint.cs
    <FeatureName>Handler.cs
```

Every operation must keep its Carter endpoint and MediatR handler together in the same operation folder. Do not place one shared endpoint file beside multiple handlers. For example:

```text
BackendAPI/Modules/<Module>/Features/<Area>/
  Command/<OperationName>/
    <OperationName>Endpoint.cs
    <OperationName>Handler.cs
  Query/<OperationName>/
    <OperationName>Endpoint.cs
    <OperationName>Handler.cs
```

If an area has several commands or queries, each command/query gets its own folder and its own `*Endpoint.cs` plus `*Handler.cs`. Validators and operation-specific request/response records should live in that same folder unless the neighboring module has an established shared-contract convention. A single aggregate endpoint file containing unrelated operations is not compliant with the vertical-slice convention.

Use a command for state changes and a query for reads. Follow the local naming style: `<Feature>Command`/`<Feature>Query`, `<Feature>Result`, validator, handler, request, response, and endpoint.

### Readability and API error handling

All new feature code must be formatted for vertical readability. Do not compress namespaces, constructors, methods, object initializers, endpoint mappings, DTOs, or Razor markup into one line. Use one declaration/member per line and multiline parameter lists/object initializers when they exceed a short line.

Before considering a feature complete, review every new module file—not only the files touched last—and manually break up long endpoint lambdas, handler declarations, fluent entity configurations, DI registrations, DTO properties, and Razor markup. `dotnet format` is required, but it does not automatically wrap every long valid C# statement, so a manual readability pass is also mandatory. Code that builds but is horizontally compressed is not complete.

Recommended verification:

```powershell
dotnet format BackendAPI/Modules/<Module>/<Module>.csproj whitespace --no-restore
dotnet build BackendAPI/API/APIs/APIs.csproj --no-restore
```

### Module global usings

Backend module-wide imports belong in the module's `GlobalUsing.cs`. Before finishing a feature, review every source file under the module, move repeated or module-wide namespace imports to `GlobalUsing.cs`, and remove the corresponding file-level `using` directives. This includes the module's data, services, shared contracts, MediatR, Carter, EF Core, caching, validation, and common system namespaces.

Keep a file-level `using` only when the import is genuinely isolated to that file or when avoiding a namespace/type ambiguity. Do not scatter the same imports across endpoints, handlers, repositories, and services. After centralizing imports, format and build the module/API to detect missing or ambiguous namespaces.

UI API services must follow the Auth service error-handling pattern: check `IsSuccessStatusCode`, read `ApiErrorResponse.Detail` and trace information when available, log the detail, and surface it to the page/snackbar. If the response is not valid JSON, preserve and display the raw response body instead of replacing it with a generic error.

### Repository, service, and cache responsibilities

Keep persistence behind a repository interface under `Data/Repository`. The repository implementation should contain database access only (queries, inserts, and saving changes); business rules, validation, token generation, email orchestration, and status transitions belong in the application service. When read caching is needed, add a decorator under `Data/Cache` and register it with Scrutor:

Keep each business process in its own service folder. The interface and implementation for that process stay together:

```text
Services/
  <BusinessProcess>/
    I<Process>Service.cs
    <Process>Service.cs
```

For Employment Verification, email request creation and email verification belong under `Services/EmailVerification/`. Feature handlers and endpoints must still remain in their own vertical-slice operation folders.

```csharp
services.AddScoped<IFeatureRepository, FeatureRepository>();
services.Decorate<IFeatureRepository, FeatureCacheRepository>();
```

The cache decorator wraps repository reads with `HybridCache` and invalidates the relevant tag after mutations. Do not put business logic in the cache or repository layers. Follow ATS's `ATSRepository`/`ATSCacheRepository` pattern; explicitly document when a feature does not need caching.

### 3. Add or update contracts and persistence

Only add the pieces the use case needs:

1. Add request/response DTOs under the module's `DTO` folder (and matching UI DTOs later).
2. Add or update the entity under `Data/Entities`.
3. Add the `DbSet` and entity configuration in `ATSDBContext` when required.
4. Add repository methods to the module's focused repository contract. For Auth, use the matching business-area interface; for legacy ATS slices still using the aggregate repository, use `IATSRepository`.
5. Implement them in the matching focused/partial repository implementation; pass `CancellationToken` through async calls.
6. Keep query projection/filtering/paging in the data layer rather than loading entire tables.
7. Use `IUnitOfWork` or the established transaction pattern for multi-write operations.
8. Generate an EF migration only when the database model changed; place ATS migrations with the existing ATS migrations in the API project.

Before generating a migration, build the solution. A typical command is:

```powershell
dotnet ef migrations add <DescriptiveName> --context ATSDBContext --project BackendAPI/API/APIs/APIs.csproj --startup-project BackendAPI/API/APIs/APIs.csproj --output-dir Migrations/ATS
```

Review the generated migration and snapshot; never hand-wave destructive column/table changes.

### 4. Add caching deliberately

The repository uses Scrutor here:

```csharp
services.AddScoped<IATSRepository, ATSRepository>();
services.Decorate<IATSRepository, ATSCacheRepository>();
```

Therefore, add caching behavior to `ATSCacheRepository`, not to the Carter endpoint.

- Cache stable, frequently read queries with `HybridCache.GetOrCreateAsync`.
- Include every input that changes the result in the cache key (including page, size, search, tenant/user scope where applicable).
- Reuse a feature tag so related entries can be invalidated together.
- After a successful write, invalidate every affected query/tag.
- Do not cache authorization decisions or user-specific data under a shared key.
- Pass the cancellation token to cache and repository operations.
- If data is volatile or correctness cannot tolerate staleness, explicitly choose no cache.

Add cache tests or integration coverage when key construction/invalidation is meaningful to the feature.

### 5. Add the application service

1. Add the operation to an existing focused service interface in `Services/<BusinessArea>`, or create a focused interface and implementation if it represents a new responsibility. In Auth, keep login, registration, password recovery, refresh tokens, lockout, and administration services in their matching folders.
2. Put business rules, orchestration, and transaction decisions in the service.
3. Let the repository handle persistence/query mechanics.
4. Return typed results/DTOs and throw the established domain/not-found/validation exceptions so the global exception handler produces consistent errors.
5. Register a new service in the module's `Add<Module>Services` method. Existing services do not need duplicate registration.

### 6. Add the MediatR handler and validation

In `<FeatureName>Handler.cs`:

1. Define an `ICommand<T>` for a write or `IQuery<T>` for a read.
2. Define a result record.
3. Add an `AbstractValidator<TRequest>` for input rules. Validation is already connected through `ValidationBehavior<,>`.
4. Implement `ICommandHandler<,>` or `IQueryHandler<,>`.
5. Inject the service, call one clear service operation, pass the cancellation token, and map to the result.

MediatR handlers are discovered from the module assembly. Do not manually register each handler.

### 7. Add the Carter endpoint

In `<FeatureName>Endpoint.cs`:

1. Implement `ICarterModule` and `AddRoutes`.
2. Map the correct HTTP verb and route.
3. Bind route/query/body values to an endpoint request or directly create the command/query.
4. call `ISender.Send(request, cancellationToken)`.
5. map the handler result to the public response.
6. Add `.WithName`, `.WithTags`, `.Produces`, `.ProducesProblem`, `.WithSummary`, and `.WithDescription` consistently.
7. Add `.RequireAuthorization()` and any feature-specific policy/permission requirement.

Carter modules are assembly-discovered. For a brand-new module, wire its marker assembly into API configuration; for a new slice inside ATS, no individual Carter registration is normally needed.

Confirm the public route through the gateway. For example, the ATS Carter endpoint may map `getusers` while the UI calls `ats/getusers`; keep gateway route transforms consistent rather than silently changing one side.

### 7a. Register every route in the YARP gateway

The gateway is part of the endpoint contract. Register each new Carter route in the module's typed `Path/<Module>Paths.cs` implementation of `IReverseProxyModule`.

`GatewayServiceExtensions.AddModuleDiscoveryAndReverseProxy` scans the marker assemblies listed in that method, collects `GetRoutes()`/`GetClusters()` from every discovered module, and hands the result to `LoadFromMemory`. **The typed path module is the only source of routes at runtime.** For a brand-new module, also reference the module project from `YarpApiGateway.csproj` and add its marker assembly to that scan list, or none of its routes will exist.

Match the HTTP method and route template exactly (including route parameters), set the correct cluster, and use `PathSet` to forward the request to the backend Carter path. Keep GET and POST operations as separate entries when they share a path. Follow the existing ATS naming style (`/ats/getusers`), for example `/employmentverification/getrequests` forwarding to backend `api/employment-verification/requests`.

Use `PathPattern`, not `PathSet`, when the route has a `{parameter}` to substitute — `PathSet` forwards the literal text `{token}` to the backend. See the `preview/{token}` entry in `EmploymentVerificationPaths.cs`.

#### Do not add routes to the gateway appsettings files

`appsettings.{Development,UAT,Production}.json` still contain `ReverseProxy:Routes` entries for Auth, PhilSys, CNX, SSO, and token endpoints. **These are dead configuration.** The only code that reads that section is `OnePlatformConfigLoader.AddRoutesFromConfiguration`, which has no callers — `Program.cs` calls `AddGatewayServices()`, which loads routes exclusively from the typed modules. Almost every route in those files is also declared in a `*Paths.cs` module, which is why the gateway still works despite the section being ignored.

No ATS, Employment Verification, PlatformLogging, or AIAgent route appears in appsettings, and those modules route correctly. Adding entries there has no runtime effect and creates a second definition that will silently drift. Register the route in the typed module only.

Confirm this at startup: the gateway logs `[Gateway] Collected N routes and M clusters from modules` — that count comes entirely from the typed modules.

**Two appsettings routes have no typed-module equivalent and are therefore not served at all:** `auth/forgot-password/get-user-id` and `sso/login`. Anything depending on them is already broken; fix by adding them to `AuthPaths`/`SSOPath`, not to appsettings. (`sso/login/callback` does exist in `SSOPath.cs` — only the bare `sso/login` is missing.)

Verify a new route with the gateway's diagnostic endpoint, which returns the actual in-memory catalog:

```text
GET /__routes
```

#### Gateway naming convention (ATS-compatible)

Use the module name as one lowercase path segment and name operations with the same verb-first style used by ATS. Do not introduce a kebab-case public prefix for these gateway routes. Employment Verification is therefore exposed as:

```text
GET  /employmentverification/getatsinprogress
GET  /employmentverification/getrequests
POST /employmentverification/createrequest
POST /employmentverification/verify/{token}
POST /employmentverification/reject/{token}
```

The `PathSet` transform may target a different internal Carter route (for example, `/api/employment-verification/requests`); only the public `MatchPath` needs to follow the ATS-compatible convention. The UI `API` client must call the public gateway path, never the internal `PathSet` value.

### 8. Add backend tests

Add both levels when the feature touches business/data behavior:

- Unit tests beside the module's existing unit tests. Instantiate the handler/service with Moq fixtures and cover success, validation/edge behavior, not found, and propagated cancellation/errors.
- Integration tests using the module's `BaseIntegrationTest` and `IntegrationTestWebAppFactory`. ATS uses a PostgreSQL Testcontainer. Seed only the records needed, exercise the real service/data path (or HTTP endpoint when route/auth behavior is the subject), and assert persistence, filtering, pagination, and isolation.

Name tests as behavior: `Method_ShouldExpectedResult_WhenCondition`.

### 9. Add matching UI DTOs

Add frontend transport models under `UI/FrontendWebassembly/DTO/<Module>/`. Keep JSON names, nullability, collections, pagination, and response envelopes compatible with the API. Do not reuse EF entities in the UI.

### 10. Add the Blazor HTTP service

1. Add a method to `UI/FrontendWebassembly/Services/<Module>/Interface/I<Feature>Service.cs`.
2. Implement it under `Implementation/`.
3. Inject `IHttpClientFactory` and reuse the named client:

```csharp
public FeatureService(IHttpClientFactory httpClientFactory)
{
    _httpClient = httpClientFactory.CreateClient("API");
}
```

4. Use `GetAsync`, `PostAsJsonAsync`, `PatchAsJsonAsync`, or the appropriate method and pass cancellation tokens.
5. URL-encode user-provided query values.
6. Parse the standard `ApiErrorResponse` on failure; do not discard the trace ID.
7. Register a new UI service interface/implementation in `FrontendServiceConfig.AddFrontEndServices`.

The named `API` client already reuses the handler/pool through `IHttpClientFactory` and includes `CookieHandler` and `InterceptorHandler`. Do not instantiate `new HttpClient()` per operation. `RefreshAPI` intentionally excludes the interceptor to prevent refresh recursion; use it only for that purpose.

### 11. Build the Blazor UI using the ATS reference

For a page/component, normally keep three colocated files:

```text
<Feature>.razor       markup, bindings, injection, routing/authorization attributes
<Feature>.razor.cs    partial class, state, lifecycle, event handlers, async calls
<Feature>.razor.css   isolated feature styles
```

Use `public partial class <Feature>` in the `.razor.cs` file. Keep substantial C# out of `@code` blocks. Small markup-only components are acceptable, but new feature screens should follow the separated pattern requested for maintainability.

For current visual direction, inspect modern files under the feature folders of `Component/ATS` immediately before implementation, especially:

- `UserManagement/UserManagement.razor` and `UserManagement/UserManagement.razor.css` for page/table/search/action styling;
- `UserManagement/AddUserComponent.razor`, `.razor.cs`, and `.razor.css` for modern dialogs/forms;
- the corresponding Add/Edit Client, Role, Module, and Package components for comparable workflows.

Follow their design language: ATS layout, navy/blue palette, Poppins headings, Inter body text, rounded cards/dialogs, restrained shadows, consistent buttons, accessible labels/focus states, responsive layout, and MudBlazor components where already established. Reuse shared generic components and `CrudPageBase`/shared loaders where suitable.

#### Reuse existing styles; do not duplicate a design

A new screen that looks like an existing one must **reuse that screen's CSS, not copy it**. Before writing a `.razor.css`, open the closest existing feature and check `wwwroot/css/ats.css` for a shared class that already produces the look. Copying a block and renaming its classes (`.bulk-*` → `.ticketing-*`) is the failure mode this rule exists to prevent: the two copies drift, and a design fix then has to be made twice.

The order of preference is:

1. **Use the existing shared class as-is** (`.ats-management-page`, `.ats-management-card`, `.ats-segmented`, `.ats-segment-btn`, `.ats-console-empty-state`, and the `.ats-status-board-*` / `.ats-cell-*` / `.ats-status-pill` families).
2. **Generalize an existing rule** when a second screen needs the same treatment. Rename the feature-specific selector to a neutral shared one, move it to `wwwroot/css/ats.css`, and update the original screen to use it in the same change. The Bulk Uploads and Ticketing Status boards share `.ats-status-board-*` this way.
3. **Add scoped CSS only for what is genuinely unique** to the new screen, and say in a comment at the top of the file which shared rules it builds on and must not re-declare.

Rules of thumb:

- Never re-declare colour, spacing, radius, or typography that a shared class already sets. Palettes come from the `--management-*` custom properties on `.ats-management-page`; do not introduce a new hue for a new screen.
- A status list (Pending / Processing / Done / Error) must use the shared board classes so every such screen has identical chips, dots and pills.
- Styles that target shared `TableComponent` internals belong in the global sheet, not in scoped CSS, because the `::deep` boundary and the scope attribute make those overrides fragile to duplicate.
- Delete wrapper elements and `TableClass`/`ContainerClass` values that no rule matches; an unused hook reads as intentional styling that is not there.

If a screen's scoped stylesheet is more than roughly a screenful, that is a signal something in it should have been shared instead.

Also include:

- correct `@page`, `@layout`, `PageTitle`, `RequirePermission`, and `RequireATSModule` attributes;
- loading and disabled/submitting states;
- debounced server-side search/pagination for large datasets;
- cancellation-safe async work and no fire-and-forget calls;
- snackbar/dialog feedback consistent with ATS;
- accessible names, keyboard focus, validation messages, and responsive CSS;
- no secrets, base URLs, or environment-specific values in components.

### 12. Verify the complete feature

Run the smallest relevant tests first, then the full build:

```powershell
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~ATS"
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~Auth.UnitTests"
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~Auth.IntegrationTests"
dotnet build 1CibiPlatform.sln
```

Also manually verify:

1. authorized happy path;
2. forbidden/unauthenticated behavior;
3. server and client validation;
4. loading, empty, success, and error states;
5. cache miss, cache hit, and invalidation after mutation;
6. pagination/search and URL encoding;
7. database migration on a clean database when applicable;
8. responsive UI and keyboard use;
9. API response and UI DTO compatibility;
10. no unrelated files or user changes were overwritten.

### 12a. Register a new unified-platform application and submenu

When a feature is a standalone platform application (rather than a submenu owned by ATS/Auth), register it in the frontend permission catalogs as part of the same vertical slice:

1. Add the application to `UI/FrontendWebassembly/ShareData/Auth/ApplicationList.cs` using the next application ID. Keep the path, display name, and MudBlazor icon stable.
2. Add the feature submenu to `UI/FrontendWebassembly/ShareData/Auth/SubMenuList.cs` using the next submenu ID. The submenu path is the route segment used by the home application card.
3. Make the root/entry page use `[RequirePermission(applicationId, subMenuId)]`; this is the UI guard used by `SecurePageBase`.
4. Add matching backend application/submenu seed data and permission assignments. The numeric IDs must match the frontend catalogs (for example, Employment Verification uses application `8` and submenu `9`).
5. Build the home-card route from the registered application/submenu and verify that a user without the pair is redirected to `/access-denied`.
6. Do not register standalone features in `ATS/ModuleList.cs` or `Auth/SubMenuList.cs` unless the feature is actually owned by that application.

The registration is part of feature completion, not a follow-up UI-only task: API seed data, UI catalogs, route attributes, and access tests must agree on the same IDs.

### Module service configuration must mirror ATS

Every standalone backend module must keep its registration in `ServiceConfig/<Module>ServiceConfiguration.cs` and expose the same registration layers used by ATS:

```csharp
public static IServiceCollection Add<Module>MediaTR(
    this IServiceCollection services,
    Assembly assembly)
{
    services.AddMediatR(config =>
    {
        config.RegisterServicesFromAssembly(assembly);
        config.AddOpenBehavior(typeof(ValidationBehavior<,>));
        config.AddOpenBehavior(typeof(LoggingBehavior<,>));
    });

    services.AddValidatorsFromAssembly(assembly);
    services.AddExceptionHandler<CustomExceptionHandler>();

    return services;
}
```

The module service-registration method must also register repositories, apply Scrutor decorators, and register application services. The API composition root must call the module's `MediaTR`, infrastructure, and services methods. Do not reduce a module's MediatR setup to handler discovery only.

Every command that accepts user/API input must declare an `AbstractValidator<TCommand>` immediately after the command record in the same handler file, following ATS. Validate required nested request objects, required database columns, maximum lengths, email formats, identifiers, and cross-field rules before the handler calls the service. The validator rules must agree with EF Core nullability and length constraints so invalid input returns a `400 ValidationException` instead of reaching PostgreSQL as a `DbUpdateException`.

### Database migration and initialization flow

Database creation/migration belongs in the module's `Data/Extensions` folder, not in a hosted seed service. Add an extension such as `EmploymentVerificationDatabaseExtensions.EmploymentVerificationInitializeDatabaseAsync(WebApplication)` that creates a scope, resolves the module `DbContext`, and calls `Database.MigrateAsync()`:

```text
BackendAPI/Modules/<Module>/Data/Extensions/
  <Module>DatabaseExtensions.cs
```

Register that initializer in `BackendAPI/API/APIs/Data/Extensions/DatabaseExtensions.cs` alongside ATS, Auth, PhilSys, and the other module initializers. Keep migrations in the API migration assembly configured by the module context (for example, `BackendAPI/API/APIs/Migrations/EmploymentVerification/`) and do not add an `IHostedService` solely to create tables or seed mock feature records. If real reference data is required, initialize it explicitly in the module extension following the ATS initializer flow.

## Definition of done checklist

- [ ] Acceptance criteria are explicit.
- [ ] Slice is under the correct module/area and Command or Query folder.
- [ ] DTO/entity/context/migration changes are complete, if required.
- [ ] Repository interface and implementation are complete.
- [ ] Cache policy and invalidation are implemented or explicitly not needed.
- [ ] Service interface/implementation and DI are complete.
- [ ] Command/query, validator, handler, and Carter endpoint are complete.
- [ ] Route metadata, cancellation, errors, and authorization are complete.
- [ ] Backend unit and integration tests pass.
- [ ] UI DTO and IHttpClientFactory-backed service are complete and registered.
- [ ] `.razor`, `.razor.cs`, and `.razor.css` follow the modern ATS reference.
- [ ] Shared CSS was reused or generalized rather than copied; scoped CSS covers only what is unique to the screen.
- [ ] UI covers loading, empty, validation, success, failure, and responsive states.
- [ ] Relevant tests and the solution build pass.
- [ ] API/UI contracts and gateway route were verified end to end.
- [ ] Every endpoint is registered in the module's typed `Path/<Module>Paths.cs` and appears in `GET /__routes`.

## Feature brief template

Copy this into a new Codex/Claude discussion:

```markdown
Read `docs/feature-development-guide.md` first and follow it. Implement this feature end to end. Use ATS components as the latest UI/theme reference. Inspect existing neighboring code before editing, preserve unrelated changes, and run relevant tests plus the solution build.

Feature name:
Module and area:
User/persona:
Business goal:

User flow:
1.
2.
3.

API operation (command/query):
Preferred HTTP method and route (if known):
Request fields:
Response fields:
Validation/business rules:
Authorization/permission/module IDs:

Database/entity changes:
Caching expectations and invalidation:

UI page/route or parent component:
UI controls and actions:
Loading/empty/success/error behavior:
Responsive/accessibility requirements:

Acceptance criteria:
- Given ..., when ..., then ...
- Given ..., when ..., then ...

Out of scope:
Reference feature(s) most similar to this one:
```

If some fields are unknown, say `please infer from the closest existing ATS feature and state assumptions`. For authorization IDs, destructive schema changes, or ambiguous business rules, require the assistant to confirm rather than invent them.
