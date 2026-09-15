# 1CibiPlatform — Architecture Overview

**Audience:** management / stakeholders
**Scope of this document:** the two vertical slices in focus — **ATS** (Applicant Tracking System) and **Employment Verification**. Other modules exist on the same platform but are intentionally left out here.

---

## 1. The Big Picture

One platform, one deployable backend, many independent business modules.

```mermaid
flowchart TB
    subgraph USERS["👥 Who Uses It"]
        U1["CIBI Staff<br/>Recruiters &amp; Verifiers"]
        U2["Client Companies<br/>HR Teams"]
        U3["Job Applicants"]
        U4["Previous Employer HR<br/>external, email link only"]
    end

    subgraph EDGE["🌐 Presentation Layer"]
        WEB["Web Application<br/><b>Blazor WebAssembly + MudBlazor</b><br/>runs in the browser"]
    end

    subgraph GW["🚪 API Gateway"]
        YARP["<b>YARP Reverse Proxy</b><br/>single public entry point<br/>routing • URL masking • CORS"]
    end

    subgraph APP["⚙️ 1CibiPlatform Backend — Modular Monolith on .NET 10"]
        direction TB
        subgraph SLICES["Business Modules in scope"]
            ATS["<b>ATS Module</b><br/>Applicant Tracking"]
            EV["<b>Employment Verification</b><br/>Module"]
        end
        OTHER["Other platform modules<br/><i>out of scope for this deck</i>"]
        BB["<b>Building Blocks</b><br/>shared CQRS • validation • security<br/>pagination • storage • exceptions"]
    end

    subgraph DATA["💾 Data &amp; Infrastructure"]
        PG[("<b>PostgreSQL</b><br/>system of record")]
        REDIS[("<b>Redis / Tair</b><br/>distributed cache")]
        OSS[["<b>Alibaba Cloud OSS</b><br/>documents &amp; reports"]]
        MAIL{{"<b>Email Service</b><br/>SMTP notifications"}}
    end

    subgraph OBS["📊 Monitoring"]
        LOGS["Serilog → Loki → Grafana<br/>+ in-platform log viewer"]
    end

    U1 --> WEB
    U2 --> WEB
    U3 --> WEB
    WEB --> YARP
    U4 -. "secure email link" .-> YARP

    YARP --> ATS
    YARP --> EV

    ATS --- BB
    EV --- BB
    OTHER -.- BB

    ATS --> PG
    EV --> PG
    ATS --> REDIS
    EV --> REDIS
    ATS --> OSS
    ATS --> MAIL
    EV --> MAIL

    APP --> LOGS

    classDef user fill:#FDF2F8,stroke:#9D174D,stroke-width:1px,color:#500724
    classDef edge fill:#EFF6FF,stroke:#1D4ED8,stroke-width:1px,color:#0B1F52
    classDef core fill:#F5F3FF,stroke:#6D28D9,stroke-width:1px,color:#2E1065
    classDef focus fill:#ECFDF5,stroke:#047857,stroke-width:2px,color:#022C22
    classDef store fill:#FFF7ED,stroke:#C2410C,stroke-width:1px,color:#431407
    classDef muted fill:#F1F5F9,stroke:#94A3B8,stroke-width:1px,color:#334155,stroke-dasharray: 4 3

    class U1,U2,U3,U4 user
    class WEB,YARP edge
    class BB core
    class ATS,EV focus
    class PG,REDIS,OSS,MAIL,LOGS store
    class OTHER muted
```

**Why this shape matters commercially**

| Decision | Business benefit |
| --- | --- |
| **Modular monolith** — modules are isolated, but ship as one unit | Microservice-style team autonomy, without microservice hosting cost or operational complexity |
| **Vertical slices** — each feature owns its full stack | New features are added *beside* existing code, not *inside* it — low regression risk |
| **API Gateway in front** | Internal structure is never exposed publicly; routes can change without breaking clients |
| **Shared Building Blocks** | Security, validation, and logging are written once and enforced everywhere |

---

## 2. Technology Stack

