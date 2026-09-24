# 1CibiPlatform

[![.NET](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com/)
[![Blazor](https://img.shields.io/badge/Blazor-WebAssembly-green)](https://blazor.net/)
[![MudBlazor](https://img.shields.io/badge/MudBlazor-UI_Components-darkblue)](https://mudblazor.com/)
[![YARP](https://img.shields.io/badge/YARP-API_Gateway-orange)](https://microsoft.github.io/reverse-proxy/)
[![Carter](https://img.shields.io/badge/Carter-Minimal_APIs-lightblue)](https://github.com/CarterCommunity/Carter)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16_+_pgvector-blue)](https://www.postgresql.org/)
[![Docker](https://img.shields.io/badge/Docker-Containerized-blue)](https://www.docker.com/)

**1CibiPlatform** is a hybrid platform for both client-facing and internal applications, built as a **modular monolith** on .NET 10.0. It hosts CIBI's background-checking operations — applicant tracking and order orchestration, identity verification, employment verification, and platform authentication — behind a single API host, a reverse-proxy gateway, and a Blazor WebAssembly client.

## 📖 Documentation

Two documents sit at the `docs/` root and are required reading:

- **[Feature development guide](docs/feature-development-guide.md)** — the authoritative playbook: the confirmed request path, vertical-slice conventions, caching, tests, Blazor UI patterns, the documentation convention described below, and a reusable AI feature-brief template.
- **[UI theming and responsiveness](docs/ui-theming-and-responsiveness.md)** — design tokens, dark mode, breakpoints. Read before adding stylesheets or screens.

Everything else is grouped by kind:

| Folder | Contents |
|---|---|
| `docs/features/<feature>/` | One folder per feature, holding a **pair**: `<feature>.md` (what it does and why) and `<feature>_code_explanation.md` (the real call chain, file by file, for someone about to change the code) |
| `docs/architecture/` | [Architecture overview](docs/architecture/architecture-diagram.md) · [infrastructure and server specifications](docs/architecture/infrastructure-and-server-specifications.md) |
| `docs/operations/` | [.NET 9 → 10 upgrade](docs/operations/upgrade-net9-to-net10.md) · [package ID migration runbook](docs/operations/package-id-migration.md) · [OMS TLS handshake fix](docs/operations/oms-tls-handshake-fix.md) · [Serilog log viewer plan](docs/operations/serilog-postgresql-log-viewer-plan.md) |
| `docs/reviews/` | [ATS + OnePlatform code review](docs/reviews/ats-oneplatform-code-review.md) · [what was fixed](docs/reviews/ats-oneplatform-code-review-fixes.md) · [fix details, item by item](docs/reviews/ats-oneplatform-fix-details.md) |

Documented features:

| Feature | High level | Code walkthrough |
|---|---|---|
| ATS sender email accounts | [ats-email-accounts.md](docs/features/ats-email-accounts/ats-email-accounts.md) | [code explanation](docs/features/ats-email-accounts/ats-email-accounts_code_explanation.md) |
| ATS email delivery | [ats-email-delivery.md](docs/features/ats-email-delivery/ats-email-delivery.md) | — |
| ATS application form email copy | [ats-application-form-email-copy.md](docs/features/ats-application-form-email-copy/ats-application-form-email-copy.md) | [code explanation](docs/features/ats-application-form-email-copy/ats-application-form-email-copy_code_explanation.md) |
| ATS in-app notifications | [ats-notifications.md](docs/features/ats-notifications/ats-notifications.md) | — |
| ATS bulk requeue | [ats-bulk-requeue.md](docs/features/ats-bulk-requeue/ats-bulk-requeue.md) | — |
| ATS AI assistant | [ats-ai-assistant.md](docs/features/ats-ai-assistant/ats-ai-assistant.md) | — |
| ATS AI assistant — voice input | [ats-ai-assistant-voice-input.md](docs/features/ats-ai-assistant-voice-input/ats-ai-assistant-voice-input.md) | — |
| ATS audit trail | [ats-audit-trail.md](docs/features/ats-audit-trail/ats-audit-trail.md) | — |
| ATS public API | [ats-public-api.md](docs/features/ats-public-api/ats-public-api.md) | — |
| ATS order status history | [ats-order-status-history.md](docs/features/ats-order-status-history/ats-order-status-history.md) | — |
| OMS auto-ticketing | [oms-auto-ticketing.md](docs/features/oms-auto-ticketing/oms-auto-ticketing.md) | — |
| Employment verification tracking | [employment-verification-request-tracking.md](docs/features/transaction-runner/employment-verification-request-tracking/employment-verification-request-tracking.md) | [code explanation](docs/features/transaction-runner/employment-verification-request-tracking/employment-verification-request-tracking_code_explanation.md) |
| Employment verification contact directory | [employment-verification-contact-directory.md](docs/features/employment-verification-contact-directory/employment-verification-contact-directory.md) | [code explanation](docs/features/employment-verification-contact-directory/employment-verification-contact-directory_code_explanation.md) |
| Employment verification auto-send | [employment-verification-auto-send.md](docs/features/employment-verification-auto-send/employment-verification-auto-send.md) | [code explanation](docs/features/employment-verification-auto-send/employment-verification-auto-send_code_explanation.md) |
| Authentication session security | [authentication-session-security.md](docs/features/authentication-session-security/authentication-session-security.md) | — |
| Transaction runner | [transaction-runner.md](docs/features/transaction-runner/transaction-runner.md) | — |

The `—` entries are companions still to be written; the convention and checklist live in [steps 12–13 of the feature guide](docs/feature-development-guide.md#12-document-the-change).

### Starting a feature with Codex or Claude

Copy this instruction at the beginning of every new feature discussion:

> Read `docs/feature-development-guide.md` first and follow it. Implement this feature end to end. Use `BackendAPI/Modules/ATS/Features/Web/UserManagement` as the API vertical-slice reference and `UI/FrontendWebassembly/Component/ATS` as the latest UI/theme reference. Preserve existing conventions and unrelated changes, include relevant backend and UI tests, and run the relevant tests plus the solution build. If information is missing, infer it from the closest existing ATS feature and state your assumptions. Ask before making destructive schema changes or inventing authorization requirements. Finish by writing both documents in `docs/features/<area>-<feature>/`, and if you refactored an already-documented feature, update its two documents in the same commit.

Then describe the feature using the **[Feature brief template](docs/feature-development-guide.md#feature-brief-template)**.

## 🏗️ Architecture Overview

A **modular monolith** using **Vertical Slice Architecture** with **Domain-Driven Design** principles. Business capabilities live in separate module class libraries, but all modules are composed into **one deployable API process** and share **one PostgreSQL database** (per-module schemas). Modules are isolated by code structure and convention, not by process or network.

The solution contains eleven projects, of which **three are deployables**:

| Deployable | Project | Role |
|---|---|---|
| 🌐 Frontend | `UI/FrontendWebassembly` | Standalone Blazor WebAssembly SPA with MudBlazor |
| 🚪 Gateway | `ApiGateways/YarpApiGateway` | YARP reverse proxy — routing, CORS, rate limiting, security headers, TLS |
| 🔧 Backend | `BackendAPI/API/APIs` | Single API host composing nine feature modules |

Supporting projects: `BackendAPI/BuildingBlocks/BuildingBlocks` (shared kernel), nine module libraries under `BackendAPI/Modules/`, `Test/Test` (xUnit), and `Tools/PdfPreview` (manual PDF dev harness).

### Request path

```text
Browser (Blazor WebAssembly + MudBlazor)
  Pages/*.razor → Component/ATS/* → Services/ATS/*   (typed UI service)
      │  named HttpClient "API" + CookieHandler + InterceptorHandler
      ▼
YARP Gateway
  routes discovered at startup from each module's IReverseProxyModule
  CORS · rate limiting · security headers · Prometheus · /health
      ▼
API host (one process, nine modules)
  Carter endpoint (minimal API)
      → ISender / MediatR
      → ValidationBehavior → LoggingBehavior → (module behaviors, e.g. AtsAuditBehavior)
      → Handler → domain service → repository contract
      → Scrutor cache decorator (HybridCache)
      → EF Core DbContext
      ▼
PostgreSQL 16 + pgvector
```

The OMS module is the one exception: it calls the legacy Order Management System on **SQL Server via stored procedures** rather than EF Core.

### Composition

`BackendAPI/API/APIs/ServiceConfig/ServiceConfiguration.cs` wires every module through nine marker assemblies (`typeof(ATSMarker).Assembly`, …) in four extension groups:

```text
AddModuleCarter          → endpoint discovery across all modules
AddModuleMediaTR         → handlers + MediatR pipeline behaviors
AddModuleServices        → domain services, HttpClients, caching decorators
AddModuleInfrastructure  → DbContexts, Quartz, external stores
```

**Adding a module means adding it to those four lists** and giving it a marker type plus an `IReverseProxyModule` route class.

### Gateway routing is code-first

The gateway does **not** read routes from `appsettings.json`. `ApiGateways/YarpApiGateway/Extensions/GatewayServiceExtensions.cs` scans the module assemblies for `IReverseProxyModule` implementations and loads the collected routes/clusters into memory, so **each module owns its own routing** in `Path/*Paths.cs`. Clusters:

| Cluster | Destination | Purpose |
|---|---|---|
| `onePlatformApi` | `http://apis:8080` | All backend module routes |
| `BlazorUI` | `http://frontendwebassembly:8080` | SPA catch-all (`/{**catchall}`) |
| `CTVIIntertalAPI` | `https://dev.gds.ctvi.com.ph` | External CTVI product API |

Per-route rate limits come from each route's `RateLimitPolicy` metadata: `LoginPolicy` (5/10s), `DefaultStrict` (20/min, public API machine callers), `AnonymousApplicationForm` (30/min per client IP, candidate-facing forms). `/metrics` and `/health` are CIDR-gated and return **404 rather than 403** to non-allowlisted callers.

## 📁 Repository Layout

```text
1CibiPlatform/
├── UI/
│   └── FrontendWebassembly/          # Blazor WebAssembly SPA (MudBlazor)
│       ├── Component/                # ATS, Generic, PhilSys, Profile, Special, UserManagement
│       ├── Layout/                   # MainLayout, ATSLayout, ConsoleLayout, SSOLayout, ...
│       ├── Pages/                    # Routable pages: ATS, Auth, AIAgentChat, Philsys, SSO, ...
│       ├── Services/                 # Typed API clients per business area
│       ├── SharedService/            # LocalStorage, Theme, validation, display mappers
│       ├── ShareData/                # Static reference lists
│       ├── ServiceConfig/            # Named HttpClients + DI registration
│       └── wwwroot/                  # index.html, appsettings.{env}.json, css/ tokens
├── ApiGateways/
│   └── YarpApiGateway/               # YARP proxy, module route discovery, monitoring gate
├── BackendAPI/
│   ├── API/
│   │   └── APIs/                     # Main API host
│   │       ├── ServiceConfig/        # Composition root
│   │       └── Migrations/<Module>/  # All EF migrations, centralized
│   ├── Modules/                      # Independent feature modules (see table below)
│   │   └── <Module>/
│   │       ├── Features/             # Vertical slices (Endpoint + Handler pairs)
│   │       ├── Services/             # Domain/application services
│   │       ├── Data/                 # Context, Entities, Repository, Cache decorator
│   │       ├── Path/                 # IReverseProxyModule gateway routes
│   │       ├── ServiceConfig/        # Module DI registration
│   │       └── <Module>Marker.cs     # Assembly marker for host + gateway discovery
│   └── BuildingBlocks/BuildingBlocks/  # Shared kernel
├── Test/Test/                        # xUnit unit + integration tests
├── Tools/PdfPreview/                 # Manual consent-form PDF preview harness
├── docker/                           # Postgres image + pgvector init scripts
├── docs/                             # Design and development guides
└── docker-compose.yml
```

## 📦 Modules

| Module | Capability | Shape |
|---|---|---|
| **ATS** | The product core: applicant tracking and background-check order orchestration. Candidates receive emailed application forms, orders ("endorsements") are placed against client packages, reports return, plus disputes, bulk CSV uploads, dashboards, notifications, audit trail, and in-module RBAC | 31 web slice areas, 8 token-authenticated `PublicApi` slices, 3 audit-trail queries; 26 entities; Quartz jobs; SignalR hub |
| **Auth** | Platform identity: login (web + API), registration, OTP, refresh tokens, password recovery, session liveness, locked-user management, and platform admin (applications, roles, submenus, app-sub-roles) | ~25 slices; own DbContext, 9 entities |
| **PhilSys** | Philippine national ID verification via government eVerify — PCN, face liveness, partner/internal system queries | 8 slices; named `HttpClient("PhilSys")`; Quartz cleanup job |
| **AIAgent** | Policy RAG assistant: ingest HR/company policy documents and answer questions over them. Semantic Kernel chat + embeddings stored in **pgvector**, YAML-declared skill registry, SignalR hub | 2 slices; plugin/skill architecture |
| **EmploymentVerification** | Employer-to-employer employment verification. A Quartz job picks up each employer slot of a submitted application form and emails a token link, which the contacted party verifies or rejects. Sends only to mailboxes listed in its own vetted contact directory | 10 slices; 2 entities; Quartz job; reuses ATS email service |
| **PlatformLogging** | Centralized log persistence and query API. Custom Serilog `PostgreSqlBatchingSink` batch-writes Warning+ events; hosted retention service prunes them | 2 slices |
| **OMS** | Bridge to the legacy Order Management System — raises tickets for failed ATS email invitations | 1 slice; **no EF Core**, SQL Server stored procedures by design |
| **SSO** | SAML2 single sign-on (Sustainsys) with callback at `/sso/login/callback` | 3 slices; no database |
| **CNX** | Concentrix integration — proxies `GetTalkpushCandidate` to the Talkpush API | 1 slice; no database |
| **CBBlue** | ⚠️ Reserved placeholder. Contains no features and is **not composed into the API host** | — |

### Inside a module: ATS feature areas

ATS splits its slices into three areas under `Features/`:

```text
Features/
├── Web/         # Browser-facing slices used by the Blazor UI (31 areas)
├── PublicApi/   # Token-authenticated client integrations
│                # CreateEndorsement, CreateBulkEndorsement, GetOrders, GetOrder,
│                # GetPackages, GetBulkUploadStatus, DownloadReport, WithdrawOrder
└── AuditTrail/  # GetAuditTrail, ExportAuditTrail, GetAuditOutcomeCounts
```

The Blazor UI mirrors this under `UI/FrontendWebassembly/Component/ATS/` with 18 matching areas (AIAssistant, ApplicationForm, AuditTrail, BulkUploads, ClientAssignment, ClientManagement, Dashboard, DisputeOrder, EmailAccountManagement, ModuleManagement, Notifications, OMSTicketing, Orders, PackageManagement, RoleManagement, Settings, UserManagement, Withdrawn).

## 🧩 The Vertical Slice Pattern

Each use case is a folder containing **exactly two files**. Reference implementation: `BackendAPI/Modules/ATS/Features/Web/UserManagement`.

```text
Features/Web/UserManagement/
├── Command/
│   ├── AddUser/            AddUserEndpoint.cs  +  AddUserHandler.cs
│   ├── EditUser/           EditUserEndpoint.cs +  EditUserHandler.cs
│   └── AssignUserClient/   ...Endpoint.cs      +  ...Handler.cs
└── Query/
    ├── GetUsers/           GetUsersEndpoint.cs +  GetUsersHandler.cs
    ├── GetAuthUsers/  GetMyAccess/  GetMyModules/  GetMyRoleId/  GetUserClientAssignments/
```

- **`<Name>Endpoint.cs`** — the HTTP contract. Co-located request/response records, a Carter `ICarterModule` route mapping, command/query construction, `ISender` dispatch, full OpenAPI metadata (`.WithTags`, `.Produces`, `.ProducesProblem`, `.WithSummary`, `.WithDescription`), and `.RequireAuthorization()`.
- **`<Name>Handler.cs`** — everything else: the `ICommand<T>`/`IQuery<T>` record, the FluentValidation `AbstractValidator<T>`, and the handler, which delegates to a domain service.

Two deliberate conventions:

1. **No mapping profiles in slices.** Mapster (`.Adapt<T>()`) is used inside services where it earns its keep, but slice DTOs are hand-shaped records. Shared payload DTOs live in `Modules/<Module>/DTO/`.
2. **No caching in slices.** Caching lives one layer down as a Scrutor repository decorator — `services.Decorate<IATSRepository, ATSCacheRepository>()` — so cache keys, tags, and invalidation stay next to the data access code.

MediatR pipeline order is `Validation → Logging → Audit`; audit registers last so an invalid request is never recorded as a user action.

## 🧱 Building Blocks

`BackendAPI/BuildingBlocks/BuildingBlocks/` is the shared kernel every module depends on.

| Folder | Provides |
|---|---|
| `CQRS/` | `ICommand`, `ICommand<TResponse>`, `IQuery<TResponse>` (`where TResponse : notnull`), handler marker interfaces |
| `Behaviors/` | `ValidationBehavior<,>`, `LoggingBehavior<,>` |
| `Data/` | `ITransactionScope`, `TransactionRunner` |
| `Exceptions/` | `BadRequest`, `Conflict`, `Forbidden`, `InternalServer`, `NotFound`, `Unauthorized` exceptions + `CustomExceptionHandler`, `SideEffectGuard` |
| `Pagination/` | **Keyset/cursor** pagination — `KeysetPaginationRequest`, `KeysetPaginatedResult<T>`, `KeysetPage`, `CursorCodec` (opaque cursors), backed by dedicated database indexes |
| `SharedInterfaces/` | `IReverseProxyModule` — the contract modules implement so the gateway can build its own route table |
| `SharedDTO/` | `RouteDefinitionDTO`, `ClusterDefinitionDTO` |
| `SharedConstants/` | `GatewayConstants` — cluster ids, HTTP methods, rate-limit policy names |
| `SharedServices/` | `EmailService`, `HashService`, `OtpService`, `SecureToken`, `AesGcmSecretProtector` (+ interfaces) |
| `Storage/` | `IObjectStorageService` → `AlibabaOssStorageService` (Alibaba Cloud OSS) |
| `Text/` | `CsvTextDecoder` for bulk-upload parsing |
| `SignalR/` | `HubCallerContextExtensions` |

## ⚙️ Cross-Cutting Concerns

**Authentication** — JWT bearer, but the token is read from an **HttpOnly cookie** (`OnMessageReceived` in `ServiceConfiguration.cs`), never from browser storage. Every request re-validates session liveness through the `sid` claim via `IAuthSessionValidator`, so logout and lockout take effect immediately instead of waiting for token expiry. SAML2 cookie sign-in runs alongside for SSO.

**Authorization and scoping** — there is no `Tenant` abstraction. The equivalent is ATS **client-scoping**: `Modules/ATS/Services/AccessScope/AtsAccessScopeResolver.cs` implements a role ladder (PlatformSuperAdmin → all clients; PlatformManager/Admin → assigned client ids from `UserClientDetails`; User/Uploader → own client and own records). The SPA uses its own permission checks (`AccessService.HasAccessAsync`, `RequirePermissionAttribute`, `RequireATSModuleAttribute`) rather than ASP.NET Core's `AuthenticationStateProvider`.

**Caching** — .NET `HybridCache` (L1 in-memory, L2 Redis via the `TairRedis` connection string, `oneplatform:` prefix, 10-minute default). The distributed layer is currently **disabled by flag**, so each instance holds its own L1. Applied through Scrutor decorators in ATS, Auth, PhilSys, and EmploymentVerification; only the first keyset page is cached, and writes invalidate by tag.

**Background processing** — **Quartz.NET with a clustered PostgreSQL persistent store** (`ats.qrtz_*` tables; schema in `BackendAPI/API/APIs/Scripts/quartz_postgres.sql`) running bulk submissions, email notifications, applicant search projection, OMS ticketing, and PhilSys transaction cleanup. Plus .NET hosted services for audit drain (System.Threading.Channels) and retention pruning. No Hangfire.

**Realtime** — two SignalR hubs: `ATSHub` (bulk-upload progress, gateway route `/hubs/atsbulk/{**catch-all}`) and `AIAgentHub`.

**AI** — Semantic Kernel in two places, intentionally different patterns. The **ATS assistant** uses plain plugin classes with `[KernelFunction]` methods and automatic function calling — prefer this for new work. The older **AIAgent** module discovers `*.skill.yaml` manifests through a reflection registry and requires the user to pick a skill. See [docs/features/ats-ai-assistant/ats-ai-assistant.md](docs/features/ats-ai-assistant/ats-ai-assistant.md).

**Email** — MailKit with a per-mailbox connection pool and rate limiter registry, multi-account failover when a mailbox is capped or throttled, and AES-GCM-encrypted credentials at rest. See [docs/features/ats-email-accounts/ats-email-accounts.md](docs/features/ats-email-accounts/ats-email-accounts.md) and [docs/features/ats-email-delivery/ats-email-delivery.md](docs/features/ats-email-delivery/ats-email-delivery.md).

**Audit trail** — an EF Core `SaveChanges` interceptor captures before/after values, drained asynchronously through a channel, with redaction, a `SkipAuditAttribute` opt-out, 30-day default retention, and Excel export.

**Documents** — QuestPDF and PDFsharp for consent forms and reports, ClosedXML for audit-trail export and bulk templates, CsvHelper for subject uploads.

**Observability** — Serilog JSON to console plus the custom PostgreSQL sink; prometheus-net with `UseHttpMetrics` deliberately placed between routing and exception handling; `/health`, `/health/live`, `/health/ready`, `/metrics`. PostgreSQL is tagged `ready` as the one hard dependency, while Redis reports **Degraded only** so a cache blip never pulls instances out of rotation. Prometheus/Grafana/Alertmanager run on a separate monitoring server that scrapes the gateway from an allowlisted CIDR — they are not part of this repository's compose stack.

## 🛠️ Technology Stack

### Backend

- **.NET 10.0** · **Carter** 10.0 (minimal APIs) · **MediatR** 12.2 (CQRS)
- **FluentValidation** 12.1 · **Mapster** 10.0 (object mapping)
- **EF Core** 10.0 + **Npgsql** · **Pgvector.EntityFrameworkCore** 0.3
- **Quartz.NET** 3.18 (clustered, PostgreSQL store)
- **HybridCache** 10.6 + StackExchange Redis · **Scrutor** 7.0 (assembly scanning, decorators)
- **Serilog** 10.0 · **prometheus-net** 8.2 · **AspNetCore.HealthChecks**
- **Sustainsys.Saml2** 2.11 (SSO) · **JwtBearer** 10.0
- **Semantic Kernel** 1.76 (AI) · **MailKit** 4.17 (SMTP)
- **QuestPDF** · **ClosedXML** · **Swashbuckle** (OpenAPI)

### Frontend

- **Blazor WebAssembly** 10.0 (standalone — no server prerendering) · **MudBlazor** 9.6
- **SignalR Client** 10.0 · **Semantic Kernel Core** · **Markdig** · **ExcelDataReader**
- CSS design tokens (`wwwroot/css/theme.css`) with `html.dark` overrides; dark mode applied pre-boot to avoid a flash. No localization framework — single language.

### Gateway & Infrastructure

- **YARP** 2.3 · **Docker Compose** (Linux containers) · **PostgreSQL 16** with pgvector 0.7.0 compiled from source

### Testing

- **xUnit** 2.9 · **FluentAssertions** 8.10 · **Moq** 4.20 (+ Moq.EntityFrameworkCore)
- **WebApplicationFactory** (`Microsoft.AspNetCore.Mvc.Testing`) · **Testcontainers.PostgreSql** 4.13

## 🚀 Getting Started

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-compose/)
- [Visual Studio 2026 Insiders](https://visualstudio.microsoft.com/vs/preview/) or [VS Code](https://code.visualstudio.com/)

### Environment configuration

Secrets are **not committed**. Two mechanisms supply them:

- **`.env` at the repository root** — required by `docker-compose.yml` (`env_file: .env` for both `apigateway` and `apis`). Gitignored; create it locally before running compose.
- **ASP.NET Core UserSecrets** — the compose file bind-mounts `${APPDATA}/Microsoft/UserSecrets` into the `apis` container, and `APIs.csproj` declares a `UserSecretsId`. Use this for connection strings (`OnePlatform_Connection`, `TairRedis`, `OMS_Connection`), `Jwt`, `HttpCookieOnlyKey`, `OpenAI`, `Saml2`, `SignalRHub`, and SMTP settings.

Per-environment config lives in `BackendAPI/API/APIs/appsettings.{Development,Testing,Sandbox,UAT,Production}.json` and `UI/FrontendWebassembly/wwwroot/appsettings.{Development,UAT,Production}.json`.

### Running with Docker Compose

```bash
git clone https://github.com/RusselG21/1CibiPlatform.git
cd 1CibiPlatform

# create .env at the repository root first (see Environment configuration)

docker-compose up --build
```

This starts `apigateway`, `apis`, and `postgres`. The `frontendwebassembly` service is **commented out** — in local development the SPA runs from Visual Studio or `dotnet run` and talks to the containerized gateway at `http://localhost:5123`, which is exactly what `wwwroot/appsettings.Development.json` sets as `ApiBase`.

Postgres initializes with `POSTGRES_DB=OnePlatform` (user `cibi`) and runs `docker/postgres/init-db.d/01_create_extension.sql` to create the `vector` extension. Data persists in the `pgdata` volume.

### Running locally without Docker

```bash
dotnet restore
dotnet build
```

Then run the projects individually — PostgreSQL must already be reachable:

```bash
cd ApiGateways/YarpApiGateway && dotnet run     # gateway
cd BackendAPI/API/APIs && dotnet run            # API host
cd UI/FrontendWebassembly && dotnet run         # SPA
```

### Ports

| Service | Docker (host → container) | Local `dotnet run` |
|---|---|---|
| Gateway | `5123 → 8080` | `http://localhost:5115` (https profile also `7048`) |
| API host | `5050 → 8080` | `http://localhost:5252` |
| PostgreSQL | `5432 → 5432` | — |
| SPA | not containerized locally | `http://localhost:5134` |

Development CORS allows `http://localhost:5123` and `http://localhost:5134`. Production CORS allows `https://apps.cibi.com.ph/oms` and `.../oms_uat`.

### Health and diagnostics

| Endpoint | Served by | Notes |
|---|---|---|
| `/health`, `/health/live`, `/health/ready` | Gateway + API | CIDR-gated at the gateway |
| `/metrics` | Gateway + API | Prometheus; CIDR-gated, 404 outside the allowlist |
| `/__routes` | Gateway | Dumps the discovered YARP route table |
| Swagger | API host | Swashbuckle, Development only |

Set `Monitoring:AllowedScrapeNetworks` to your monitoring server's CIDR — the default is empty, which denies all non-loopback scrapers.

### Running tests

```bash
dotnet test Test/Test/Test.csproj
```

Integration tests spin up a real `postgres:16` container via Testcontainers, so **Docker must be running**. They execute real EF migrations, strip Quartz hosted services, and substitute a `FakeEmailSender`; `BaseIntegrationTest` truncates all tables `RESTART IDENTITY CASCADE` and evicts HybridCache tags between tests.

## 🧪 Testing

One xUnit project (`Test/Test/`, ~104 suites) referencing the real projects:

- **Unit tests** — handlers, validators, services, and mappers with Moq fixtures: `BackendAPI/Modules/<Module>.UnitTests/`
- **Integration tests** — `WebApplicationFactory<Program>` against Testcontainers PostgreSQL: `BackendAPI/Modules/<Module>.IntegrationTests/`, with the shared harness in `BackendAPI/Infrastructure/<Module>.Infrastracture/`
- **UI tests** — plain xUnit coverage of frontend CSV helpers under `UI/`. Razor components are **not** covered (no bunit).

## 🏛️ Architectural Principles

- **One use case per slice folder.** Never create a generic controller holding unrelated operations.
- **High cohesion, low coupling.** A slice owns its contract, validation, and handler; shared behavior lives in BuildingBlocks or module services.
- **Modules declare their own routes.** The gateway assembles them; nothing is hand-maintained in gateway config.
- **Repositories stay focused.** Business services depend on narrow contracts (e.g. `ILoginRepository`, `IUserRepository`), not the aggregate interface, which exists only for decorator compatibility.
- **Caching belongs at the repository boundary,** tagged and invalidated on write, not scattered through handlers.
- **The codebase is the source of truth.** Inspect the named reference files before editing; this README and the guides can drift.

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Follow [docs/feature-development-guide.md](docs/feature-development-guide.md), including its definition-of-done checklist
4. Run the relevant tests and build the solution
5. Commit (`git commit -m 'Add some amazing feature'`), push, and open a Pull Request against `dev`

## 📄 License

This project is licensed under the terms specified in the [LICENSE](LICENSE) file.
