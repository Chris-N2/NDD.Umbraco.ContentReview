## 1. Repository and build files

- [x] 1.1 Add `global.json` (SDK 10.0.300, `latestPatch`), `.gitattributes`, `LICENSE` (MIT, Norwood Design & Development Ltd.), and gitignore entries for `wwwroot/App_Plugins/`, `node_modules/`, TestSite `umbraco/Data`, `umbraco/Logs` and `*.db`; verify `dotnet --version` resolves a 10.0.3xx SDK in the repo root
- [x] 1.2 Add `Directory.Build.props` (nullable, implicit usings, `TreatWarningsAsErrors` when `CI`/`TF_BUILD` is true except NU1901–NU1904, `Version` 17.0.0-alpha.1, authors/company/copyright, MIT, README and icon packing guarded by `IsPackable`, symbols as snupkg); verify by building an empty project in task 2.1
- [x] 1.3 Add `Directory.Packages.props` with central management and floating versions, `Umbraco.Cms.*` at `[17.7.1,18.0.0)`, `Umbraco.Automate*` at `[17.5.1,18.0.0)`, and xunit v3 / runner / Microsoft.NET.Test.Sdk; verify restore succeeds once projects exist
- [x] 1.4 Add a placeholder `assets/icon.png` (128×128) and a `README.md` stating what the package is, the scope (review dates, owners and reminders only), install instructions (`NDD.Umbraco.ContentReview`, plus `.Automate` for Automate users) and the Umbraco 17 requirement; verify both pack in task 5.2

## 2. Projects

