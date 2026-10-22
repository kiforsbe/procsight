# Dependency and licensing review

Reviewed 2026-10-22 against nuget.org and actual restored package metadata/license files.
This is a local development review, not release certification or legal clearance.
The project remains AGPL v3.0; no license replacement or dependency exception is granted here.

The [2026-10-22 detailed review](winbuildtools-dependency-review-2026-10-22.md) records
SQLite patch validation, benchmark lock synchronization and host servicing findings.

## Selected set

The solution resolves 38 unique package/version pairs. The final whole-solution NuGet
checks report no outdated, deprecated or known-vulnerable direct/transitive packages.
All 38 were separately checked as listed, non-prerelease and non-deprecated in NuGet
registration metadata. Advisory data and latest versions can change after this date.

| Area | Previous | Selected | Compatibility decision |
| --- | --- | --- | --- |
| WinUI / Windows App Runtime | 2.2.1 / 2.2.0 | 2.3.9 / 2.5.1 | Matches current stable SDK component family; retain split packages rather than importing unused AI/ML features. |
| Windows SDK build tools | 10.0.26100.4654 | 10.0.28000.2705 | Build tooling only; target/minimum remains 10.0.26100.0. |
| MSIX tools | 1.7.251221100 | 1.7.260930100 | Newer 1.7.261008100 is unlisted; excluded. |
| SQLitePCLRaw | 2.1.13 | 3.0.5 | Updated bundle/provider as one family; native SQLite 3.54.1 selected centrally. |
| WebView2 SDK | 1.0.3719.77 | 1.0.4258.31 | Transitive SDK update; no new browser-based authentication flow. |
| Diagnostics client | 0.2.661903 | 0.2.750501 | TraceEvent dependency; sampler tests pass. |
| Logging / DI abstractions | 8.0.3 / 8.0.2 | 10.0.13 | Compatible with the application's .NET 10 baseline. |
| Async interfaces | 6.0.0 | 10.0.13 | Current transitive dependency for supported test tooling. |
| xUnit framework | Deprecated xunit 2.9.3 | xunit.v3.mtp-off 4.0.2 | Official supported v3 variant preserving the existing VSTest commands and VS adapter; test projects become executables. |
| .NET libraries / Test SDK / VS adapter / TraceEvent | Existing pins | Retained | Already latest listed stable versions; see inventory. |

Central transitive pinning prevents NuGet from selecting older dependency minimums.
It does not add every central entry to every project. Lock files record the resolved
versions. New package-ID migrations require code/tooling review, not a blind version edit.

The SQLite bundle brings configuration, managed core/provider and the native `SQLite`
package. Current 3.0.5 metadata depends directly on `SQLite`, not `SourceGear.sqlite3`;
older migration prose mentions the previous package name. The win-x64 assets graph has
one native `e_sqlite3.dll`, from SQLite 3.54.1. No encryption/commercial SQLite package
was added. Microsoft.Data.Sqlite 10.0.13 alone still requests the older 2.1.13 bundle.

## License and commercialization findings

MIT/Apache-2.0 dependencies permit commercial redistribution subject to their notices
and conditions. The SQLite native package states public domain; WebView2 SDK carries
BSD-style redistribution terms. Test-tool dependencies remain separate from app assets.

Windows App SDK binary packages use Microsoft's own terms, not automatically the MIT
license of the upstream source. Current WinUI 2.3.9 has the standard SDK terms; the old
WinUI 2.2.1 file had engineering-preview restrictions. Runtime 2.2.0 already had standard
terms, contrary to the earlier notice inventory. Windows SDK/build tools have their own
terms too. Review the binary redistribution conditions, including copyleft restrictions,
for the intended distribution. No blanket AGPL compatibility is asserted by this review.

For ProcSight's own source license, GitHub recognizes Apache-2.0, MIT, MPL-2.0 and AGPL-3.0:

| Option | Commercialization tradeoff |
| --- | --- |
| Apache-2.0 | Recommended for permissive commercial/proprietary editions, with an explicit contributor patent grant and notice/change requirements. Others can also create proprietary forks. |
| MIT | Simple permissive commercialization; retain copyright/license notices. No equivalent express patent-grant clause. |
| MPL-2.0 | File-level copyleft: distributed modifications to covered files remain available as source; separate files can be proprietary. |
| Current AGPL v3.0 | Selling copies/support is permitted, but applicable corresponding-source and modified network-service obligations remain. It does not reserve commercialization to the original author. |

Relicensing or separate proprietary licensing requires appropriate rights to all covered
contributions and cannot override third-party terms. Existing recipients retain rights
already granted. The user must choose any change to the project's license separately.

## Verification and limits

- Locked restore and Release build passed; 399 permitted tests passed (74 Core,
  138 Application, 118 Infrastructure, 69 Windows). ChatGPT-auth tests were excluded.
- The updated xUnit analyzer emits 259 xUnit1051 cancellation-token advisories in test
  sources. They were not suppressed. Production app builds passed without warnings.
- Unsigned self-contained Direct MSIX creation/inspection passed. No signing, trust
  installation, real release/tag or publication was performed.
