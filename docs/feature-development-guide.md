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

Use a command for state changes and a query for reads. Follow the local naming style: `<Feature>Command`/`<Feature>Query`, `<Feature>Result`, validator, handler, request, response, and endpoint.

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

For current visual direction, inspect modern files under `Component/ATS` immediately before implementation, especially:

- `UserManagement.razor` and `UserManagement.razor.css` for page/table/search/action styling;
- `AddUserComponent.razor`, `.razor.cs`, and `.razor.css` for modern dialogs/forms;
- the corresponding Add/Edit Client, Role, Module, and Package components for comparable workflows.

Follow their design language: ATS layout, navy/blue palette, Poppins headings, Inter body text, rounded cards/dialogs, restrained shadows, consistent buttons, accessible labels/focus states, responsive layout, and MudBlazor components where already established. Reuse shared generic components and `CrudPageBase`/shared loaders where suitable.

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
- [ ] UI covers loading, empty, validation, success, failure, and responsive states.
- [ ] Relevant tests and the solution build pass.
- [ ] API/UI contracts and gateway route were verified end to end.

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