```mermaid
flowchart LR
    subgraph FE["Frontend"]
        F1["Blazor WebAssembly"]
        F2["MudBlazor<br/>Material design components"]
        F3["SignalR client<br/>live progress updates"]
    end

    subgraph API["Backend — .NET 10"]
        B1["Carter<br/>Minimal API endpoints"]
        B2["MediatR<br/>CQRS mediator"]
        B3["FluentValidation"]
        B4["Mapster<br/>object mapping"]
        B5["EF Core<br/>data access"]
        B6["Quartz.NET<br/>background jobs"]
        B7["Serilog<br/>structured logging"]
    end

    subgraph SEC["Security"]
        S1["JWT Bearer tokens<br/>HttpOnly cookies"]
        S2["SAML2 SSO"]
        S3["Hashed, expiring<br/>verification tokens"]
    end

    subgraph OPS["Platform &amp; Ops"]
        O1["Docker &amp; Docker Compose"]
        O2["PostgreSQL 16 + pgvector"]
        O3["Redis / Alibaba Tair"]
        O4["Alibaba Cloud OSS"]
        O5["Grafana + Loki"]
        O6["GitHub Actions CI"]
    end

    FE --> API --> OPS
    API --- SEC

    classDef fe fill:#EFF6FF,stroke:#1D4ED8,color:#0B1F52
    classDef be fill:#F5F3FF,stroke:#6D28D9,color:#2E1065
    classDef sec fill:#FEF2F2,stroke:#B91C1C,color:#450A0A
    classDef ops fill:#FFF7ED,stroke:#C2410C,color:#431407
    class F1,F2,F3 fe
    class B1,B2,B3,B4,B5,B6,B7 be
    class S1,S2,S3 sec
    class O1,O2,O3,O4,O5,O6 ops
```

---

## 3. What a "Vertical Slice" Means

Traditional layered systems split code by *technical layer*. We split by *business capability*. Each feature is a self-contained slice — request, validation, business rule, and data access all in one place.

```mermaid
flowchart TB
    REQ["HTTP Request<br/>from Gateway"]

    subgraph SLICE["One Vertical Slice — e.g. 'Create Verification Request'"]
        direction TB
        E["<b>Endpoint</b><br/>Carter — defines the URL"]
        C["<b>Command / Query</b><br/>the intent"]
        V["<b>Validator</b><br/>FluentValidation"]
        H["<b>Handler</b><br/>the business rule"]
        S["<b>Service</b><br/>orchestration"]
        R["<b>Repository</b><br/>EF Core + cache"]
    end

    DB[("Database")]

    REQ --> E --> C --> V --> H --> S --> R --> DB

    NOTE["✅ Add a feature = add a folder<br/>❌ Never edit five shared layers"]
    SLICE -.- NOTE

    classDef s fill:#ECFDF5,stroke:#047857,color:#022C22
    classDef n fill:#FEFCE8,stroke:#CA8A04,color:#422006
    class E,C,V,H,S,R s
    class NOTE n
```

---

## 4. Module Deep Dive — ATS &amp; Employment Verification

```mermaid
flowchart TB
    subgraph ATSM["📋 ATS Module — Applicant Tracking"]
        direction TB
        ATSF["<b>Feature Slices</b><br/>Client Management • Package Management<br/>User &amp; Role Management • Module Management<br/>Bulk Subject Upload • Application Forms<br/>Reports &amp; Downloads • Dispute Handling<br/>Dashboard • Order Status History"]
        ATSJ["<b>Background Jobs — Quartz.NET</b><br/>Bulk submission processing<br/>Email notification queue<br/>Applicant search projection"]
        ATSH["<b>SignalR Hub</b><br/>real-time bulk upload progress"]
        ATSD[("<b>ATS Data</b><br/>Applicants • Clients • Packages<br/>Reports • Roles • Order History")]
    end

    subgraph EVM["✅ Employment Verification Module"]
        direction TB
        EVF["<b>Feature Slices</b><br/>Create Verification Request<br/>List Requests<br/>Get Available ATS Records<br/>Preview by Token<br/>Verify / Reject Employment"]
        EVD[("<b>EV Data</b><br/>Verification Requests<br/>hashed tokens • expiry • outcome")]
    end

    CONTRACT["<b>IATSVerificationDataProvider</b><br/><i>published contract</i><br/>ATS exposes in-progress employment records;<br/>EV consumes them. No direct database coupling."]

    ATSF --> ATSD
    ATSJ --> ATSD
    ATSF --> ATSH
    EVF --> EVD

    ATSM -->|"provides"| CONTRACT
    CONTRACT -->|"consumed by"| EVM

    classDef ats fill:#EFF6FF,stroke:#1D4ED8,stroke-width:2px,color:#0B1F52
    classDef ev fill:#ECFDF5,stroke:#047857,stroke-width:2px,color:#022C22
    classDef ct fill:#FEFCE8,stroke:#CA8A04,stroke-width:2px,color:#422006
    class ATSF,ATSJ,ATSH,ATSD ats
    class EVF,EVD ev
    class CONTRACT ct
```

> **Key architectural point for leadership:** Employment Verification does **not** reach into the ATS database. It talks to ATS through a published contract. Either module can be reworked, re-platformed, or extracted into its own service without touching the other.

---

## 5. End-to-End Business Flow — Employment Verification

This is the flow a stakeholder can follow without any technical background.

