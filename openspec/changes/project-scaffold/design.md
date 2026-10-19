## Context

The repository holds only OpenSpec and Claude configuration (see proposal.md,
*Why*). The sibling repository `D:\Applications\NDD\UmbracoPackages\uBookIt`
already solves the same plumbing on the same stack: an aggregate package, an
MSBuild-built Lit client, manifest version stamping, and CI that proves every
suite ran. This design reuses those patterns, minus the parts that exist
there because uBookIt is bigger: a delivery API, a front-end rendering
assembly, a separate persistence assembly, SQL Server integration tests, and
two release lines.

Versions as of 2026-10-19: `Umbraco.Cms` 17.7.1 is the latest 17.x release.
`Umbraco.Automate.Core` 17.5.1 is the latest 17.x release (18.x targets
Umbraco 18) and needs Umbraco ≥ 17.6.0. Umbraco.Automate is MIT-licensed
(github.com/umbraco/Umbraco.Automate).

## Goals / Non-Goals

**Goals:**
- Every project builds and packs from a clean clone with one `dotnet build` /
  `dotnet pack`.
- The structural invariants (one install package, Automate confined to its
  own package, Umbraco < 18, version stamping) are enforced by tests, not by
  review alone.
- The TestSite boots to an Umbraco 17 backoffice with Automate installed, so
  the next feature change can be exercised by hand straight away.

**Non-Goals:**
- Choosing where review data lives. The candidates include Umbraco's own
  mechanisms (a composition carrying review properties, plus Umbraco's
  key-value store for bookkeeping) as well as custom tables. Storing nothing
  of our own is the leading option, not a requirement. That's decided in the
  first change that stores review data, so Core gets no persistence folder
  here.
- Any publish or release automation.

## Decisions

### D1. Four packable projects, not uBookIt's five

| Project | Packs | References |
|---|---|---|
| `…Core` | yes | Umbraco.Cms.Core / Infrastructure |
| `…Backoffice` | yes | Core, Umbraco.Cms.Api.Management, Web.Common |
| `…Automate` | yes | Core, Umbraco.Automate.Core |
| `NDD.Umbraco.ContentReview` | yes (no code) | Backoffice (and so Core) |
| `…TestSite` | no | the aggregate, Automate, Umbraco.Cms, Umbraco.Automate |
| `tests/…Tests` | no | whatever it tests |

Whatever storage the package ends up with lives inside Core, under its own
folder and namespace, rather than in a separate assembly. The separation of
concerns that matters here is domain/storage vs. backoffice vs. Automate. A
separate persistence package earned its keep in uBookIt because of EF Core's
weight and its migration assembly. This package's storage is expected to be
small, possibly just Umbraco's own content properties, so it doesn't need its
own package.

There is no `Web` project because nothing renders on the front end. If a
later change adds front-end output (for example a "last reviewed" tag
helper), it adds the project then.

*Alternatives:* a single assembly (rejected: the Automate rule needs an
assembly boundary, and backoffice vs. domain is the boundary every later
change will lean on); uBookIt's full layout (rejected: empty projects with no
reason to exist).

### D2. Automate is a sibling package, not a dependency of the aggregate

