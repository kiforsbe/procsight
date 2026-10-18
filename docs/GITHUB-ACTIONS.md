# Build ProcSight on GitHub

The [Windows build and MSI workflow](../.github/workflows/windows-build.yml) builds on GitHub
hosted Windows runners, uploads an unsigned self-contained x64 MSI, and prepares a draft GitHub
Release with the MSI and hash manifest attached. No personal tokens or signing secrets are needed.

1. Push the committed workflow and source to your GitHub repository's default branch. Enable
   Actions in Settings if organizational policy has disabled them. This work does not push.
2. Create your versioned release locally with `prepare-release.ps1`, then explicitly push
   that release commit and its matching tag to GitHub when ready. The workflow never creates tags.
3. Open **Actions → Windows build and MSI → Run workflow**, leave the workflow branch on the
   default branch, enter the existing **tag**, for example `v0.2.0`, and start it. The manual-run
   UI requires the workflow to exist on the default branch first. The build source comes from
   your tag, not the branch selected for the workflow definition.
4. Open the successful run and download **ProcSight-<version>-windows-x64-<run>-<attempt>**
   under **Artifacts**. GitHub login is required. Extract its ZIP and open the `.msi`.
   The adjacent `release-artifacts.json` contains version, source commit and SHA-256.
5. Open **Releases**, find the draft for the selected tag, review the MSI and hash manifest,
   edit the starter release notes (using the matching changelog entry), and select **Publish release**
   when ready. Until publication, the draft is visible only to repository users with appropriate access.

**Manual only:** pushes, tags and pull requests never trigger this workflow. It has only
`workflow_dispatch`; it does not run on a schedule or as a follow-up to another workflow.
The required tag must be stable `vMAJOR.MINOR.PATCH`, exist on GitHub, and match the tagged
Version.props and manifests. Missing tags and version mismatches fail before compilation.
An untagged branch tip cannot be selected as the build source.

Workflow artifacts expire after 14 days (subject to repository policy); attached release assets
are separate downloads. Neither is a Store submission. The unsigned MSI requires no certificate import,
but Windows reputation/Smart App Control or device policy can still block it. An artifact contains code from the requested tag and must not be treated as a trusted release. Builds use the central version;
CI does not independently bump it, so different commits may produce the same MSI product version.
Use the existing local release-preparation flow for versioned upgrades.

## Execution

