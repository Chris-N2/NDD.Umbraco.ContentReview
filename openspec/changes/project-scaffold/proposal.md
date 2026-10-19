## Why

The repository is empty. Every later feature (review dates, owners, reminders,
the Automate trigger) needs a solution to land in. The boundaries between
packages, especially the rule that only one package may know about Umbraco
Automate, are cheapest to enforce from the first commit. Adding them after
features exist means untangling references.

## What Changes

- A solution (`NDD.Umbraco.ContentReview.slnx`) with five shipped or dev
  projects and one test project:
  - `NDD.Umbraco.ContentReview.Core`: domain, abstractions, and (in later
    changes) persistence of review records. Empty here apart from a composer
    entry point.
  - `NDD.Umbraco.ContentReview.Backoffice`: Management API host and the
    Lit/uui/Vite backoffice client. MSBuild builds the client, so a clean
    clone packs a working package.
  - `NDD.Umbraco.ContentReview.Automate`: the optional Umbraco Automate
    integration. It is the **only** project that may reference Umbraco
    Automate. It contains no trigger yet.
  - `NDD.Umbraco.ContentReview`: the package a site installs. It contains no
    code and depends on Core and Backoffice, **not** Automate.
  - `NDD.Umbraco.ContentReview.TestSite`: a local Umbraco 17 site for
    development. It references everything, Automate included, and is never
    packed.
  - `tests/NDD.Umbraco.ContentReview.Tests`: an xunit test project.
- Repo-level build files: `global.json` (.NET 10 SDK),
  `Directory.Build.props` (nullable, warnings as errors in CI, shared package
  metadata), `Directory.Packages.props` (central package management, Umbraco
  pinned to the 17 line), `LICENSE` (MIT), `README.md`, `.gitattributes`.
- A backoffice bundle manifest, with no extensions yet, so the package shows
  in the backoffice Packages screen with the right version.
- A GitHub Actions CI workflow that builds the solution and client with
  warnings as errors, runs the tests, and fails if any test project didn't
  run.
- Guard tests: package composition (the aggregate reaches Core and
  Backoffice, and nothing but the Automate package depends on Automate) and
  the manifest version stamp.

**Staying in scope.** This change adds no content-review behaviour at all, so
it cannot drift toward an approval workflow. It does install the structural
guard that keeps Automate optional (invariant 3).

## Non-goals

- Any feature: review dates, owners, reminders, notifications, the "remind
  me" type, the Automate trigger, dashboards or property editors.
- Choosing where review data lives (Umbraco compositions and key-value
  store, or custom tables). That belongs to the first change that stores
  review data.
- Publishing: no publish workflow, no NuGet push, no Marketplace listing, no
  repository URL in package metadata until the repository's public home is
  decided.
- Supporting the Umbraco 18 STS line.

## Capabilities

### New Capabilities

- `package-composition`: which packages exist, what each one contains and
  depends on, how a site installs the product, the Automate isolation rule,
  the Umbraco version range, and the version a site sees in the backoffice.
- `continuous-integration`: what a CI run verifies and when it counts as
  green.

### Modified Capabilities

None. There are no existing specs.

## Impact

- **New dependencies.** The shipped ones are listed, with their licences and
  which package declares each, in the package-composition spec
  (*Shipped packages declare only named, licensed dependencies*):
  - the `Umbraco.Cms` 17.x family (MIT)
  - `Umbraco.Automate.Core` 17.x (MIT, Automate package only)
  - three security pins on packages already in the resolved graph, raised
    to fixed versions:
    - `System.Security.Cryptography.Pkcs` (MIT, declared by Core)
    - `MessagePack` (MIT, Backoffice and Automate)
    - `OpenTelemetry.Api` (Apache-2.0, Automate only, reached through
      Umbraco.Automate.Core → WorkflowCore)

  These build- or test-only dependencies never ship:
  `@umbraco-cms/backoffice` (MIT), Vite (MIT), TypeScript (Apache-2.0), xunit
  v3 and its runner (Apache-2.0), Microsoft.NET.Test.Sdk (MIT), and the full
  `Umbraco.Automate` package (MIT, TestSite only). Developers need Node 22
  alongside the .NET 10 SDK.
- **Public API surface:** two composer classes, which are public because
  Umbraco discovers composers by type scanning. Packages are versioned
  `17.0.0-alpha.N` until a first release, so nothing here is a compatibility
  promise yet.
- **No schema**, so there are no migrations.
- CLAUDE.md gets real build and test commands once this change is archived.
