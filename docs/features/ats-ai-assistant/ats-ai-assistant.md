# ATS AI Assistant

The chat assistant on `/s&i/ats/aiassistant`: what it can do, how it is kept inside its
lane, and what to know before adding a function.

`docs/feature-development-guide.md` §"AI features" points here before you add a function,
change the system prompt, or let a model reach a write path.

The audit trail itself has no separate document: its access rule is stated in
`AtsAuditService.CanRead()`, what the assistant may do with it is section 4, and how
conversations are recorded into it is section 5.

---

## 1. What it does

Three things, and nothing else:

| Capability | Functions | Scope |
|---|---|---|
| Look up orders | `SearchOrdersBySubjectAsync` | The caller's clients, via `IAtsAccessScopeResolver` |
| Prepare a new order | `GetAvailablePackagesAsync`, `StageNewOrderAsync` | Same |
| Report on the audit trail | `GetAuditSummaryAsync`, `SearchAuditEntriesAsync` | **Platform super admin only** |

Everything else is refused. `RejectOutOfScopeRequest` exists so the model has to *classify*
a message rather than talk itself into answering.

## 2. The pattern

Semantic Kernel plugins: a plain class (`AI/AtsAssistantPlugin.cs`) whose methods carry
`[KernelFunction]` and `[Description]`, registered on a **cloned** kernel with
`AddFromObject` and invoked through `FunctionChoiceBehavior.Auto()`.

```text
AtsAssistantService.AskAsync
  -> new AtsAssistantPlugin(...)      one per request, carrying the caller's identity
  -> _kernel.Clone()                  so ATS plugins never leak onto the shared kernel
  -> kernel.Plugins.AddFromObject(plugin, "ats")
  -> chatCompletion.GetChatMessageContentAsync(history, settings, kernel, ct)
  -> read plugin.LastSearchResults / LastAuditEntries / StagedDraft
  -> AtsChatAnswerDTO(answer, orders, draft, auditEntries, auditQuery)
```

The plugin is constructed per request and holds the caller's `ICurrentUser`, so a function
can never see further than the user it answers for. **The kernel clone is not optional** —
without it one user's plugin instance would be visible to the next request.

This is deliberately different from the older `AIAgent` module, which discovers
`*.skill.yaml` manifests through a reflection registry. Prefer this pattern for new work.

## 3. The refusal is enforced in code, not just asked for

The prompt tells the model to call `RejectOutOfScopeRequest` and return its text. That is a
request, and a request can be talked around.

So `AtsAssistantService.AskAsync` **overwrites** the answer once the plugin has been told the
turn was out of scope, and withholds every structured payload the model also produced:

```csharp
if (plugin.WasRefusedAsOutOfScope)
{
    answer = AtsAssistantPlugin.OutOfScopeReply;
}

var orders = !plugin.WasRefusedAsOutOfScope && ... ? ... : null;
var auditEntries = !plugin.WasRefusedAsOutOfScope && ... ? ... : null;
var draft = plugin.WasRefusedAsOutOfScope ? null : plugin.StagedDraft;
```

A jailbreak that talks the model past its own refusal still gets nothing out. **Any new
structured field on the answer must be gated the same way.**

## 4. The audit trail capability

### It is super-admin only, and that check is different from every other function

Every other function scopes through `IAtsAccessScopeResolver` (client + requestor). The
audit functions do **not**. They check `ICurrentUser.IsPlatformSuperAdmin`, mirroring
`AtsAuditService.CanRead()`.

The reason is in that method's own comment: the trail is deliberately **not** client-scoped,
because a trail the audited user can read is a weaker control. Scoping it by client would
hand every uploader a filtered view of their own colleagues' actions.

The check is duplicated on purpose — in the plugin *and* in the service:

- **In the plugin**, so the model is told it may not read the trail and relays that, rather
  than being handed an empty list it might explain away as "no activity today".
- **In the service**, so the boundary holds even if a future function forgets.

### What the model is given, and what it is not

`AtsAuditEntrySummaryDTO` is deliberately narrower than the screen's `AuditTrailListDTO`:

| Excluded | Why |
|---|---|
| `Payload` | The redacted command JSON. Contains free text a user typed — prompt-injection surface. |
| `Changes` | Field-level before/after values. Same reason. |
| `IpAddress`, `TraceId`, role/client ids | Forensic detail that belongs on the screen built for it. |

`FailureReason` **is** included — it is the reason anyone asks — but truncated to 200
characters before it reaches the model, because an exception message can run to thousands
and is attacker-influencable.

