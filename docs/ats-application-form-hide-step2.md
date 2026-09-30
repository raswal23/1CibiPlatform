# Hiding (and restoring) application-form Step 2 — PhilSys verification

## What was hidden

`ApplicationFormComponent` used to show six steps to the candidate:

| Index | Title   | Content                             |
|-------|---------|-------------------------------------|
| 0     | Step 1  | Consent to collect and disclose     |
| 1     | Step 2  | **PhilSys national-ID verification**|
| 2     | Step 3  | Personal information + uploads      |
| 3     | Step 4  | Address and educational background  |
| 4     | Step 5  | Credentials, licenses, experience   |
| 5     | Final   | References and signature            |

Index 1 — the PhilSys step — is commented out of the stepper. Nothing was deleted:
the markup sits in a `@* … *@` block in `ApplicationFormComponent.razor`, and the
handlers it called (`ProceedClicked`, `SkipStep`) are still in the code-behind.

The form now shows five steps, renumbered Step 1 – Step 4 + Final.

## Why re-enabling is more than uncommenting

`MudStepper` assigns indices by child order, so removing a step shifts every later
step down by one. Seven places key off those indices, and all seven were shifted to
match. **Uncommenting the block without shifting them back leaves the form subtly
wrong** — upload validation would run against the wrong step, the Save & Next button
would appear on the PhilSys step alongside its own Skip/Proceed pair, and per-step
CSS would land on the wrong step.

Every changed site carries a comment pointing back at this file.

## Checklist to re-enable Step 2

Work through all eight items. Line numbers are from the commit that added this doc;
search the quoted code if they have drifted.

### 1. Uncomment the step

`UI/FrontendWebassembly/Component/ATS/ApplicationForm/ApplicationFormComponent.razor`

Remove the `@*` at line 182 and the `*@` at line 236, and the explanatory
`@* STEP 2 … *@` banner above them. The block between is the original step, unmodified.

> Razor comments do not nest. The block was checked to contain no `@*`/`*@` of its own —
> keep it that way if you edit it while hidden, or the comment will terminate early.

### 2. Renumber the visible step titles

Same file. Restore the titles and `Completed` indices:

| Line | Currently | Restore to |
|------|-----------|------------|
| 239  | `Title="Step 2" … Completed="@(_activeStep > 1)"` | `Title="Step 3" … Completed="@(_activeStep > 2)"` |
| 543  | `Title="Step 3" … Completed="@(_activeStep > 2)"` | `Title="Step 4" … Completed="@(_activeStep > 3)"` |
| 881  | `Title="Step 4" … Completed="@(_activeStep > 3)"` | `Title="Step 5" … Completed="@(_activeStep > 4)"` |
| 1639 | `Title="Final" … Completed="@(_activeStep > 4)"`  | `Title="Final" … Completed="@(_activeStep > 5)"` |

The `Class` attributes (`ats-step-one`, `ats-step-two`, …) were left alone — they are
CSS hooks, not step numbers, and they already disagreed with the titles before this
change. Do not try to "fix" them as part of this.

### 3. Shift the action-bar CSS classes

Same file, line 2020. The class names encode the original numbering:

```razor
@(_activeStep == 1 ? "ats-step-three-action-bar" : string.Empty)   →  == 2
@(_activeStep == 2 ? "ats-step-four-action-bar"  : string.Empty)   →  == 3
@(_activeStep == 3 ? "ats-step-five-action-bar"  : string.Empty)   →  == 4
```

`_activeStep == 0` (consent) and `stepper.Steps.Count - 1` (final) need no change —
one is anchored to the first step, the other counts from the end.

### 4. Restore the Save & Next guard

Same file, in `ActionContent` after the `Steps.Count - 1` submit branch. While Step 2
is hidden the `else` renders one unconditional button. The PhilSys step supplies its
own Skip/Proceed pair and must not also get Save & Next, so restore the split:

