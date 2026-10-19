## Purpose

Defines which NuGet packages make up NDD.Umbraco.ContentReview, what each one
contains and depends on, and how a site installs the product. It keeps the
Umbraco Automate integration optional, and keeps the version a site sees
truthful.

## ADDED Requirements

### Requirement: One package installs the product

The product SHALL be installed by adding a single NuGet package,
`NDD.Umbraco.ContentReview`, which contains no code of its own and depends on
every packable library except the Automate integration. Installing it SHALL
give a site everything the product needs to work without Umbraco Automate.

#### Scenario: Aggregate reaches the libraries
- **WHEN** the solution is packed
- **THEN** the `NDD.Umbraco.ContentReview` package declares dependencies that reach `NDD.Umbraco.ContentReview.Core` and `NDD.Umbraco.ContentReview.Backoffice`

#### Scenario: A new library cannot be forgotten
- **WHEN** a packable library project is added to the solution and the aggregate does not reach it
- **THEN** the test suite fails, naming the unreached project, unless that project is the Automate integration

#### Scenario: Aggregate does not bring Automate
- **WHEN** the `NDD.Umbraco.ContentReview` package's dependency graph is examined
- **THEN** it contains neither `NDD.Umbraco.ContentReview.Automate` nor any `Umbraco.Automate*` package

### Requirement: Umbraco Automate is confined to one package

Only the `NDD.Umbraco.ContentReview.Automate` package SHALL depend on any
`Umbraco.Automate*` package, directly or through another project. A site
SHALL be able to install `NDD.Umbraco.ContentReview` without Umbraco
Automate, and SHALL be able to add Automate integration by also installing
`NDD.Umbraco.ContentReview.Automate`.

#### Scenario: Automate reference outside the integration project
- **WHEN** any project other than `NDD.Umbraco.ContentReview.Automate`, `NDD.Umbraco.ContentReview.TestSite` or a test project references an `Umbraco.Automate*` package or the Automate integration project
- **THEN** the test suite fails, naming the offending project

#### Scenario: Integration package depends on the core
- **WHEN** the solution is packed
- **THEN** the `NDD.Umbraco.ContentReview.Automate` package depends on `NDD.Umbraco.ContentReview.Core` and on an `Umbraco.Automate` 17.x package

### Requirement: Packages target the Umbraco 17 LTS line only

Every shipped package SHALL target .NET 10, and SHALL declare its Umbraco
dependencies (and Umbraco Automate, where present) with an upper bound that
excludes major version 18. The package version's major number SHALL equal
the targeted Umbraco major.

#### Scenario: Umbraco 18 cannot be resolved
- **WHEN** a packed nuspec's Umbraco or Umbraco Automate dependency ranges are read
- **THEN** each range has an upper bound that admits no `18.x` version

#### Scenario: Version major tracks Umbraco
- **WHEN** the packages are built
- **THEN** every package's version begins `17.`

#### Scenario: Libraries target .NET 10
- **WHEN** the packed nuspecs are read
- **THEN** every package's dependency groups target `net10.0` only, and every package that ships an assembly ships it under `lib/net10.0/`

### Requirement: Shipped packages declare only named, licensed dependencies

A shipped package SHALL declare a third-party dependency only if it is
listed here for that package, with a licence compatible with MIT. Adding one
SHALL require changing this requirement.

| Dependency | Licence | May be declared by |
|---|---|---|
| `Umbraco.Cms.*` (17.x) | MIT | Core, Backoffice |
| `Umbraco.Automate.Core` (17.x) | MIT | Automate |
| `System.Security.Cryptography.Pkcs` (security pin) | MIT | Core |
| `MessagePack` (security pin) | MIT | Backoffice, Automate |
| `OpenTelemetry.Api` (security pin) | Apache-2.0 | Automate |

Dependencies on the package's own libraries (`NDD.Umbraco.ContentReview*`)
are not third-party.

#### Scenario: Unlisted dependency
- **WHEN** a packed package declares a third-party dependency that is not listed for it above
- **THEN** the test suite fails, naming the package and the dependency

### Requirement: Shipped packages carry complete metadata

Every shipped package SHALL carry the same version, the MIT licence
expression, the authors "Norwood Design & Development Ltd.", a README and an
icon, and a description specific to that package. The development site and
test projects SHALL NOT be packable.

#### Scenario: Metadata on every package
- **WHEN** the solution is packed
- **THEN** each produced package has licence `MIT`, the shared version, a README and an icon, and no two packages have the same description

#### Scenario: Non-shipped projects do not pack
- **WHEN** the solution is packed
- **THEN** no package is produced for `NDD.Umbraco.ContentReview.TestSite` or any test project

### Requirement: The backoffice package is self-contained

The `NDD.Umbraco.ContentReview.Backoffice` package SHALL contain its built
backoffice client and an `umbraco-package.json` manifest. Packing from a
clean clone, with no client previously built, SHALL produce the same client
content as packing from a working copy.

#### Scenario: Clean-clone pack includes the client
- **WHEN** the solution is packed from a checkout in which the client has never been built
- **THEN** the Backoffice package contains the `umbraco-package.json` manifest and the client bundle it names

#### Scenario: Building without Node
- **WHEN** a build is run with the client build explicitly disabled
- **THEN** the .NET projects still build, so tests can run on a host without Node

### Requirement: The backoffice shows the true package version

A site with the package installed SHALL list it in the backoffice Packages
screen under the name "Content Review", with the version of the installed
package, and the backoffice SHALL load its bundle without errors.

#### Scenario: Version in the Packages screen
- **WHEN** a site runs a build of version X of the package and an administrator opens the Packages screen
- **THEN** the package is listed with version X, not a placeholder such as `0.0.0`

#### Scenario: Manifest stamp cannot fail silently
- **WHEN** the manifest source no longer contains the placeholder that the build replaces with the version
- **THEN** the build fails with an error rather than shipping an unstamped manifest