- [x] 2.1 Create `src/NDD.Umbraco.ContentReview.Core` (net10.0, Umbraco.Cms.Core and Infrastructure) with a `ContentReviewComposer` that registers nothing, using file-level `using` directives for Umbraco types (design D5); verify `dotnet build` succeeds
- [x] 2.2 Create `src/NDD.Umbraco.ContentReview.Backoffice` (Razor SDK, `StaticWebAssetBasePath` `/`, references Core, Umbraco.Cms.Api.Management and Web.Common, framework reference to ASP.NET Core); verify `dotnet build` succeeds
- [x] 2.3 Create `src/NDD.Umbraco.ContentReview.Automate` referencing Core and `Umbraco.Automate.Core`, with an empty composer; verify `dotnet build` succeeds and restore resolves Automate 17.x, not 18.x
- [x] 2.4 Create the aggregate `src/NDD.Umbraco.ContentReview` (no build output, NU5128 suppressed, `umbraco-marketplace` tag appended, references Backoffice only); verify `dotnet pack` yields a nuspec depending on Backoffice
- [x] 2.5 Give each packable project its own `Title` and `Description`, and set `IsPackable=false` on TestSite and tests; verify no two descriptions match in task 5.2
- [x] 2.6 Create `NDD.Umbraco.ContentReview.slnx` listing every project under `/src/` and `/tests/`; verify `dotnet build NDD.Umbraco.ContentReview.slnx` succeeds with zero warnings under `CI=true`
- [x] 2.7 Check transitive vulnerabilities with `dotnet list package --vulnerable --include-transitive`, and add Pkcs/MessagePack-style security pins (with uBookIt's comment and EXIT wording) only to packable projects that resolve a vulnerable version (design D3); verify the command reports no vulnerable packages for packable projects

## 3. Backoffice client

- [x] 3.1 Create `Client/` with `package.json` (`@umbraco-cms/backoffice` ^17, typescript, vite; `build` = `tsc && vite build`), `tsconfig.json`, `vite.config.ts` (library mode, ES, `@umbraco` external, output `../wwwroot/App_Plugins/NDDContentReview`, sourcemaps), and `src/bundle.manifests.ts` exporting an empty manifest array; verify `npm ci && npm run build` emits the bundle
- [x] 3.2 Add `Client/public/umbraco-package.json` (id `NDD.Umbraco.ContentReview`, name "Content Review", version `0.0.0` placeholder, one `bundle` extension pointing at the built JS); verify it is copied to the output folder by the build
- [x] 3.3 Port uBookIt's three MSBuild targets (build client, stamp manifest version with an `Error` on a missing placeholder, add `wwwroot/**` to `@(Content)`), renamed with a `ContentReview` prefix and a `ContentReviewBuildClient` escape hatch; verify deleting `wwwroot/App_Plugins` then `dotnet pack` produces a Backoffice package containing the bundle and a manifest with version `17.0.0-alpha.1`
- [x] 3.4 Verify the stamp guard: temporarily change the placeholder and confirm the build fails with the stamp error, then revert

## 4. Test site

- [x] 4.1 Create `src/NDD.Umbraco.ContentReview.TestSite` (Web SDK, Umbraco.Cms, Umbraco.Automate, project references to the aggregate and Automate projects) with Umbraco 17's standard `Program.cs`, and unattended install on SQLite in `appsettings.Development.json`; verify `dotnet run` boots and the backoffice login page loads
- [x] 4.2 Log in to the TestSite backoffice and open Settings → Packages; verify "Content Review" is listed with version `17.0.0-alpha.1`, the Automate section is present, and the browser console shows no errors from the bundle

## 5. Tests

- [x] 5.1 Create `tests/NDD.Umbraco.ContentReview.Tests` (xunit v3) with a repo-root locator helper; verify `dotnet test` discovers the project
- [x] 5.2 Add composition tests over freshly packed nuspecs (shared fixture packing once per run): the aggregate reaches every packable library except Automate, no package except Automate reaches `Umbraco.Automate*`, Automate depends on Core and Automate 17.x, every Umbraco/Automate range has an upper bound ≤ 18.0.0, every version starts `17.`, there is shared metadata with distinct descriptions, TestSite and tests produce no package, and the Backoffice package's manifest carries the package version; verify each test fails when its rule is broken by a temporary edit, then passes
- [x] 5.3 Add the project-graph test (csproj parse of `ProjectReference`/`PackageReference`) enforcing the Automate rule at source level; verify it fails when Core is given a temporary Automate reference
- [x] 5.4 Add the workflow test requiring every `uses:` in `.github/workflows/*.yml` to name a 40-hex SHA; verify it fails on a temporary `@v4` reference
- [x] 5.5 (QA round 1) Add tests for the CI triggers, `CI: 'true'` in the workflow, read-only permissions across `*.yml`/`*.yaml`, warnings-as-errors under `CI=true` only (probe project with CS8602), net10.0-only targets in the packed nuspecs, and third-party dependencies against the spec's allow-list; verify each fails under a temporary breaking edit (unlisted Pkcs on Backoffice, `CI: 'true'` removed, push narrowed to `main`, a `lib/net9.0` file in Core)
- [x] 5.6 (QA round 1) Extend the build-behaviour tests: the manifest stamp preserves `;` and `%3B`, and the whole solution builds with no Node on the PATH from a state with no built client; verify both pass, and that the stamp test passes with and without an `Escape` around `Lines` (it isn't needed)

## 6. Continuous integration

- [x] 6.1 Add `.github/workflows/ci.yml` (push on all branches, PRs to `main`, `contents: read`, ubuntu-24.04, setup-dotnet from global.json, setup-node 22 with npm cache, `npm ci`, `CI=true dotnet build -c Release`, `dotnet test` with TRX output), with actions pinned to the SHAs uBookIt uses; verify the workflow test (5.4) passes
- [x] 6.2 Add `scripts/ci/Assert-TestResults.ps1` (adapted from uBookIt) that fails when any test project on disk has no TRX, or any test failed or was skipped, and call it from the workflow; verify locally by running it against a results folder missing one project's TRX and against a complete one
- [x] 6.3 Run the full CI sequence locally (`CI=true dotnet build -c Release`, `dotnet test`, the assert script); verify it passes with zero warnings

## 7. Documentation

- [x] 7.1 Update `CLAUDE.md`: remove the pre-scaffold status, and add real build, test, single-test (`dotnet test --filter`), client build and TestSite commands, the D5 namespace rule, and the actual project layout; verify every command listed in it runs successfully