The aggregate leaves Automate out, so a site without Automate never downloads
it or WorkflowCore, Jint and the rest of its graph. Sites that use Automate
install `NDD.Umbraco.ContentReview.Automate` as well. Later, Core will raise
an Umbraco notification for "review due". The Automate project will map that
notification to a trigger (Automate's `NotificationTriggerBase`), so Core
needs no knowledge of Automate.

*Alternative:* referencing Automate from the aggregate and no-op'ing when
it's not configured. Rejected because it forces the dependency on every site,
and it's the opposite of invariant 3.

### D3. Version and dependency ranges

- `Version` in `Directory.Build.props`: `17.0.0-alpha.1`. The major tracks
  Umbraco, as in uBookIt.
- `Directory.Packages.props` uses central package management with floating
  ranges enabled, as uBookIt does: `Umbraco.Cms.*` at `[17.7.1,18.0.0)` and
  `Umbraco.Automate*` at `[17.5.1,18.0.0)`. The floor is the current release
  rather than 17.0.0. It's alpha, there are no existing installs to protect,
  and a low floor would make us chase advisories that later 17.x releases
  fix.
- **Security pins.** uBookIt pins `System.Security.Cryptography.Pkcs` 10.0.13
  and `MessagePack` 3.1.9 because Umbraco 17.7.1's graph resolves vulnerable
  versions. The same graph applies here. Apply adds a pin only where
  `dotnet list package --vulnerable --include-transitive` shows a packable
  project resolving a vulnerable version, using uBookIt's comment and *EXIT*
  wording, and doesn't copy the pins blindly. The scan found three:

  | Pin | Licence | Declared by | Why |
  |---|---|---|---|
  | `System.Security.Cryptography.Pkcs` 10.0.13 | MIT | Core (the others reach it through Core) | GHSA-846f-f764-76mx, GHSA-2fmv-rv2c-59x7 |
  | `MessagePack` 3.1.9 | MIT | Backoffice, Automate | GHSA-qhrr-8q5h-9q3h, via Umbraco.Cms.Web.Common |
  | `OpenTelemetry.Api` 1.15.3 | Apache-2.0 | Automate only | GHSA-g94r-2vxg-569j, via Umbraco.Automate.Core → WorkflowCore 3.9.0 (1.1.0). Same major, so WorkflowCore is unaffected |

  The aggregate and Automate also declare the ASP.NET Core shared framework,
  so the SDK prunes `System.Security.Cryptography.Xml` and sites run the
  framework's copy, as in uBookIt. Each pin is a listed dependency in the
  package-composition spec, and a test enforces the list.

### D4. Client build and manifest stamp copied from uBookIt

`…Backoffice.csproj` gets uBookIt's three targets, renamed:
1. build the client (incremental, `npm ci` only when `node_modules` is
   missing, `ContentReviewBuildClient=false` to skip it),
2. stamp `"version": "0.0.0"` in `Client/public/umbraco-package.json` with
   `$(Version)`, with an `Error` if the placeholder is gone,
3. add freshly built `wwwroot/**` to `@(Content)` before static web asset
   discovery.

The client lives at `src/NDD.Umbraco.ContentReview.Backoffice/Client`
(Vite library mode, ES module, `@umbraco*` external). It outputs to
`wwwroot/App_Plugins/NDDContentReview/`, which is gitignored. The manifest id
is `NDD.Umbraco.ContentReview` with the name "Content Review", and it
registers a single `bundle` extension whose `bundle.manifests.ts` exports an
empty array. That's enough for the backoffice to load the bundle and list the
package.

The manifest sets `"allowTelemetry": true`. That lets a site whose own
Umbraco telemetry is switched on report this package's name and version to
Umbraco, and nothing more. A site that opts out of telemetry reports
nothing. Kept on, as uBookIt does, because install counts are the only
adoption signal a free package gets. Turning it off later is a one-line
change.

### D5. Namespaces include `Umbraco`, so Umbraco types use `using` only

The root namespace is the package ID, `NDD.Umbraco.ContentReview`. Inside it,
a qualified name such as `Umbraco.Cms.Core.Composing.IComposer` resolves
`Umbraco` to `NDD.Umbraco` and fails to compile. Code therefore imports
Umbraco types with file-level `using` directives, which resolve from the
global namespace, or writes `global::Umbraco…`, and never uses partly
qualified names. This is recorded in CLAUDE.md after archive. It's the price
of the naming convention you chose (`Our.Umbraco.*` packages live with the
same thing).

### D6. TestSite runs on SQLite with unattended install

The TestSite is `Microsoft.NET.Sdk.Web` with Umbraco 17's standard
`Program.cs`. `appsettings.Development.json` holds the
`Umbraco:CMS:Unattended` settings (a throwaway local admin account), so
`dotnet run` gives a working backoffice with no manual install. Automate
shares the CMS database (`Umbraco:Automate:UseNamedConnectionString:
umbracoDbDSN`), because it refuses to start without a connection string of
its own.

The SQLite connection string and `Imaging:HMACSecretKey` live in the
TestSite's user secrets, not in any committed file. A connection string in
appsettings tends to get edited in place, and later committed, when someone
points the site at another database. Umbraco writes an HMAC key into
`appsettings.json` whenever none is configured, so a clone that hasn't set
the secret yet will see a new key appear there. Don't commit it: move it to
user secrets. CLAUDE.md gives the `dotnet user-secrets` commands. The site
adds `Umbraco.Automate` (the full package, which brings its UI and storage)
so the Automate project can be exercised. `umbraco/` data and logs are
gitignored. Which database engines the *package* supports is decided with
persistence (see Non-Goals).

### D7. Tests read packed nuspecs and project files, not prose

`tests/NDD.Umbraco.ContentReview.Tests` (xunit v3) holds:
- **The `PackedPackages` assembly fixture** copies the repository with no
  build output (no `bin`/`obj`, `node_modules` or built client) and packs
  that copy once per run. That makes the clean-clone scenario the real test.
- **Composition tests** read each packed `.nuspec` and check:
  - the dependency closure from the aggregate, and that no package except
    Automate reaches `Umbraco.Automate*`
  - the upper bounds of the Umbraco ranges
  - the net10.0-only targets and `lib/` folders
  - third-party dependencies against the spec's allow-list
  - the shared metadata
  - the client and stamped version in the Backoffice package
- **Project-graph test** parses every `*.csproj` under `src/` with
  `System.Xml.Linq`. It walks `ProjectReference` and `PackageReference` to
  enforce the Automate rule at source level as well. That catches a
  violation before anyone packs.
- **Workflow tests** line-scan `.github/workflows/*.yml|*.yaml`. They check:
  - every `uses:` names a 40-hex SHA
  - the push and pull-request triggers
  - `CI: 'true'`, the solution build and the results check
  - read-only permissions

  A line scan is enough for one hand-written workflow, where uBookIt needed
  YAML parsing (see Risks).
- **Build-behaviour tests** run builds inside the clean copy:
  - the manifest stamp, in both its failing and its succeeding path,
    including `;` and `%3B` in the manifest
  - a whole-solution build with no Node on the PATH
  - warnings-as-errors under `CI=true` but not locally, using a probe
    project with a nullable warning

  The no-Node test deletes the copy's built client. It's the only test that
  changes the shared copy, and `PackedPackages.CopyDirectory` documents
  that.

### D8. CI: one job, ubuntu, no database

`.github/workflows/ci.yml` runs one `verify` job on `ubuntu-24.04` with
`permissions: contents: read`. Its steps: checkout, setup-dotnet from
`global.json`, setup-node 22 with npm cache, `npm ci` in the client,
`dotnet build -c Release` with `CI=true`, and `dotnet test` writing TRX. A
small PowerShell script (`scripts/ci/Assert-TestResults.ps1`, adapted from
uBookIt) then fails the run on any missing test project or skipped test.
Actions are pinned to the same SHAs uBookIt uses today.

`global.json` pins the SDK to `10.0.300` with `rollForward: latestPatch`, as
uBookIt does. The machine's installed 10.0.301 satisfies that.

## Risks / Trade-offs

- [Running `dotnet pack` inside tests is slow, and needs the client built] →
  Pack once per test run in a shared fixture. The Backoffice client output is
  already incremental. On a host without Node, pack tests fail with a clear
  message instead of passing vacuously.
- [The `uses:` line scan misses exotic YAML forms] → Accepted while there is
  one workflow written by us. Switch to a YAML parser when a second workflow
  or a publish workflow arrives.
- [Automate 17.x might stop receiving releases before Umbraco 17 reaches
  EOL] → The isolation means only the Automate package is affected. Core and
  the aggregate keep shipping.
- [Floor of 17.7.1 excludes sites on earlier 17.x] → Acceptable for an alpha
  package. Revisit when a first stable release is cut.
- [`D5` namespace trap will bite whoever forgets] → It fails at compile time,
  not at run time, and CLAUDE.md records it.