- Synthetic native columns fixtures passed in dark/wide and light/narrow configurations.
  Column-drag and details-stability captures completed but exited abnormally during
  shutdown; similar native fixture shutdown failures existed before this upgrade.
  Those runs are not clean end-to-end passes. Installed/runtime activation, full drag
  fluidity, accessibility and production lifecycle acceptance remain unverified.
- Dependency helper checks cover rejected metadata, direct/transitive findings, rollback
  and packaging audit arguments. Actual NuGet failure-path results are recorded in
  [verification notes](verification.md). None of these checks establish complete security.

Use [the local update workflow](../DEVELOPERS.md#dependency-review-and-updates) for future
reviews. Package license texts and notices remain required distribution materials;
see [THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md).

## Resolved inventory

License entries describe the inspected package metadata/files, not a relicensing grant.

| Package | Resolved version | License metadata / file |
| --- | --- | --- |
| Microsoft.Bcl.AsyncInterfaces | 10.0.13 | MIT |
| Microsoft.CodeCoverage | 18.11.0 | MIT |
| Microsoft.Data.Sqlite | 10.0.13 | MIT |
| Microsoft.Data.Sqlite.Core | 10.0.13 | MIT |
| Microsoft.Diagnostics.NETCore.Client | 0.2.750501 | MIT |
| Microsoft.Diagnostics.Tracing.TraceEvent | 4.0.0 | MIT |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.13 | MIT |
| Microsoft.Extensions.Logging.Abstractions | 10.0.13 | MIT |
| Microsoft.NET.Test.Sdk | 18.11.0 | MIT |
| Microsoft.TestPlatform.ObjectModel | 18.11.0 | MIT |
| Microsoft.TestPlatform.TestHost | 18.11.0 | MIT |
| Microsoft.Web.WebView2 | 1.0.4258.31 | BSD-style SDK license |
| Microsoft.Win32.Registry | 5.0.0 | MIT |
| Microsoft.Windows.SDK.BuildTools | 10.0.28000.2705 | Windows SDK terms |
| Microsoft.Windows.SDK.BuildTools.MSIX | 1.7.260930100 | Windows SDK terms |
| Microsoft.WindowsAppSDK.Base | 2.0.4 | Microsoft Windows App SDK terms |
| Microsoft.WindowsAppSDK.Foundation | 2.3.12 | Microsoft Windows App SDK terms |
| Microsoft.WindowsAppSDK.InteractiveExperiences | 2.1.9 | Microsoft Windows App SDK terms |
| Microsoft.WindowsAppSDK.Runtime | 2.5.1 | Microsoft Windows App SDK terms |
| Microsoft.WindowsAppSDK.WinUI | 2.3.9 | Microsoft Windows App SDK terms |
| SQLite | 3.54.1 | Public domain |
| SQLitePCLRaw.bundle_e_sqlite3 | 3.0.5 | Apache-2.0 |
| SQLitePCLRaw.config.e_sqlite3 | 3.0.5 | Apache-2.0 |
| SQLitePCLRaw.core | 3.0.5 | Apache-2.0 |
| SQLitePCLRaw.provider.e_sqlite3 | 3.0.5 | Apache-2.0 |
| System.CodeDom | 10.0.13 | MIT |
| System.Management | 10.0.13 | MIT |
| System.Reflection.TypeExtensions | 4.7.0 | MIT |
| System.Security.AccessControl | 6.0.1 | MIT |
| xunit.analyzers | 2.2.0 | Apache-2.0 |
| xunit.runner.visualstudio | 4.0.1 | Apache-2.0 |
| xunit.v3.assert | 4.0.2 | Apache-2.0 |
| xunit.v3.common | 4.0.2 | Apache-2.0 |
| xunit.v3.core.mtp-off | 4.0.2 | Apache-2.0 |
| xunit.v3.extensibility.core | 4.0.2 | Apache-2.0 |
| xunit.v3.mtp-off | 4.0.2 | Apache-2.0 |
| xunit.v3.runner.common | 4.0.2 | Apache-2.0 |
| xunit.v3.runner.inproc.console | 4.0.2 | Apache-2.0 |

## Primary references

- [Microsoft stable Windows App SDK components](https://www.nuget.org/packages/Microsoft.WindowsAppSDK/2.5.1)
- [WinUI 2.3.9 binary license](https://www.nuget.org/packages/Microsoft.WindowsAppSDK.WinUI/2.3.9/License)
- [SQLitePCLRaw packaging migration](https://github.com/ericsink/SQLitePCL.raw/blob/main/v3.md) (verify actual selected package metadata)
- [xUnit supported runner choices](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform)
- [NuGet auditing and its error codes](https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages)
- [GitHub license recognition](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/licensing-a-repository)
- [Apache-2.0 terms](https://choosealicense.com/licenses/apache-2.0/), [MIT terms](https://choosealicense.com/licenses/mit/),
  [Mozilla MPL FAQ](https://www.mozilla.org/en-US/MPL/2.0/FAQ/), [AGPL v3.0 terms](https://choosealicense.com/licenses/agpl-3.0/)

## GitHub Actions build tools

The [workflow dependency review](GITHUB-ACTIONS.md#action-dependency-review) separately covers
full-SHA-pinned official Actions, production Node dependency trees, licenses and known upstream
advisory exceptions. These tools run only on hosted build workers and are not MSI payload
components. The NuGet-only clean-scan statements above do not extend to these Action bundles.
