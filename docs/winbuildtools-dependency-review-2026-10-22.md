# winbuildtools integration dependency review

Reviewed **2026-10-22**, with initial findings followed by the explicitly authorized
dependency update and validation below. This records live registry,
upstream and actual package-license findings; it is not a security certification,
license clearance or installed-lifecycle acceptance. Host software, licenses and application version were not changed by this review.

## Initial findings

1. **SQLite is behind a listed stable patch:** the solution resolves 3.54.0 while
   NuGet and SQLite upstream list **3.54.1**, released 2026-10-16. Update the central
   SQLite pin and regenerate applicable locks deliberately, then validate locked
   restore, tests, native payload and package checks. The 3.54.1 package was downloaded
   into ignored review artifacts and its actual nuspec/license inspected: no package
   dependencies, native `e_sqlite3` naming, public-domain license statement unchanged.
   No compatibility claim is made before those checks. See [upstream changes](https://sqlite.org/releaselog/3_54_1.html)
   and [the package](https://www.nuget.org/packages/SQLite/3.54.1).
2. **Benchmark locks are stale outside the solution scan.** Sampling resolves
   Diagnostics.NETCore.Client 0.2.661903, Logging.Abstractions 8.0.3 and
   DependencyInjection.Abstractions 8.0.2; their current central selections are
   0.2.750501 / 10.0.13 / 10.0.13. Rules resolves SQLitePCLRaw bundle/core/provider/lib
   2.1.13, while the application uses the coherent 3.0.5 bundle/config/core/provider
   plus SQLite native package. Regenerate these benchmark locks against the central
   graph and validate their own builds/runs. Do not upgrade the obsolete native
   `SQLitePCLRaw.lib.e_sqlite3` independently: follow the selected bundle's native
   dependency. No benchmark compatibility exception is approved by this report.
3. **The checkout host has a flagged Git prerequisite:** installed
   2.54.0.windows.1 is in the published vulnerable ranges for
   [wincred OAuth store heap overflow](https://github.com/git-for-windows/git/security/advisories/GHSA-v3h4-vrpr-rh9x)
   and [wincred erase heap overflow](https://github.com/git-for-windows/git/security/advisories/GHSA-5j7x-r6vf-jhq2).
   Triggering depends on use of that credential helper; this review did not inspect
   credential configuration or exercise the defects. Current stable upstream is
   [2.56.0.windows.3](https://github.com/git-for-windows/git/releases/tag/v2.56.0.windows.3).
   Update the installed Git through its normal managed mechanism; no host update
   was performed here. This is a host finding, not a bundled MSI payload dependency.
4. **PowerShell is behind servicing:** installed 7.6.5 meets the API floor but
   [7.6.8](https://github.com/PowerShell/PowerShell/releases/tag/v7.6.8) is the current
   stable release returned by upstream's release API. Microsoft requires the latest
   patch for support. The support-lifecycle page lags the release API, so its displayed
   patch number was not used as the current-version authority. Do not treat the
   shared 7.4+ prerequisite as permission to run unpatched 7.4.0. See
   [support policy](https://learn.microsoft.com/en-us/powershell/scripting/install/powershell-support-lifecycle).
5. **Redistribution conditions remain material.** Shared tools are MIT, but WinUI,
   Windows App Runtime and Windows SDK binaries carry Microsoft's own package terms.
   Their distribution requirements/restrictions must remain in notices and payload
   checks. This review does not grant or establish blanket AGPL compatibility. Keep
   the project's existing license and disclose this unresolved distribution review.

## Shared tooling revision, license and external graph

Live `git ls-remote` returned main and latest annotated tag `v0.3.0` at
**9a3bf8cf1d79d05946abbd020fe57ccec3d0ceb5**. Older tags are v0.1.0 and v0.2.0.
The GitHub Releases API returned no releases; `/releases/latest` returned 404.
Tags therefore establish the published source baseline, not a GitHub Release asset.
The public repository security-advisories endpoint returned an empty array. This
cannot reveal private/unpublished reports and is not proof of absence of defects.
The source-level certificate race and inspection/configuration findings in the
approved integration design remain actionable despite that empty advisory feed.

The reviewed shared source has **no NuGet, npm or external PowerShell module graph**,
no vendored native tools/runtimes, and its manual-only workflow uses native Git and
PowerShell rather than third-party Actions. Integration introduces a pinned source
checkout, not a new application runtime package. Preserve exact reachable commit
pinning and deliberate updates, never build-time fetching of a moving branch.

| External prerequisite | Ownership / compatibility / redistribution |
| --- | --- |
| Windows Installer Automation, MakeCab, Expand | Host Windows components, not distributed by winbuildtools; patched Windows owns their security and lifecycle. No portable package version to pin. |
| Windows SDK MakeAppx / SignTool | Installed SDK or consumer NuGet tooling; shared discovery floor 10.0.26100.0 is an API floor. ProcSight pins 10.0.28000.2705 build tools and 1.7.260930100 MSIX tools, both latest listed stable in registry review. SDK terms apply; do not ship build tools as app payload. |
| PowerShell | Consumer-installed supported stable patched release; MIT and upstream third-party notices apply to PowerShell itself. Host servicing finding above. |
| Git | Consumer-installed checkout tool; GPLv2 and bundled-component notices apply to Git's distribution. Host servicing/advisory finding above. |
| .NET SDK / runtime | Consumer-owned. ProcSight global.json selects SDK 10.0.301 with latestPatch, installed runtime is 10.0.13. Microsoft's current .NET 10 runtime patch is 10.0.13; preserve app's same-version runtime floor and debugger support. SDK feature-band movement is not required by shared tool adoption and was not validated here. |
| GitHub Windows hosted runner | Mutable managed infrastructure; no runner image or tool binary is redistributed. Consumer workflow Actions and their dependency/advisory exceptions remain a separate existing review surface. |

Actual shared `LICENSE`, `NOTICE.md`, `msi/LICENSE` and `msi/NOTICE.md` were inspected.
They preserve MIT copyright/permission/warranty terms and attribution to Agenda and
ProcSight extraction, with recorded copyright-holder relicensing authorization.
Retain those files in source distribution and add the pinned tools to ProcSight's
third-party source notices. MIT does not relicense Windows/platform components or
application payloads. References: [source license](https://github.com/kiforsbe/winbuildtools/blob/v0.3.0/LICENSE),
[provenance](https://github.com/kiforsbe/winbuildtools/blob/v0.3.0/NOTICE.md),
[shared dependency boundary](https://github.com/kiforsbe/winbuildtools/blob/v0.3.0/docs/DEPENDENCIES.md),
[.NET support](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).

## NuGet method and results

Executed the required commands against the configured nuget.org service:

```powershell
dotnet list ProcSight.slnx package --outdated --include-transitive --format json
dotnet list ProcSight.slnx package --vulnerable --include-transitive --format json
dotnet list ProcSight.slnx package --deprecated --include-transitive --format json
```

All completed successfully. Outdated reported SQLite 3.54.0 -> 3.54.1 in App,
Infrastructure and Infrastructure.Tests. Vulnerable and deprecated reported no
findings in the solution. No advisory-source error was bypassed or suppressed.
These are feed results, not complete security conclusions. The benchmarks are not
members of ProcSight.slnx; their tracked locks were independently inspected.

Independently enumerated **45 distinct package/version pairs across 39 IDs** from
all tracked locks (including benchmarks), fetched each ID's live NuGet registration
pages, and checked resolved-version listing, deprecation and vulnerability metadata
and newest listed non-prerelease version. All resolved versions were listed,
non-prerelease and had no deprecation or vulnerability entries at review time.
The inventory below includes benchmark-only older versions, so it is intentionally
larger than the earlier 38-pair solution inventory. Older versions are findings,
not silently accepted exceptions. Newer *unlisted* or prerelease versions were
excluded from the stable comparison. Central pins already match the latest listed
stable versions except SQLite; framework compatibility still requires validation.

Package license expressions/URLs came from actual restored `.nuspec` files, not
repository-license assumptions. Available license/NOTICE/THIRD-PARTY files were
read and archived (29 file instances), and the Windows SDK license URL was fetched
because BuildTools has URL-only license metadata. SPDX MIT/Apache expressions
refer to those standard terms; several packages do not embed a full license file.
License texts/notices are retained as distribution requirements, not replaced by
this summary. File-license findings: WinUI/Runtime/Base/Foundation/InteractiveExperiences
use Microsoft Windows App SDK terms; MSIX tools use Windows SDK terms; WebView2
uses BSD-style three-clause terms; SQLite's actual license states public domain.

The Windows App SDK terms permit package-binplaced code to be redistributed but
attach end-user/distributor protections and restrictions on changing the license
of Microsoft distributable code. SDK terms separately define distributable-code
and utility rights. This report does not resolve all distribution/legal questions.
WebView2 binaries require copyright/conditions/disclaimer in distribution materials;
SQLitePCLRaw Apache notices and applicable .NET/package third-party notices must
be retained. Test packages remain build/test inputs, not application payload.

The split Windows App SDK selections match the current stable umbrella package's
component dependencies; retaining those separate packages avoids adding unused
AI/ML/Search components. xUnit's official `mtp-off` variant remains appropriate to
the existing VSTest adapter/test SDK workflow. SQLitePCLRaw v3's actual 3.0.5 bundle
metadata supplies the selected `SQLite` native package; do not infer native package
IDs from older migration prose. References: [stable SDK component graph](https://www.nuget.org/packages/Microsoft.WindowsAppSDK/2.5.1),
[xUnit supported variants](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform),
[SQLitePCLRaw migration](https://github.com/ericsink/SQLitePCL.raw/blob/main/v3.md),
[Windows SDK license](https://aka.ms/WinSDKLicenseURL).

## Initial observed package inventory

Latest means latest listed stable package ID version at review time, not a claim
that moving a standalone transitive package is compatible.

| Package | Resolved | Latest listed stable | Actual nuspec license |
| --- | --- | --- | --- |
| Microsoft.Bcl.AsyncInterfaces | 10.0.13 | 10.0.13 | MIT |
| Microsoft.CodeCoverage | 18.11.0 | 18.11.0 | MIT |
| Microsoft.Data.Sqlite | 10.0.13 | 10.0.13 | MIT |
| Microsoft.Data.Sqlite.Core | 10.0.13 | 10.0.13 | MIT |
| Microsoft.Diagnostics.NETCore.Client | 0.2.661903 | 0.2.750501 | MIT |
| Microsoft.Diagnostics.NETCore.Client | 0.2.750501 | 0.2.750501 | MIT |
| Microsoft.Diagnostics.Tracing.TraceEvent | 4.0.0 | 4.0.0 | MIT |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.13 | 10.0.13 | MIT |
| Microsoft.Extensions.DependencyInjection.Abstractions | 8.0.2 | 10.0.13 | MIT |
| Microsoft.Extensions.Logging.Abstractions | 10.0.13 | 10.0.13 | MIT |
| Microsoft.Extensions.Logging.Abstractions | 8.0.3 | 10.0.13 | MIT |
| Microsoft.NET.Test.Sdk | 18.11.0 | 18.11.0 | MIT |
| Microsoft.TestPlatform.ObjectModel | 18.11.0 | 18.11.0 | MIT |
| Microsoft.TestPlatform.TestHost | 18.11.0 | 18.11.0 | MIT |
| Microsoft.Web.WebView2 | 1.0.4258.31 | 1.0.4258.31 | BSD-style (LICENSE.txt) |
| Microsoft.Win32.Registry | 5.0.0 | 5.0.0 | MIT |
| Microsoft.Windows.SDK.BuildTools | 10.0.28000.2705 | 10.0.28000.2705 | Windows SDK terms |
| Microsoft.Windows.SDK.BuildTools.MSIX | 1.7.260930100 | 1.7.260930100 | Windows SDK terms |
| Microsoft.WindowsAppSDK.Base | 2.0.4 | 2.0.4 | Windows App SDK terms (license.txt) |
| Microsoft.WindowsAppSDK.Foundation | 2.3.12 | 2.3.12 | Windows App SDK terms (license.txt) |
| Microsoft.WindowsAppSDK.InteractiveExperiences | 2.1.9 | 2.1.9 | Windows App SDK terms (license.txt) |
| Microsoft.WindowsAppSDK.Runtime | 2.5.1 | 2.5.1 | Windows App SDK terms (license.txt) |
| Microsoft.WindowsAppSDK.WinUI | 2.3.9 | 2.3.9 | Windows App SDK terms (license.txt) |
| SQLite | 3.54.0 | 3.54.1 | Public domain (LICENSE.txt) |
| SQLitePCLRaw.bundle_e_sqlite3 | 2.1.13 | 3.0.5 | Apache-2.0 |
| SQLitePCLRaw.bundle_e_sqlite3 | 3.0.5 | 3.0.5 | Apache-2.0 |
| SQLitePCLRaw.config.e_sqlite3 | 3.0.5 | 3.0.5 | Apache-2.0 |
| SQLitePCLRaw.core | 2.1.13 | 3.0.5 | Apache-2.0 |
| SQLitePCLRaw.core | 3.0.5 | 3.0.5 | Apache-2.0 |
| SQLitePCLRaw.lib.e_sqlite3 | 2.1.13 | 3.53.3 | Apache-2.0 |
| SQLitePCLRaw.provider.e_sqlite3 | 2.1.13 | 3.0.5 | Apache-2.0 |
| SQLitePCLRaw.provider.e_sqlite3 | 3.0.5 | 3.0.5 | Apache-2.0 |
| System.CodeDom | 10.0.13 | 10.0.13 | MIT |
| System.Management | 10.0.13 | 10.0.13 | MIT |
| System.Reflection.TypeExtensions | 4.7.0 | 4.7.0 | MIT |
| System.Security.AccessControl | 6.0.1 | 6.0.1 | MIT |
| xunit.analyzers | 2.2.0 | 2.2.0 | Apache-2.0 |
| xunit.runner.visualstudio | 4.0.1 | 4.0.1 | Apache-2.0 |
| xunit.v3.assert | 4.0.2 | 4.0.2 | Apache-2.0 |
| xunit.v3.common | 4.0.2 | 4.0.2 | Apache-2.0 |
| xunit.v3.core.mtp-off | 4.0.2 | 4.0.2 | Apache-2.0 |
| xunit.v3.extensibility.core | 4.0.2 | 4.0.2 | Apache-2.0 |
| xunit.v3.mtp-off | 4.0.2 | 4.0.2 | Apache-2.0 |
| xunit.v3.runner.common | 4.0.2 | 4.0.2 | Apache-2.0 |
| xunit.v3.runner.inproc.console | 4.0.2 | 4.0.2 | Apache-2.0 |

## Evidence and remaining checks

Detailed outputs live under ignored `artifacts/tooling-review/`: `outdated.json`,
`vulnerable.json`, `deprecated.json`, `registry-license-review.json`,
`benchmark-older-resolutions.json`, `license-texts.json`, shared repository tags,
release/advisory responses, platform release/advisory responses, SDK license and
the candidate SQLite package/license/nuspec. They are local verification evidence,
not files intended for distribution. Registry links use the NuGet v3 registration
endpoint recorded per row; their content may change after review.

During the initial read-only phase, no builds, native UI runs, signing, trust changes, installation, package creation,
remote publication or host upgrades were performed. The subsequent authorized
dependency update and build validation are recorded below.
After resolving the package findings, rerun solution and benchmark checks, deliberate
locked restore, relevant builds/tests and profile-specific audited packaging.
Review the final payload's complete native/runtime license materials. Existing
GitHub Actions production dependency graphs/advisory exceptions remain documented
in [GITHUB-ACTIONS.md](GITHUB-ACTIONS.md#action-dependency-review); adopting a
package-free shared source checkout does not clear those exceptions or re-audit
all current consumer Action bundles. Inspect the eventual shared revision and
consumer submodule pin again after the shared implementation is committed.


## Authorized update and validation, 2026-10-22

The implementation coordinator explicitly authorized resolving the package findings.
`./scripts/update-dependencies.ps1 -Apply` selected **only SQLite 3.54.0 -> 3.54.1**,
regenerated solution locks, and completed its final direct/transitive graph checks,
locked solution restore, Release build and permitted tests. Its test command excludes
`FullyQualifiedName~ChatGptAuth`; no live account authentication was performed.
The helper captures child build/test output internally, so this report does not
invent test counts or a warning count from its final success message. The complete
helper transcript is `artifacts/tooling-review/apply-update.log`.

Separately ran each Sampling, Rules and ComWrappers benchmark through:

```powershell
dotnet restore benchmarks/<Name>/<Name>.csproj --force-evaluate --nologo `
  -p:NuGetAudit=true -p:NuGetAuditMode=all -p:NuGetAuditLevel=low `
  -p:WarningsAsErrors=NU1900%3BNU1901%3BNU1902%3BNU1903%3BNU1904%3BNU1905
dotnet restore benchmarks/<Name>/<Name>.csproj --locked-mode --nologo `
  -p:NuGetAudit=true -p:NuGetAuditMode=all -p:NuGetAuditLevel=low `
  -p:WarningsAsErrors=NU1900%3BNU1901%3BNU1902%3BNU1903%3BNU1904%3BNU1905
dotnet build benchmarks/<Name>/<Name>.csproj -c Release --no-restore --nologo
dotnet list benchmarks/<Name>/<Name>.csproj package --outdated --include-transitive --format json
dotnet list benchmarks/<Name>/<Name>.csproj package --vulnerable --include-transitive --format json
dotnet list benchmarks/<Name>/<Name>.csproj package --deprecated --include-transitive --format json
```

All restores/builds passed. Each benchmark Release build reported zero warnings
and zero errors, and all nine graph reports contained zero findings. ComWrappers
has no package dependencies, so its forced restore produced no tracked lock change.
Sampling's lock now uses the existing central Diagnostics client 0.2.750501 and
Logging/DI 10.0.13 selections. Rules now uses the same SQLitePCLRaw 3.0.5 family and
SQLite 3.54.1 as the application; old `SQLitePCLRaw.lib.e_sqlite3` was removed by
resolution, rather than independently bumped. Solution and benchmark force-evaluation
also refreshed stale project-reference version floors from 0.1.0 to the current
0.2.0; no application version field was edited here. The final all-lock inventory
contains **38 package/version pairs**, archived as `final-package-inventory.json`.
The initial inventory above is retained to explain exactly what was corrected.

Executed only the synthetic checks:

```powershell
dotnet run --project benchmarks/Rules/Rules.csproj -c Release --no-build --no-restore
dotnet run --project benchmarks/ComWrappers/ComWrappers.csproj -c Release --no-build --no-restore
```

Both completed successfully. Rules exercised a temporary synthetic SQLite knowledge
store with 373 catalog definitions, 200 synthetic learned rules and 500/1,000 entity
workloads, including batch reads. ComWrappers reported runtime **10.0.13**, retained
managed growth **1,992 bytes** after one million balanced COM calls, and elapsed
47 ms. These are single-run observations, not performance comparisons or general
leak-free claims. Sampling was **built only**, never executed; no machine inventory
was collected. Detailed per-command logs are retained under the ignored review tree.

SQLite and benchmark dependency findings are resolved for these verified graphs.
No older-package compatibility exception was needed. Installed Git and PowerShell
were deliberately left unchanged; their host findings remain open. Packaging,
native UI and installed lifecycle acceptance remain the coordinator's separate
checks. This update did not sign/import trust/install products, prepare MSI artifacts,
commit, tag, push or publish. No advisory failure or compatibility error was suppressed.