### Periods, not dates

Both functions take `daysBack`, not a date range. Models are unreliable with relative dates,
and a day count is trivially bounded: clamped to 1–90, with 0 (an omitted argument) treated
as the 7-day default rather than an empty range.

### The two functions compete, and the descriptions are what separate them

`[Description]` text is the **only** thing steering which function the model calls. Both
audit functions answer questions about "failures today", so the wording has to divide them
explicitly or the model picks by feel.

It got this wrong once. `SearchAuditEntries` said *"use this **after a summary**"*, which the
model read as "this is a follow-up step" — so "list all the errors" was answered from
`GetAuditSummary` in prose, nothing populated `LastAuditEntries`, and **no table appeared**.
Intermittently, which made it look like a rendering bug.

The split is now stated from both sides:

| | `GetAuditSummary` | `SearchAuditEntries` |
|---|---|---|
| Answers | how many | which ones |
| Renders | a sentence, **no table** | the table |
| Claims | "how many", "were there any" | "list", "show", "display", "see" |
| Disclaims | listing verbs, by name | — |

`AtsAssistantPluginTests` §"Function descriptions" asserts these properties — including that
"after a summary" never comes back — because a description is prose that no compiler checks.

The row cap is **50** (not the order search's 10): "list all the failures" is a normal
question, and ten rows reads as a broken answer. The prompt tells the model to say the newest
are shown, and point at the export, when the count exceeds what it received.

## 5. Every conversation is audited

Both sides of every turn — the question **and** the answer — are recorded in the ATS audit
trail as an `AskAtsAssistant` entry.

### Why the service writes it, not the pipeline

`AskAtsAssistantCommand` still carries `[SkipAudit]`, which looks wrong until you know why:
**`AtsAuditBehavior` only ever serializes the request.** An entry written there would capture
what was asked and lose what the system replied, and half a conversation is not a record of
it.

So `AtsAssistantService.RecordAudit` writes the entry itself, straight to `IAtsAuditWriter`,
with `AtsChatAuditPayloadDTO` as the payload:

```json
{
  "Question": "show me today's failures",
  "Answer": "There were 12 failed actions today.",
  "WasRefused": false,
  "OrderResultCount": 0,
  "AuditResultCount": 12,
  "StagedOrderDraft": false
}
```

It is written in both the success and the exception path, so a turn that blew up is recorded
too — that is exactly the one someone comes looking for. The whole method is wrapped
best-effort: nothing about recording a conversation may break the conversation.

### What is stored, and what is not

**Counts, not rows.** `OrderResultCount` and `AuditResultCount` say how much came back, but
the rows themselves are not copied in. That data already lives in the tables this trail sits
beside, and duplicating candidate details into the audit payload would spread it further for
no gain. `AuditResultCount > 0` is how you find who used the assistant to read the trail.

**The question is stored verbatim, and that is a real trade-off.** `AtsAuditRedactor` masks
by *property name*, which cannot help with free prose — a question that happens to contain an
SSS or TIN is stored exactly as typed. Accepted deliberately, for a complete transcript, and
it is part of why the trail stays super-admin only. The detail dialog shows an extra warning
on these rows so nobody reads the standard "IDs are masked" note and assumes it applies.

Both fields are truncated at 4,000 characters. The question is already validated to 2,000,
but an answer is model output with no such cap.

## 6. Export to Excel

**A model cannot hand the browser a file.** A download has to be started by a real user
gesture on the page or the browser blocks it. So "export this to Excel" works like this:

```text
user: "export today's failures to excel"
  -> model calls SearchAuditEntriesAsync
  -> plugin records LastAuditEntries AND LastAuditQuery (the filters it used)
  -> chat renders the table plus an "Export to Excel" button
  -> the CLICK calls ats/exportaudittrail and downloadFile()
```

`AtsAuditQueryDTO` carries the model's own arguments back to the UI, so the workbook contains
exactly the rows shown — even after several more questions. The button is only offered
alongside a table (`auditQuery` is null when `auditEntries` is), so nobody can download a
period they were never shown.

The prompt tells the model it cannot download anything and must not claim it has.

### The workbook

`AtsAuditWorkbookWriter` (ClosedXML, the version the AIAgent module already uses) renders:

- **Failed rows tinted red across the whole row**, not just the outcome cell — someone
  scanning a thousand rows should find the failures without reading a column
- **"Cause of failure" as its own column**, immediately after Outcome
- **"Details"**, which for an `AskAtsAssistant` row is the transcript laid out as
  `Q: … / A: …` rather than raw JSON, with a `[refused as out of scope]` line when the turn
  was refused. Any other command falls back to its stored payload.
- Frozen header, auto-filter, wrapped Cause and Details columns, top-aligned rows

That styling is the reason this is a workbook rather than a CSV — a CSV cannot do any of it.

Two safety details: the cause **and the transcript** are written with `SetValue` as text so
a value starting with `=` cannot execute as a formula, and the filename is built from a
timestamp so no filter value reaches the `Content-Disposition` header. Both are pinned by
`AtsAuditWorkbookWriterTests`.

### The screen has the same export

`AuditTrailComponent` exports too, honouring its active outcome chip, search and date range —
unlike the bulk subject export, which is deliberately unfiltered because it is named after
one file. Both screens hit `ats/exportaudittrail`.

Unlike the paged read, **the export throws `ForbiddenException`** for a non-admin instead of
returning an empty result. A download leaves the system; a plausible-looking empty file is
worse than being told no.

## 7. How to verify it

```powershell
dotnet format BackendAPI/Modules/ATS/ATS.csproj whitespace --no-restore
dotnet build 1CibiPlatform.sln
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~AtsAssistantPluginTests"
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~AtsAuditServiceTests"
```

The tests that pin the security decisions:

- `GetAuditSummaryAsync_ShouldRefuse_WhenCallerIsNotAPlatformSuperAdmin`
- `SearchAuditEntriesAsync_ShouldReturnNothing_WhenCallerIsNotAPlatformSuperAdmin`
- `GetRecentEntriesAsync_ShouldReturnNothing_ForAnOrdinaryUser`
- `ExportAuditTrailAsync_ShouldThrowForbidden_ForAnOrdinaryUser`
- `Write_ShouldNotTreatLeadingEqualsAsAFormula`
- `Write_ShouldRenderAnAssistantTranscriptAsQuestionAndAnswer`

Manually, as a super admin:

1. "how many failed actions this week?" → counts in prose
2. "show me the recent failures" → table with a Cause column, red outcomes
3. "export those to excel" → button appears; click downloads a styled workbook
4. Ask something off-topic → refusal, **no** table and **no** export button

Then repeat 1–3 as an ordinary ATS user: the assistant should say the trail is not available
to their account, with no table and no button.

To confirm conversations are recorded, open the Audit Trail screen after any of the above —
each turn appears as an `AskAtsAssistant` row, and its detail dialog shows the question and
answer. Or straight from the database:

```sql
SELECT "OccurredAt", "UserFullName", "Payload"->>'Question', "Payload"->>'Answer'
FROM ats."AuditTrail"
WHERE "Action" = 'AskAtsAssistant'
ORDER BY "OccurredAt" DESC
LIMIT 20;
```

The payload is `jsonb`, so this is queryable directly — `"Payload"->>'AuditResultCount' <> '0'`
finds everyone who used the assistant to read the trail.

## 8. What not to do

- **Do not scope the audit functions with `IAtsAccessScopeResolver`.** It is the platform
  role that governs the trail, not the ATS client. Using the scope resolver would hand every
  uploader a filtered view of their colleagues' actions.
- **Do not add `Payload` or `Changes` to what the model sees.** They are the largest
  injection surface in the system and add nothing the screen does not already show.
- **Do not add a structured field to `AtsChatAnswerDTO` without gating it on
  `WasRefusedAsOutOfScope`.** That gate is what makes the refusal real.
- **Do not remove the kernel `Clone()`.** Plugins would leak across users and modules.
- **Do not let the model claim it downloaded, emailed or created anything.** It stages and
  reports; the application acts. `StageNewOrder` in particular only prepares a draft.
- **Do not describe one function in terms of another's turn order** ("use this after…",
  "once you have…"). The model treats that as a precondition and will not call it directly.
  Name the trigger words each function owns instead.
- **Do not remove `[SkipAudit]` from `AskAtsAssistantCommand` thinking it is an oversight.**
  It is there so the pipeline does not write a second, half-blind entry alongside the one
  `RecordAudit` writes with both sides of the conversation.
- **Do not let `RecordAudit` throw.** It is wrapped best-effort on purpose: recording a
  conversation must never break the conversation.
- **Do not describe UI in the prompt** ("press Confirm", "click the button"). The application
  decides what to render, and the wording drifts out of sync the moment it changes.
