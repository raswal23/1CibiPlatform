# Employment Verification auto-send — code walkthrough

Companion to [employment-verification-auto-send.md](employment-verification-auto-send.md).
That document says what was built and why; this one is for someone about to change it.

## Where the supervisor email comes from

The address the job falls back to is typed by the candidate on the application form. It
has been captured and validated for a long time — it was simply never read by the
verification path.

| # | File | What happens |
|---|---|---|
| 1 | `UI/.../Component/ATS/ApplicationForm/ApplicationFormComponent.razor` (~1112) | "Supervisor email" input, per employer |
| 2 | `ATS/DTO/ProfessionalExperiencesDTO.cs` | `Emp1SupervisorEmail` … `Emp3SupervisorEmail` |
| 3 | `ATS/Features/Web/AddApplicationFormData/AddApplicationFormDataHandler.cs:391-395` (and 460-464, 528-532) | required + email-format validation |
| 4 | `ATS/Services/ApplicationForm/ApplicationFormService.cs:344` | `.Adapt<ProfessionalExperiences>()` |
| 5 | `ats."ProfessionalExperiences"."Emp1SupervisorEmail"` | `varchar(255)` |
| 6 | `ATS/Shared/Implementations/ATSVerificationDataProvider.cs` | read into `SupervisorEmail` — **this is the new part** |

Step 6 is the whole fix on the read side. The old provider stopped at
`Emp1SupervisorName` and used `user.UserEmail` — the recruiter — for the address.

## One pass, end to end

**1. `BackgroundJobs/AutoVerificationRequest/AutoVerificationRequestJob.cs`**

Quartz `IJob`, `[DisallowConcurrentExecution]`, modelled on `ATS/BackgroundJobs/FollowUpEmail/`.
It decides nothing — schedule, scope, kill switch and catch only:

```csharp
var enabled = _configuration.GetSection("EmailVerification").GetValue("AutoSendEnabled", false);
if (!enabled) { return; }

using var scope = _scopeFactory.CreateScope();
var autoRequestService = scope.ServiceProvider.GetRequiredService<IAutoVerificationRequestService>();
```

The scope matters: `IAutoVerificationRequestService` is scoped and the job is a singleton.

Trigger in `AutoVerificationRequestJobSetup.cs` — every 5 minutes, registered with
`services.ConfigureOptions<AutoVerificationRequestJobSetup>()` in
`EmploymentVerificationServiceConfiguration.AddEmploymentVerificationServices`. There is
one Quartz scheduler in the process and **ATS owns it** (tables `ats.qrtz_*`,
`UseClustering()`); PhilSys registers a job the same way. EV needed the Quartz package
references and `global using Quartz;`, which it previously lacked.

**2. `Services/AutoRequest/AutoVerificationRequestService.cs`** — all the policy.

```csharp
// before the read, not after
await verificationService.ReinstateLapsedOrdersAsync(cancellationToken);

var available = await verificationService.GetAvailableATSRecordsAsync(cancellationToken);
```

Order matters. `GetAvailableATSRecordsAsync` only sees orders whose marker is true, so a
link that lapsed after its order was released would be invisible forever if the reconcile
ran second.

Already filtered per `(subject, segment)` — see below. Then consent, before any lookup:

```csharp
foreach (var record in available)
{
    if (record.PermissionToContact) { consented.Add(record); }
    else { skippedNoConsent++; }
}
```

Then one batched directory lookup for the whole pass, then per segment:

```csharp
var recipient = ResolveRecipient(record, mailboxes);
if (recipient is null) { skippedNoRecipient++; continue; }

try { await verificationService.CreateAndSendAsync(...); }
catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
catch (Exception exception) { failed++; /* log */ }
```

**Caught per item.** `CreateAndSendAsync` throws on send failure and the house rule is
"jobs catch; feature code does not" — one unreachable mailbox must not abandon the pass.
Cancellation is rethrown rather than counted as a failure.

`ResolveRecipient` is the whole recipient policy:

```csharp
if (string.IsNullOrWhiteSpace(record.SupervisorEmail))
    return null;

var address = record.SupervisorEmail.Trim();

if (!knownMailboxes.Contains(address.ToLowerInvariant()))
    return null;

return (address, VerificationRecipientSource.Directory);
```

Two ways to return null, one way to send. The directory is an **allow-list**: the address
comes from the candidate's own form, so writing to one the directory does not list would
let them nominate who verifies their own history. Lower-cased before comparing because
`ContactDirectoryService` stores contacts lower-cased.

There is no company-name matching left. An earlier revision preferred a company's vetted
mailbox and fell back to the candidate's address; that fallback was the hole this closes.

### The hand-off marker

At the end of the pass, orders with nothing outstanding are handed back:

```csharp
var finished = available
    .Select(record => record.SubjectId)
    .Distinct()
    .Where(subjectId => !unfinished.Contains(subjectId))
    .ToList();

await verificationService.ReleaseFinishedOrdersAsync(finished, cancellationToken);
```

