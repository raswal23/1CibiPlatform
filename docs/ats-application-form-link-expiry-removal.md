# Removing the 24-hour application-form link expiry

## What this was

Every application-form invitation carried a deadline. `EmailInvitationRequest.HashTokenExpiration`
was stamped at creation as `now + ATS:ATSApplicationFormExpiryInHours` (24 in every environment),
and four separate places enforced it. Past the deadline the candidate's link showed a
"Session Expired" card and there was no self-service way back — the operator had to resend,
which rotated the token and invalidated the email the candidate already had.

## Why it is gone

The package follow-up reminder (see `ats-package-follow-up-email.md`) chases a candidate
`FollowUpEmail` days after the order. With a 24-hour link, every chaser past day one would
point at a dead page — the reminder and the expiry cannot both exist. Operationally the window
had also been too short regardless: a candidate who opened the email on a Monday evening and
gathered their documents over the week had already lost the link.

The link now stops working when the **form is no longer answerable**, not when a clock runs out.

## What replaced the guard

`IsHashTokenValidAsync` could not simply drop its expiry clause. Without it the query reduces to
"does a row with this token exist", which is permanently true — the link would never close.
The predicate is now `ApplicationFormStatus == Pending`, the same guard
`AuthorizeApplicationFormAsync` already applied. So:

- an unopened invitation from three months ago still works;
- a submitted or withdrawn form is refused, as before.

## Two traps this removal had to clear

**NULL read as expired.** `ApplicationFormClaimDTO.IsExpired` was
`!HashTokenExpiration.HasValue || HashTokenExpiration.Value <= DateTime.UtcNow`, and
`ApplicationFormService` repeated the same shape inline. The repository predicate
`HashTokenExpiration > DateTime.UtcNow` drops NULL rows silently in SQL. Making the column
nullable while any of those three survived would have locked out **every new candidate** — and,
through `PartnerSystemService`, every PhilSys ATS session. All three had to go in the same commit
as the migration, which is why this change is not splittable.

**A colliding, untagged cache key.** `IsHashTokenValidAsync` was cached under
`$"ATS_ApplicationFormStatus_{hashToken}"` — byte-identical to the key
`GetEmailIdAndApplicationFormPathAsync` uses for a different type — and passed no `tags:`, so
nothing ever evicted it. Harmless while the answer was "has this expired" and the entry was a
short-lived `true`; actively wrong now that the answer is "is this form still pending", because a
stale `true` would keep accepting a form that had just been submitted. The decorator is now a
pure passthrough.

## The column is kept, not dropped

`HashTokenExpiration` is nullable and nothing writes or reads it. Existing rows keep the deadline
they were issued under, so historical questions ("what was this candidate's window?") stay
answerable, and the change reverses without data loss — `Down` backfills
`HashTokenCreatedAt + interval '24 hours'` before restoring `nullable: false`.

Do not re-add a read of this column. If a future feature needs a deadline, it needs a new column
with its own semantics; this one now means "what the deadline would have been under the old
policy", which is not the same fact.

## Everything that was removed

| Layer | Removed |
| --- | --- |
| Entity/config | `HashTokenExpiration` required → nullable |
| DTOs | `ApplicationFormClaimDTO.IsExpired`, `EmailInvitationRequeueDTO.HashTokenExpiration`, `ExpiresAt` on both `EmailIdAndApplicationFormPathDTO`s |
| Guards | the `claim.IsExpired` throw and the inline `ExpiresAt` block in `ApplicationFormService` |
| Writes | expiry stamps in `EndorsementSubmissionService`, `BulkSubmissionProcessorService`, `ATSInitialData`; the expiration parameter on `RequeueEmailInvitationAsync` |
| Email copy | the two `{hours} hours` interpolations in `ATSEmailService` |
| Config | `ATS:ATSApplicationFormExpiryInHours` from `.env`, five `appsettings.*.json`, and two test fixtures |
| UI | `ExpiredComponent` (all three files), the `IsExpired` branch and field, the client-side re-check, and the "within the next 24 hours" copy |

`IConfiguration` left `ATSEmailService` and `BulkSubmissionProcessorService` entirely — the expiry
was their only consumer.
