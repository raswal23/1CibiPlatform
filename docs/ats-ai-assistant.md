# ATS AI Assistant

The ATS AI Assistant is a chat surface inside ATS that does two things: it looks up existing
orders by candidate name, and it collects the details for a new order and stages it for the
requestor to confirm.

It is ATS module **12**, routed at `/s&i/ats/aiassistant`.

## Relationship to the AIAgent module

`BackendAPI/Modules/AIAgent` was the reference for the LLM stack, not a template to copy. That
module discovers skills from `*.skill.yaml` manifests through a reflection-based `SkillRegistry`,
and the user picks a skill from a dropdown before asking.

ATS deliberately does **not** do that. It uses the plain Semantic Kernel pattern:

- one plugin class whose methods are marked `[KernelFunction]` and `[Description]`;
- the plugin is registered on the kernel with `AddFromObject`;
- `FunctionChoiceBehavior.Auto()` lets the model pick the function.

There is no manifest, no registry, no skill picker. To add a capability you add a method.

## Architecture

```text
AIAssistantComponent.razor(.cs)
  -> IAtsAssistantService (UI, named "API" HttpClient)
  -> /ats/askassistant            (YARP, ATSPaths.cs)
  -> askatsassistant              (Carter endpoint)
  -> AskAtsAssistantHandler       (MediatR)
  -> IAtsAssistantService         (ATS module)
  -> Kernel.Clone() + AtsAssistantPlugin
  -> IATSRepository / IOrderHistoryService / IPackageManagementService
  -> PostgreSQL
```

Key files:

| Concern | File |
|---|---|
| Kernel functions | `BackendAPI/Modules/ATS/AI/AtsAssistantPlugin.cs` |
| Staged orders | `BackendAPI/Modules/ATS/AI/AtsOrderDraftStore.cs` |
| Conversation history | `BackendAPI/Modules/ATS/AI/AtsChatHistoryStore.cs` |
| Orchestration + system prompt | `BackendAPI/Modules/ATS/Services/AIAssistant/AtsAssistantService.cs` |
| Slices | `BackendAPI/Modules/ATS/Features/AIAssistant/` |
| Gateway routes | `BackendAPI/Modules/ATS/Path/ATSPaths.cs` |
| Chat UI | `UI/FrontendWebassembly/Component/ATS/AIAssistantComponent.razor` |

## The plugin

`AtsAssistantPlugin` exposes four functions:

| Function | Purpose |
|---|---|
| `SearchOrdersBySubjectAsync` | Find orders by full or partial candidate name |
| `GetOrderStatusHistoryAsync` | The dated lifecycle timeline of one order |
| `GetAvailablePackagesAsync` | The packages assigned to the caller's client |
| `StageNewOrderAsync` | Validate details and stage a draft — **never writes** |

Search reuses `IATSRepository.SearchReportsAsync`, which already matches on
`FirstName + ' ' + LastName` with `ILike`. No new query or migration was added for this feature.

### Adding another function

1. Add a method to `AtsAssistantPlugin` with `[KernelFunction]` and a `[Description]` that says
   when to call it. Describe every parameter too — the descriptions are the model's only
   documentation.
2. If it reads data, take the caller's access into account: the plugin holds `ICurrentUser`
   and resolves the authorized client ids / required requestor id the same way
   `ReportService.GetReportsAsync` does, so pass those to the repository rather than
   querying unscoped.
3. If it writes data, stage it and require a confirmation step. Do not let the model write.
4. Mention the function in the system prompt only if the model needs sequencing rules
   (for example "call `GetAvailablePackages` before staging").

No registration step is needed — the method is discovered from the attribute.

## Confirm before write

The assistant cannot create an order. `StageNewOrderAsync` validates the details against the same
rules as `EmailInvitationRequestCommandValidator`, checks the package against the client's actual
list, and puts the result in `AtsOrderDraftStore`.

The API returns that draft as `PendingDraft`, the UI renders a confirmation card, and only when
the requestor presses **Confirm & send** does `/ats/confirmorderdraft` run. Confirmation calls the
existing `IEndorsementSubmissionService.InsertEmailInvitationRequestAsync`, so the order gets the
same requestor, client, token, `OrderCreated` history entry and invitation email as one created
from the New Order screen.

Drafts are single use and expire after 15 minutes, and a draft can only be consumed by the user
who staged it, so a stale or copied card cannot be replayed.

## Access scope

`AtsAssistantService` is scoped, so it injects `ICurrentUser` and `IUserClientRepository`
directly. Both are handed to a **new plugin instance per request**, which derives the
authorized client ids and required requestor id from the caller's role — the same rules as
`ReportService.GetReportsAsync` — and the kernel is `Clone()`d before the plugin is added.

This matters: `AddFromType<T>()` would resolve the plugin from the root provider and give it a
root-scoped `ICurrentUser`, which would leak one client's data into another's conversation.
Always construct the plugin explicitly and use `AddFromObject`.

An unauthenticated or unauthorized caller gets no orders rather than an exception.

## Prompt injection

Candidate names, emails and package names are third-party data that reach the model. The system
prompt instructs the assistant to treat order data as data and never as instructions. The stronger
guarantee is structural: the plugin only ever reads within the caller's scope, and the only write
path requires a human click. A successful injection cannot widen data access or create an order.

## SignalR

The assistant reuses the existing `ATSHub` at `/hubs/atsbulk` rather than adding a hub.
`IATSClient` gained `ReceiveChatResponse` and `ReceiveChatTyping`; the hub already groups by
`userId` and is already routed through the gateway, so no new config key, environment variable or
gateway entry was required. The chat works over HTTP alone if the hub is unavailable — only the
typing indicator is lost.

## Configuration

Uses the existing `OpenAI:Endpoint`, `OpenAI:ApiKey` and `OpenAI:Model` settings
(`OPENAI__*` in `.env`). `AddATSAssistantConfiguration` registers ATS's own
`AddOpenAIChatCompletion` plus `AddKernel()`; it is a no-op when those settings are missing, so
the rest of ATS still starts. ATS registers its own chat completion because the AIAgent module
registers an `IChatClient` rather than an SK `IChatCompletionService`, which is what automatic
function calling needs.

## Module registration

`ModuleList.List` entry `12` (`aiassistant`, "AI Assistant") in
`UI/FrontendWebassembly/ShareData/ATS/ModuleList.cs`, mirrored by
`ATS.Constants.AtsModuleIds.AIAssistant`. `ModuleList.IsPrimaryNavigationModule` decides whether a
module renders in the main sidebar or under **Manage** — the assistant is in the main nav.

The seed grants module 12 to the Platform Manager, Admin and User roles.

> **Existing databases:** module rows are not retroactive. Users created before this feature need
> a `ats."UserDetails"` row with `ModuleId = 12` (or the module assigned through User Management)
> before the assistant appears in their sidebar.

## Tests

- `Test/.../ATS.UnitTests/AtsAssistantPluginTests.cs` — search projection, blank and denied-scope
  behavior, package filtering, draft validation, rejection of a hallucinated package, and
  single-use/owner-bound draft consumption.
- `Test/.../ATS.IntegrationTests/AtsAssistantServiceIntegrationTests.cs` — name and partial-name
  search against PostgreSQL, requestor scoping, and expired/unknown draft rejection.

The LLM is not called in tests; the plugin and confirmation path are exercised directly. If a test
ever needs the chat loop, register a fake `IChatCompletionService` in
`IntegrationTestWebAppFactory`.
