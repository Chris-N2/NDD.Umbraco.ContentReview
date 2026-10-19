# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A free, open-source Umbraco package, `NDD.Umbraco.ContentReview` (product
name and NuGet package ID, which are effectively permanent once pushed), that
lets editors set a **review date and an owner** on a content node (page or
folder), and notifies the owner when the review falls due. It's aimed at
universities, councils and other sites that have to keep pages current,
accessibility statements especially.

The free alternatives are thin: Umbraco Workflow (paid) has content reviews,
and cogPageReview dates from Umbraco 7. The sibling package
`D:\Applications\NDD\UmbracoPackages\uBookIt` uses the same stack and process,
and this repo's build plumbing (client build targets, manifest stamp, CI
results check) was ported from it. Look there for working patterns, and copy
the patterns, not the complexity.

## Invariants (these hold across every context and every spec)

1. **Umbraco LTS is what `main` targets.** That's Umbraco 17 on .NET 10 now.
   `Umbraco.Cms.*` is pinned to `[17.7.1,18.0.0)` and `Umbraco.Automate*` to
   `[17.5.1,18.0.0)` in `Directory.Packages.props`. The package version's
   major tracks the Umbraco major. No multi-targeting for older majors.
2. **Review dates, owners and reminders only.** No approval workflow, no
   publish gating, no sign-off chains, and save/publish is never blocked on
   review state. This keeps the package clearly separate from Umbraco
   Workflow's paid content-review feature. A proposal that drifts toward
   workflow is rejected at propose time.
3. **Umbraco Automate is optional.** Only `NDD.Umbraco.ContentReview.Automate`
   (and the TestSite) may reference Umbraco Automate. The aggregate package
   never brings it, and the core works fully without it. Core raises ordinary
   Umbraco notifications, and the Automate package maps them to Automate
   triggers. Tests enforce this at csproj level and in the packed nuspecs.
4. **The backoffice is native Umbraco 17.** Lit web components, the Umbraco UI
   Library (`uui-*`), TypeScript and Vite. No third-party widget frameworks.
5. **OpenSpec drives all changes.** explore → propose → apply → qa-review →
   archive. No code without an approved change. Never modify anything under
   `openspec/changes/archive/`.

## Commands

From the repo root. You need the .NET 10 SDK (`global.json` pins 10.0.300,
latest patch) and Node 22.

```
dotnet build NDD.Umbraco.ContentReview.slnx           # also builds the backoffice client
CI=true dotnet build NDD.Umbraco.ContentReview.slnx    # warnings as errors, exactly as CI
dotnet test NDD.Umbraco.ContentReview.slnx             # ~1-2 min: packs a clean copy of the repo
dotnet test tests/NDD.Umbraco.ContentReview.Tests --filter "FullyQualifiedName~ProjectGraphTests"   # one class (still packs: the fixture is assembly-wide)
dotnet pack NDD.Umbraco.ContentReview.slnx -c Release -o artifacts/pack
```

- **Backoffice client:** `src/NDD.Umbraco.ContentReview.Backoffice/Client`.
  Use `npm run build`, or `npm run watch` while developing. A normal
  `dotnet build` runs it incrementally, and `-p:ContentReviewBuildClient=false`
  skips it on a host without Node. The output goes to
  `wwwroot/App_Plugins/NDDContentReview/` (gitignored). The build stamps the
  manifest version there from `$(Version)`, and fails if the `"version":
  "0.0.0"` placeholder in `Client/public/umbraco-package.json` is missing.
- **TestSite:** `dotnet run --project src/NDD.Umbraco.ContentReview.TestSite`.
  It serves https://localhost:44341 (port 9010 is taken by Logitech G Hub on
  this machine). It uses SQLite and unattended install, so the first run
  creates the database, and the admin login is in its
  `appsettings.Development.json`. Automate shares the CMS database there
  (`Umbraco:Automate:UseNamedConnectionString`). Delete
  `src/NDD.Umbraco.ContentReview.TestSite/umbraco/Data` to start from scratch.
- **TestSite secrets:** the connection string and the image-signing key are
  in user secrets, never in appsettings. On a fresh clone, set them once:

  ```
  cd src/NDD.Umbraco.ContentReview.TestSite
  dotnet user-secrets set "ConnectionStrings:umbracoDbDSN" "Data Source=|DataDirectory|/Umbraco.sqlite.db;Cache=Shared;Foreign Keys=True;Pooling=True"
  dotnet user-secrets set "ConnectionStrings:umbracoDbDSN_ProviderName" "Microsoft.Data.Sqlite"
  dotnet user-secrets set "Umbraco:CMS:Imaging:HMACSecretKey" "<random base64>"
  ```

  If Umbraco writes an `Imaging:HMACSecretKey` into `appsettings.json`
  (it does that whenever none is configured), move it to user secrets.
  Don't commit it.
