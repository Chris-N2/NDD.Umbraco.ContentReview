---
name: qa-review
description: Adversarial QA review of an implemented OpenSpec change in this content-review package. Use whenever a change has completed openspec-apply-change and needs verification, or whenever the user asks to QA, review, verify, check, or sign off work. Must run in a fresh context or subagent that did not write the code under review. If this session implemented the change, tell the user to start a new session or spawn a subagent instead of proceeding.
---

# QA Review

## Stance

You did not write this code. Assume it contains defects and your job is to
find them. Do not give the implementer the benefit of the doubt. Check
behaviour against the **spec**, not against what the code or its comments
claim. You report findings and never fix code. Fixes go back through
`openspec-apply-change` so the review stays independent.

## Inputs

The change ID (or path) of the OpenSpec change to review. If it isn't given,
ask. Read the change's proposal, design, spec deltas and task list in full
before looking at any code.

## Process

Work through every gate. A gate marked **hard fail** forces a REJECT
verdict regardless of everything else.

### 1. Spec conformance

- Extract every requirement and scenario from the spec deltas into a
  checklist.
- For each one, find the implementing code and the test that covers it. A
  criterion with no covering test is a finding (major).
- Flag behaviour that is in the code but not the spec (scope creep), and
  behaviour that is in the spec but not the code.
- **Guarantees dropped by a rewritten requirement.** A `## MODIFIED
  Requirements` entry replaces its requirement wholesale. Diff each one
  against `openspec/specs/`. Every old scenario and SHALL must be carried
  forward, restated more strongly, or dropped with the drop stated in the
  proposal. An unexplained omission is a finding (major).

### 2. Build and tests

- `dotnet build` must succeed with zero warnings.
- `dotnet test`: all tests pass. Note any skipped tests and why.
- The backoffice client builds (`npm run build` in the client folder) with
  no type errors.
- New or changed public behaviour without new or changed tests is a finding
  (major).

### 3. Scope and dependency boundaries: **hard fail**

- **No approval workflow.** Any approval chain, publish gating, sign-off
  step, or blocking of save/publish on review state → REJECT. The package
  records review dates and owners and sends reminders, nothing more.
- **Automate isolation.** The core package must not reference Umbraco
  Automate. Check every `.csproj` outside the Automate integration project:
  ```
  grep -ri "automate" --include="*.csproj" --include="*.props" src/
  ```
  A hit in any project other than the Automate integration → REJECT.
- New third-party dependencies: confirm each one was named in the spec with
  a licence compatible with MIT.

### 4. Public API surface

- Diff the public API surface against the previous state. Every addition,
  change or removal must be intentional per the spec. A breaking change the
  spec doesn't declare is a finding (critical).

### 5. Persistence

- Migrations are additive: no column drops, renames or type narrowing
  without explicit spec approval.
- An upgrade from the previous package version applies cleanly.
- Deleting, trashing or moving a content node leaves no orphaned or wrong
  review records (whatever the spec says should happen, happens).

### 6. Notifications and scheduling

- A due review notifies **once** per due occurrence, not once per job run,
  per server, or per restart. Check behaviour in load-balanced setups
  (only the scheduling server runs the job, or there's an equivalent guard).
- Time zones and date boundaries: "due today" means the same thing to the
  job, the backoffice and the email.
- A failed send (SMTP down, missing owner) is logged without personal data
  and does not stop other reminders.

### 7. Security

- Management API endpoints carry backoffice authorization policies, and a
  user cannot read or set reviews on content they have no access to.
- No secrets, connection strings, or personal data in logs, exceptions shown
  to users, or test fixtures.

### 8. Accessibility (any change touching backoffice UI or email markup)

- Prefer `uui-*` components, and flag hand-rolled controls that duplicate
  one.
- Every input has an associated label, keyboard operation works, focus is
  managed when dialogs open and close.
- Notification emails are semantic HTML with a plain-text alternative.

## Report format

```
VERDICT: APPROVE | APPROVE WITH NITS | REJECT

Findings:
[CRITICAL] file:line — description; which spec criterion or invariant it violates
[MAJOR]    ...
[MINOR]    ...
[NIT]      ...

Must fix before merge: (list, or "none")
Spec criteria verified: n/m (list any unverifiable and why)
```

REJECT if any hard-fail gate trips or any CRITICAL finding stands.
APPROVE WITH NITS only when every remaining item is cosmetic.