`unfinished` is built as the loop runs. A segment lands in it when it is **deferred** —
trimmed by the cap, waiting on a directory contact, or its send failed. A segment the
candidate declined consent for does *not*, because it will never become sendable and would
otherwise hold the order queued forever.

That asymmetry is the whole rule: release on *settled*, hold on *deferred*.

The two provider methods behind it are plain `ExecuteUpdateAsync` calls:

```csharp
// ATSVerificationDataProvider
public Task ReleaseOrdersAsync(...)   => SetNeedsEmploymentVerificationAsync(ids, false, ct);
public Task ReinstateOrdersAsync(...) => SetNeedsEmploymentVerificationAsync(ids, true,  ct);
```

with `.Where(invitation => invitation.NeedsEmploymentVerification != needsVerification)` so
re-releasing an already-released order writes nothing.

Reinstatement is driven by `ListSubjectsWithLapsedRequestsAsync` — the mirror of the `Sent`
clause in the availability rule:

```csharp
.Where(request => request.Status == VerificationRequestStatus.Sent)
.Where(request => request.TokenExpiresAt < asOfUtc)
```

**3. `ATS/Shared/Implementations/ATSVerificationDataProvider.cs`** — the unpivot.

Two things bound the query before any of this runs:

```csharp
where invitation.OrderStatus == OrderStatus.InProgress
    && invitation.NeedsEmploymentVerification      // ← filtered index
```

and the select projects the 18 fields actually used rather than the whole 39-column
`ProfessionalExperiences` entity. Before both, a pass loaded every in-progress order and
its full employment row, then discarded most of it in memory — cost proportional to the
table, not to the work.

Queries once, then fans out with a local function, mirroring `ATSRepository.Reports.cs:622`:

```csharp
void AddSegment(short segment, string? companyName, /* … */ string? permissionToContact)
{
    if (string.IsNullOrWhiteSpace(companyName)) { return; }   // empty slot is not an employer
    records.Add(new ATSInProgressEmploymentRecord(
        SubjectId: order.EmailInvitationID,
        EmploymentSegment: segment,
        /* … */
        PermissionToContact: AffirmativeAnswer.IsAffirmative(permissionToContact)));
}

AddSegment(1, employment.Emp1CompanyName, /* … */);
AddSegment(2, employment.Emp2CompanyName, /* … */);
AddSegment(3, employment.Emp3CompanyName, /* … */);
```

In memory, not SQL: the row is already materialised and the slots are plain columns, so a
UNION would buy nothing and read far worse.

Two other fixes in the same method — `OrderStatus.InProgress` instead of the literal
`"In Progress"`, and the requestor join dropped (it existed only to supply the recruiter's
email, and being an inner join on a nullable `RequestorId` it silently discarded every
bulk-upload and public-API order).

**4. `ATS/Constants/AffirmativeAnswer.cs`** — consent parsing, promoted from a private copy
in `ApplicationFormPreviewPdfDocument` so the two cannot drift. Accepts `"yes"`/`"true"`
case-insensitively; **everything else, including null and blank, is false.** The stored
values are `"True"`/`"False"`: the form binds a checkbox, the DTO is `bool?`, the entity
column is `string?`, and Mapster bridges the last hop.

## The availability rule

This is the part that makes three segments independent.

`Data/Repository/EmploymentVerificationRepository.cs`:

```csharp
public async Task<IReadOnlyList<BlockedEmploymentSegment>> ListBlockedSegmentsAsync(...) =>
    await db.Requests.AsNoTracking()
        .Where(request => request.AtsSubjectId != null)
        .Where(request => request.EmploymentSegment != null)
        .Where(request =>
            request.Status == VerificationRequestStatus.Pending ||
            request.Status == VerificationRequestStatus.Verified ||
            (request.Status == VerificationRequestStatus.Sent && request.TokenExpiresAt >= asOfUtc))
        .Select(request => new BlockedEmploymentSegment(
            request.AtsSubjectId!.Value, request.EmploymentSegment!.Value))
        .Distinct()
        .ToListAsync(cancellationToken);
```

The status predicate is unchanged from before; **only the projection changed** — from
`Guid` to the `(SubjectId, Segment)` pair. `EmploymentVerificationService` then filters:

```csharp
.Where(record => !blocked.Contains(
    new BlockedEmploymentSegment(record.SubjectId, record.EmploymentSegment)))
```

Still deliberately uncached in the decorator: the result turns on how `asOfUtc` compares
to each token expiry, so a cached list would keep lapsed requests blocking their segment.

## Send-failure handling

`Services/EmailVerification/EmploymentVerificationService.cs`:

```csharp
if (!await _emailService.SendEmailAsync(entity.HrEmail, "Employment verification request", body, true))
{
    await _repository.MarkRespondedAsync(
        entity.Id, VerificationRequestStatus.Expired, DateTime.UtcNow, cancellationToken);
    throw new InvalidOperationException("The verification email could not be sent.");
}
```