- **CI results check:** `scripts/ci/Assert-TestResults.ps1 -ResultsDirectory
  <dir>` after `dotnet test --logger trx --results-directory <dir>`. It fails
  if any test project didn't report or any test was skipped.

## Architecture

| Project | Ships | Role |
|---|---|---|
| `NDD.Umbraco.ContentReview` | yes | The package a site installs. No code. References Backoffice, and through it Core. Never references Automate. |
| `….Core` | yes | Domain, services and storage of review data. Raises Umbraco notifications. |
| `….Backoffice` | yes | Management API and the Lit client. Its csproj builds the client and adds it to static web assets (the targets explain why each step is needed). |
| `….Automate` | yes, optional | Maps Core's notifications to Umbraco Automate triggers. The only shipped project allowed to reference Automate. |
| `….TestSite` | no | Local Umbraco 17 site referencing the aggregate and Automate packages, as a site would install them. |
| `tests/….Tests` | no | xunit v3. |

How storage works isn't decided yet. The leading option is a composition
(document type) carrying the review properties, plus Umbraco's key-value
store for reminder bookkeeping, with no tables of our own. The first change
that stores review data decides it.

**The tests check outcomes, not prose.** `PackedPackages` (an assembly
fixture) copies the repo with no build output (no `bin`/`obj`,
`node_modules` or built client), packs it once, and the composition tests
read the resulting nuspecs. Packing a clean copy is deliberate: it's the only
way to catch a client that reaches the package only because a working copy
had it built. If you add a rule about packaging, add the test against the
packed output.

## Conventions

- **Namespaces contain `Umbraco`.** Inside `NDD.Umbraco.ContentReview.*`, a
  partly qualified name like `Umbraco.Cms.Core.X` resolves to `NDD.Umbraco`
  and fails to compile. Import Umbraco types with file-level `using`
  directives, or write `global::Umbraco…`.
- Nullable reference types are enabled everywhere, and warnings are errors in
  CI, except NuGet advisories NU1901–NU1904, which stay warnings.
- Central package management: versions live only in `Directory.Packages.props`.
  Package metadata lives in `Directory.Build.props`. Each packable project
  sets only its own `Title` and `Description`, and descriptions must differ.
- **Security pins** (Pkcs, MessagePack, OpenTelemetry.Api) are direct
  references on the projects that resolve a vulnerable version. Each has an
  EXIT condition in its comment. Don't add `CentralPackageTransitivePinning`.
  The TestSite alone suppresses the ImageSharp advisories, since it's never
  shipped.
- Management API endpoints require backoffice authorization policies, and
  respect the user's content access.
- Schema or document-type changes the package owns are additive. A
  destructive change needs explicit spec approval and an upgrade path.
- Owners are people, so owner names and emails are personal data. Keep them
  out of logs and test fixtures.
- Licence: MIT, copyright Norwood Design & Development Ltd.

## Workflow

OpenSpec (`@fission-ai/openspec`, CLI `openspec`) is installed globally. Its
skills are in `.claude/skills/`, and its slash commands are `/opsx:*`.
Project context and artifact rules are in `openspec/config.yaml`.

1. `/opsx:explore`: investigate and gather context. No decisions yet.
2. `/opsx:propose`: write proposal, design, spec deltas and tasks, then get
   human approval.
3. `/opsx:apply`: implement against the approved change.
4. `qa-review` skill: **always in a fresh session or subagent**, never the
   context that implemented the change. Use one reviewer subagent per change
   and send later rounds back to the same agent. A REJECT is a gate: findings
   go back through apply, and QA never edits code. Treat each round's fixes
   as new code, and ask the reviewer to verify your claims (build state, test
   counts) rather than trust them.
5. `/opsx:archive`: after QA approval, archive the change and sync the main
   specs.

When a delta uses `## MODIFIED Requirements`, remember that it replaces the
requirement wholesale. List every existing scenario and SHALL, and carry each
one forward, strengthen it, or state its removal in the proposal. Silence
deletes guarantees.

### Tooling notes (this machine)

- The `openspec` shim fails under Git Bash (a stray global `node` npm package
  shadows Node). Run `openspec` from **PowerShell**, or remove the stray
  packages with `npm uninstall -g node openspec`.
- Nested `dotnet` builds started from tests must disable MSBuild node reuse
  and the build server (`ProcessRunner` does this). Otherwise the
  long-running MSBuild worker processes inherit the output pipes and the
  test run hangs.
