## Purpose

Defines what an automated CI run verifies for NDD.Umbraco.ContentReview, and
when a run counts as green, so that a passing run means the code builds
cleanly and every test suite actually ran.

## ADDED Requirements

### Requirement: Every push and pull request is verified

CI SHALL run on every push to any branch and on every pull request targeting
`main`, and SHALL build the solution, including the backoffice client, from a
clean checkout.

#### Scenario: Push to a feature branch
- **WHEN** a commit is pushed to any branch
- **THEN** a CI run starts for that commit

#### Scenario: Pull request into main
- **WHEN** a pull request targeting `main` is opened or updated
- **THEN** a CI run starts for the pull request's head

### Requirement: Warnings fail CI

A CI build SHALL treat compiler and analyser warnings as errors, except NuGet
vulnerability advisories (NU1901–NU1904). A local build run with the same
CI flag SHALL behave identically.

#### Scenario: Nullable warning
- **WHEN** a commit introduces a nullable-reference warning
- **THEN** the CI run fails

#### Scenario: Reproducing CI locally
- **WHEN** a developer runs the build with `CI=true`
- **THEN** warnings are errors exactly as in CI

### Requirement: Green means every suite ran

A CI run SHALL fail when any test fails, when any test project in the
repository produced no results, or when any test was skipped.

#### Scenario: Test project silently not run
- **WHEN** a test project exists on disk but reports no results in the run
- **THEN** the CI run fails, naming the project

#### Scenario: Skipped test
- **WHEN** a test is skipped during the run
- **THEN** the CI run fails

### Requirement: CI is least-privileged and pinned

CI SHALL run with read-only repository permissions and SHALL pin every
third-party action to a full commit SHA.

#### Scenario: Action referenced by tag
- **WHEN** the workflow references a third-party action by tag or branch instead of a commit SHA
- **THEN** the test suite fails, naming the action