The row is already committed — `AddAsync` saves — and `Pending` blocks its segment forever
with no expiry and no sweeper. `Expired` releases it, so a failed send is retried on the
next pass. It still throws, so the manual path and the job both see the failure.

**`Expired`, not `Rejected`.** `Rejected` now means the employer answered "not accurate"
and blocks the segment permanently; reusing it for a delivery failure would disguise one as
the other and strand the segment. `MarkRespondedAsync` derives `VerifiedAt`/`RejectedAt`
from the status, so `Expired` correctly leaves both null.

## Wiring not visible from one file

| Thing | Where | Note |
|---|---|---|
| Marker set on submit | `ATSRepository.ApplicationForms.cs` → `UpdateEmailInvitationRequestForFilledUpFormAsync` | Same `ExecuteUpdateAsync` that sets `NeedsProjection`. Set here, not at enrolment: the supervisor addresses only exist once the form is filled in |
| Marker cleared / reinstated | `ATSVerificationDataProvider.ReleaseOrdersAsync` / `ReinstateOrdersAsync` | The shared contract's only **write** methods — it was read-only before this feature |
| Job + service registration | `EmploymentVerificationServiceConfiguration.cs` | `ConfigureOptions<AutoVerificationRequestJobSetup>()` + `AddScoped<IAutoVerificationRequestService, …>` |
| Quartz packages | `EmploymentVerification.csproj` | pinned to ATS's 3.18.2 — one scheduler, ATS owns it |
| `global using Quartz;` | EV `GlobalUsing.cs` | also `Microsoft.Extensions.Options`, `System.Text.RegularExpressions` |
| Kill switch | `appsettings.{Development,Sandbox,UAT,Production}.json` | `EmailVerification:AutoSendEnabled`. **Still absent from Testing.** |
| Contract ↔ UI DTO | `ATSInProgressEmploymentRecord` ↔ `ATSInProgressEmploymentRecordDTO` | matched by property name; nothing enforces it at compile time |
| Backfill | migration `…_AddEmploymentSegmentAndRecipientSource` | existing rows → segment 1, `CandidateSupplied`; without it live requests stop blocking and get re-sent |

## Change X, also check Y

| If you change… | Also check |
|---|---|
| `ATSInProgressEmploymentRecord` | It is positional — the compiler finds backend call sites, but `ATSInProgressEmploymentRecordDTO` and `NeedsRequest.razor` match by name and will not break loudly |
| The availability predicate | `BlockedSegmentPredicateTests`, which mirrors it and must be changed with it; the `Expired`-on-send-failure path that depends on Expired releasing; and the decorator's deliberate non-caching |
| A `VerificationRequestStatus` member | Whether it blocks. A new value defaults to "releases", which is the unsafe direction for anything meaning "already answered" |
| The release rule | Whether the new case is *settled* or *deferred*. Deferred must land in `unfinished`, or the order is released and the segment stranded invisibly |
| `ListSubjectsWithLapsedRequestsAsync` | It mirrors the `Sent` clause of the availability rule; the two must agree or a lapsed segment reopens in EV while ATS stops offering it |
| `CreateAndSendAsync` | The 86-char hash the verify validators enforce; the emailed link embeds the stored hash itself |
| The job interval | The comment in `AutoVerificationRequestJobSetup` explaining why 5 minutes, and the pass cap |
| `AffirmativeAnswer` | `ApplicationFormPreviewPdfDocument.IsAffirmative` delegates to it — the PDF's employment-dates logic changes too |

## Tests

`Test/Test/BackendAPI/Modules/EmploymentVerification.UnitTests/`

- **`EmploymentSegmentAvailabilityTests`** — the regression this design exists for: a sent
  segment 1 leaves 2 and 3 available; a same-numbered segment on a *different* order does
  not block; all-blocked and none-blocked.
- **`AutoVerificationRequestServiceTests`** — sends only to a directory-listed address;
  case-insensitive matching; skip when unlisted or blank; skip without consent (and **no
  directory query issued** for it); three segments carry 1/2/3; each segment goes to its own
  employer; two segments sharing a mailbox still get two emails; one failure does not end
  the pass; blank position substituted. Plus the hand-off rules: release when every segment
  is settled, **keep queued** when one is waiting on a contact or its send failed, and
  reconcile-before-read.
- **`BlockedSegmentPredicateTests`** — mirrors the availability predicate, including
  `Rejected` blocking permanently and `Expired` releasing. It cannot invoke the repository
  directly (the DbContext is sealed, and the test project has no in-process EF provider), so
  it must be changed whenever that predicate is.

Both use `MockBehavior.Strict`, so an unexpected call fails the test rather than passing
silently — that is what makes "no lookup for a non-consented segment" an assertion rather
than a hope.

There are still **no EV integration tests**; the 475 existing ones exercise this migration
because `IntegrationTestWebAppFactory` runs every migration on a clean Testcontainers
database. A dedicated EV harness must clear **both** EV cache tags alongside truncation.