```mermaid
sequenceDiagram
    autonumber
    actor V as CIBI Verifier
    participant UI as Web App<br/>Blazor
    participant GW as API Gateway<br/>YARP
    participant EV as Employment<br/>Verification Module
    participant ATS as ATS Module
    participant DB as PostgreSQL
    participant ML as Email Service
    actor HR as Previous Employer HR

    V->>UI: Open Employment Verification
    UI->>GW: Request candidates awaiting verification
    GW->>EV: Route to module
    EV->>ATS: Get in-progress employment records<br/>via published contract
    ATS->>DB: Query applicant records
    DB-->>ATS: Records
    ATS-->>EV: Candidate list
    EV-->>UI: Display candidates

    V->>UI: Select candidate, send request
    UI->>GW: Create verification request
    GW->>EV: Route to module
    EV->>EV: Generate secure token<br/>hashed, time-limited
    EV->>DB: Save request
    EV->>ML: Send branded verification email
    ML-->>HR: Secure one-time link

    HR->>GW: Open link — no login required
    GW->>EV: Preview request by token
    EV->>DB: Validate token &amp; expiry
    DB-->>EV: Request details
    EV-->>HR: Show verification form

    HR->>GW: Confirm or Reject employment
    GW->>EV: Record outcome
    EV->>DB: Persist result &amp; timestamp
    EV-->>HR: Confirmation

    Note over V,DB: Verifier sees the updated status<br/>on the dashboard
```

**Business value delivered by this flow**

- Employment checks that were manual phone calls and email chasing are now a tracked, auditable, self-service workflow.
- The external HR contact needs **no account** — one secure, expiring, single-purpose link.
- Every request has a timestamped audit trail: who requested, who responded, when, and what the outcome was.

---

## 6. Security Model

```mermaid
flowchart LR
    subgraph INT["Internal Users"]
        I1["Login"] --> I2["JWT in<br/>HttpOnly cookie"] --> I3["Server-side<br/>session validation"] --> I4["Role-based<br/>authorization"]
    end

    subgraph EXT["External HR Contacts"]
        E1["Cryptographically<br/>random token"] --> E2["Stored hashed<br/>never in plain text"] --> E3["Time-limited<br/>expiry window"] --> E4["Single-purpose<br/>scope"]
    end

    subgraph PLAT["Platform Wide"]
        P1["Gateway is the only<br/>public surface"]
        P2["CORS allow-list<br/>per environment"]
        P3["Structured audit<br/>logging"]
        P4["Secrets via<br/>environment config"]
    end

    classDef a fill:#EFF6FF,stroke:#1D4ED8,color:#0B1F52
    classDef b fill:#ECFDF5,stroke:#047857,color:#022C22
    classDef c fill:#FEF2F2,stroke:#B91C1C,color:#450A0A
    class I1,I2,I3,I4 a
    class E1,E2,E3,E4 b
    class P1,P2,P3,P4 c
```

---

## 7. Deployment Topology

```mermaid
flowchart TB
    subgraph HOST["Containerized Deployment — Docker Compose"]
        direction TB
        C1["<b>apigateway</b><br/>YARP container"]
        C2["<b>oneplatform-backend</b><br/>API + all modules"]
        C3["<b>postgres</b><br/>pgvector:pg16"]
        C4["<b>loki</b> + <b>promtail</b><br/>log aggregation"]
        C5["<b>grafana</b><br/>dashboards"]
    end

    subgraph CLOUD["Managed Cloud Services"]
        M1["Alibaba OSS<br/>object storage"]
        M2["Alibaba Tair / Redis<br/>distributed cache"]
        M3["SMTP relay"]
    end

    BROWSER["User Browser<br/>Blazor WASM assets"]

    BROWSER --> C1 --> C2
    C2 --> C3
    C2 --> M1
    C2 --> M2
    C2 --> M3
    C2 --> C4 --> C5

    classDef box fill:#F5F3FF,stroke:#6D28D9,color:#2E1065
    classDef cloud fill:#FFF7ED,stroke:#C2410C,color:#431407
    classDef user fill:#FDF2F8,stroke:#9D174D,color:#500724
    class C1,C2,C3,C4,C5 box
    class M1,M2,M3 cloud
    class BROWSER user
```

---

## 8. Summary for Leadership

| Question | Answer |
| --- | --- |
| **How fast can we add a new business module?** | Modules are self-contained. A new one plugs into registration, gateway routing, and shared building blocks — no rewrite of existing modules. |
| **What is the blast radius of a change?** | A vertical slice. Changing one feature does not touch shared layers used by other features. |
| **Can we scale?** | Yes — the gateway allows multiple backend instances, cache and storage are already externalized, and any module can be extracted into its own service later because integration is contract-based. |
| **Is it observable?** | Every request is structured-logged through Serilog, shipped to Loki, and visualised in Grafana, with an in-platform log viewer for support staff. |
| **What is the operational cost profile?** | One deployable backend instead of N microservices — significantly lower hosting, networking, and DevOps overhead at this stage of growth. |