The runner is `windows-2025`, whose [image inventory](https://github.com/actions/runner-images/blob/main/images/windows/Windows2025-Readme.md)
includes Visual Studio and Windows SDK 10.0.26100. The setup step honors the tagged global.json's stable
SDK/roll-forward policy. PowerShell runs `scripts/verify.ps1 -Configuration Release`, followed
by `scripts/package.ps1 -Profile Msi`. Existing package auditing remains fail-closed. There is
no NuGet cache shared between builds, custom installer, cloud VM setup or self-hosted runner.
The installer bundles required app runtimes; the build does not launch the WinUI app.

The build job has a 45-minute timeout, cancels older runs for the same tag and uses read-only
repository permissions. Checkout does not persist credentials. All Actions are full-SHA pinned.
The upload stage accepts only the inspected unsigned MSI and manifest, checks the staged hash,
and excludes build logs, databases, local metadata and intermediate output. No private secrets,
OAuth session, signing certificate, version rewrite, commit, tag or public release publication is used.
Automated auth/provider tests use synthetic handlers/stores; no actual account sign-in is run.

The separate 10-minute draft-release job has `actions: read` and `contents: write`. It uses the
runner-provided [GitHub CLI](https://cli.github.com/manual/gh_release_create) and automatic
`GITHUB_TOKEN`; it performs no checkout and executes no tagged code or third-party Actions.
It downloads only this run's exact named artifact, revalidates the two filenames, installer hash,
version/source commit and remote tag, and creates the release with `--draft --verify-tag`.
An existing draft can receive assets if these names are absent; its notes remain unchanged.
Published releases and colliding asset names cause a clear failure, with no replacement or
automatic deletion. After a partial upload or a rebuild, inspect the draft and explicitly remove
the old MSI/manifest before rerunning the failed job or workflow. Never delete an asset blindly.
No new tag, release version or public release is created by this job.

If repository/organization policy rejects the write permission, an administrator must allow
the workflow's requested `contents: write`; do not work around it with a personal token.
Synthetic CLI tests live in [test-github-release.ps1](../scripts/test-github-release.ps1) and
run through verify.ps1. They cover draft creation/upload and refusal/failure paths without any
credentials, network calls, release creation or asset changes. The release job's workflow-owned
logic works with existing tags that predate this job; the tagged source need not contain it.

Hosted CI is not Windows 11 desktop acceptance. Interactive UI, accessibility, real GPU/ETW,
installer UAC and installed lifecycle tests remain separate. A successful workflow has not yet
been observed; local workflow lint and command checks do not establish hosted-run success.

## Action dependency review

Reviewed against live upstream releases, licenses and production package-lock trees on 2026-10-18.
The workflow uses the latest stable releases listed by each official repository at review time:

| Official Action | Stable release | License | Production lock entries | Npm audit affected packages (high) |
| --- | --- | --- | --- | --- |
| actions/checkout | 7.0.1 | MIT | 24 | 1 |
| actions/setup-dotnet | 6.0.0 | MIT | 65 | 3 |
| actions/upload-artifact | 7.0.2 | MIT | 148 | 5 |

Exact immutable commit pins are beside the versions in the workflow. Release/license sources:
[checkout](https://github.com/actions/checkout/releases),
[setup-dotnet](https://github.com/actions/setup-dotnet/releases),
[upload-artifact](https://github.com/actions/upload-artifact/releases).
Production `npm audit --omit=dev --package-lock-only --ignore-scripts --json` reported the
following known upstream findings; these are retained as explicit, constrained build-tool
exceptions, not silently treated as clean dependencies:

- All three lock trees include affected **undici**, including
  [response desynchronization](https://github.com/advisories/GHSA-8xcm-r25x-g524) and related
  HTTP/cookie/WebSocket findings. Our Action inputs use app-owned values and official service
  endpoints; no custom network/request-body/header options are configured. This reduces exposed
  inputs but is not a complete reachability proof for the bundled code.
- setup-dotnet/upload-artifact include affected **brace-expansion**, with
  [resource-exhaustion findings](https://github.com/advisories/GHSA-mh99-v99m-4gvg).
  Cache is disabled and upload uses one fixed staging path, not user-controlled glob patterns.
- setup-dotnet includes **fast-xml-parser** with a
  [DOCTYPE limit bypass](https://github.com/advisories/GHSA-8r6m-32jq-jx6q).
  upload-artifact includes older affected fast-xml-parser/fast-xml-builder and lodash versions.
  A manually selected tag can contain arbitrary build code; no privileged follow-up consumes
  these artifacts and no signing/release secrets are exposed.

Audit counts describe affected package entries, not independent exploits. Lock files do not
prove which modules are present/reachable in upstream bundled Action code. Latest stable is not
a security guarantee; recheck fixes/advisories before updating the full-SHA pins. Runtime NuGet
advisory rejection is unchanged and these JavaScript tools are not bundled into ProcSight's MSI.

Declared production license families are MIT/MIT-X11, ISC, Apache-2.0, BSD-3-Clause, 0BSD and
BlueOak-1.0.0 (including combined Apache/BSD terms). Registry metadata confirmed missing lock
license fields for json5 2.2.3 and tunnel 0.0.6 as MIT. Legacy buffers 0.1.1 has no license
declaration in its registry metadata or published tarball; that transitive declaration remains
unresolved. This review does not claim full license clearance of upstream Action bundles.
The workflow references vendor-hosted tools; it does not redistribute those tools in the app,
and the project license remains AGPL v3.0. No product package pins/lock files were changed.
