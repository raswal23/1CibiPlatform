# Auth user approval rejection

## What it does

The **Approval** tab of User Management lists accounts awaiting a decision. Opening one
gives an administrator two outcomes: **Approve**, which sets `IsApproved = true` and emails
the user, or **Disapprove**, which takes them off the queue.

Disapprove previously did nothing — the button called the dialog's `Cancel()`, so it was
indistinguishable from closing the dialog. It now performs a real rejection.

## Rejection deactivates; it does not delete

`RejectUserAsync` sets `Authusers.IsActive = false`. The registration survives.

Both list queries already require `IsActive`, so clearing it removes the row from the
approval queue without destroying the record. That matters because a rejection is a human
judgement that can be wrong: recovering from a mistaken one is then a data fix, not a
re-registration by a person who has already been told no once.

It also leaves the login refusal intact — `LoginService` still finds the account, and
`IsApproved` is still false, so nothing about the sign-in experience changes.

The account remains visible on the **User** tab, which reports Approved and Active state and
can reactivate it. See
[`auth-user-status-management`](../auth-user-status-management/auth-user-status-management.md).

## Guards

| Condition | Result |
|---|---|
| User not found | `NotFoundException` → 404 |
| User is already approved | `BadRequestException` → 400 |

The second guard exists because this is the *approval queue's* action. An approved user
leaving through it would be an account deactivation wearing the wrong name, and the User tab
is where that belongs.

The not-found guard also covers a double rejection: `GetRawUserAsync` filters on `IsActive`,
so the second attempt finds nothing rather than silently succeeding.

## Two confirmations, deliberately

Disapprove sits directly beside Approve, where a misclick is easy, and it is the one action
on the screen that cannot be undone from the screen itself. So the dialog's button opens a
second confirmation (`ShowUserManagementConfirmationAsync`, the same one account unlock
uses) naming the email address before anything is written.

## No rejection email

Approve sends one through `SendApprovalNotificationAsync`. Reject sends nothing — no
template exists, and whether a rejected applicant should be told is a policy question rather
than a technical one. If that changes, the send belongs in `SideEffectGuard.RunAsync` after
the rejection has committed, not inside it.

## How to verify it

```powershell
dotnet test Test/Test/Test.csproj --filter "FullyQualifiedName~UserManagement"
dotnet build 1CibiPlatform.sln
```

Restart `apis` and `apigateway` — YARP reads routes at startup — and confirm `GET /__routes`
lists `RejectUserEntryPoint`.

Correct looks like: disapproving a pending user asks for confirmation, then removes the row
from the Approval tab; the same user still appears on the User tab as Inactive; and
approving from the Approval tab still emails them.

## What not to do

| Don't | Because |
|---|---|
| Hard-delete the user instead | A mistaken rejection becomes unrecoverable, and the row is the only record that the registration happened. |
| Drop the already-approved guard | The approval queue would become a second path to deactivating live accounts. |
| Write `IsActive` outside `EditUserAsync` | That is where the cache decorator invalidates `UsersTag`/`UnApprovedUsersTag`. |
| Let the dialog perform the call | The page owns the confirm-then-call sequence, matching the rest of the screen. |