```razor
@if (_activeStep == 0)
{
    <MudButton Class="ats-save-next-button" … OnClick="OnSaveAndNextAsync">Save &amp; Next</MudButton>
}
else if (_activeStep != 1)
{
    <MudButton Class="ats-save-next-button" … OnClick="OnSaveAndNextAsync">Save &amp; Next</MudButton>
}
```

Both branches render the same button; the only purpose of the split is to skip index 1.

### 5. Shift the upload-validation switch

`ApplicationFormComponent.razor.cs`, `ValidateUploads()` (line 412). Each case is the
step whose uploads it checks:

```csharp
1 => …  // government ID / resume / NBI   →  2
2 => …  // diploma                        →  3
3 => …  // license / COE 1-3              →  4
```

### 6. Raise the active-step clamp

`ApplicationFormComponent.razor.cs`, `OnInitializedAsync` (line 166):

```csharp
_activeStep = Math.Clamp(ActiveStep, 0, 4);   →   Math.Clamp(ActiveStep, 0, 5);
```

The bound is the last step index.

### 7. Restore the page-level step allowlist

`UI/FrontendWebassembly/Pages/ATS/ATSApplicationForm.razor.cs`

```csharp
// line 14
private readonly HashSet<int> allowedSteps = new() { 0, 1, 2, 3, 4 };   →   add 5

// line 67-69 — fallback for an out-of-range ?stepActive= value
: 0;   →   : 1;
```

The fallback was `1` because that was the PhilSys step — a candidate with a bad query
value landed on verification. With Step 2 hidden, `1` is personal information, which
would skip consent, so it was changed to `0`. Restore `1` only if you want the original
behaviour back; `0` is also defensible.

### 8. Point the PhilSys liveness return back at index 2

`UI/FrontendWebassembly/Pages/Philsys/PhilSysLiveness.razor.cs`, `OnLivenessCompleted`
(line 162):

```csharp
…?showAppForm=true&philSysShow=false&stepActive=1   →   stepActive=2
```

This deep-link returns the candidate to the **personal-information** step, which PhilSys
has just pre-filled from `data_subject`. It must track that step's index, not the
PhilSys step's.

## What was deliberately not touched

- **The PhilSys components and backend.** `PhilSysComponent`, the liveness pages,
  `IPhilSysService` and every endpoint behind them are untouched and still wired up.
- **`ProceedClicked` / `SkipStep`.** Now only reachable from the commented block. Both
  carry a comment saying so; they look like dead code to a reader or an analyzer but
  deleting them means rewriting the step on restore.
- **The `showPhilSys` branch** at the top of the component. Step 2's Proceed button was
  the in-form way in, but `?philSysShow=true` still reaches it, so the PhilSys lookup UI
  is not orphaned — it is only unreachable from the stepper.
- **Draft state.** `ApplicationFormState.SignatureDetails` still persists `Consent` and
  `DeclineConsent`, the two flags Step 2's radio group bound to. Saved drafts stay
  round-trip compatible in both directions, so no state version bump was needed and
  drafts written while Step 2 is hidden will restore correctly once it is back.
- **CSS.** `ats-step-one*` rules in `wwwroot/css/ats.css` and
  `ApplicationFormComponent.razor.css` style the hidden step and were left in place.

## Verifying a restore

`dotnet build UI/FrontendWebassembly/FrontendWebassembly.csproj` catches nothing here —
every one of these is a valid-but-wrong integer or string. Check by hand:

1. The stepper header reads Step 1 … Step 5, Final.
2. Step 2 shows the PhilSys hero and the Yes/No radio pair, with **Skip and Proceed
   only** — no Save & Next.
3. Proceed opens the PhilSys lookup; completing liveness returns to **Step 3**
   (personal information) with the name and birth-date fields pre-filled.
4. Advancing past Step 3 with no government ID, resume, or NBI upload blocks with
   three inline errors — this is the check that silently moves to the wrong step if
   item 5 is missed.
5. Advancing past Step 4 with a college-or-higher attainment and no diploma blocks.
6. Submit on Final still works, and an unsigned form bounces back to Step 1.
