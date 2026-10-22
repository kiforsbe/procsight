# Third-party components

ProcSight source is licensed under [AGPL-3.0](LICENSE). Dependency licenses remain separate.
The repository pins package versions and tracks NuGet lock files; it does not contain a
redistributable installer or promise compatibility of every future distribution.

| Component | Package license metadata |
| --- | --- |
| Microsoft.WindowsAppSDK.WinUI 2.3.9 / Runtime 2.5.1 | Microsoft Windows App SDK terms supplied in each package |
| Microsoft.WindowsAppSDK.Base 2.0.4 / Foundation 2.3.12 / InteractiveExperiences 2.1.9 | Microsoft Windows App SDK terms supplied in each package |
| Microsoft.Windows.SDK.BuildTools 10.0.28000.2705 | Windows SDK license terms linked by the package |
| Microsoft.Windows.SDK.BuildTools.MSIX 1.7.260930100 | Windows SDK license terms supplied in sdk_license.txt; build-only tooling |
| Microsoft.Data.Sqlite / Microsoft.Data.Sqlite.Core 10.0.13 | MIT, Microsoft Corporation |
| System.Management 10.0.13 | MIT, Microsoft Corporation |
| Microsoft.Diagnostics.Tracing.TraceEvent 4.0.0 | MIT, .NET Foundation and Contributors; [license](https://github.com/microsoft/perfview/blob/v4.0.0/LICENSE.TXT) |
| Microsoft.Diagnostics.NETCore.Client 0.2.750501 | MIT, .NET Foundation and Contributors |
| Microsoft.Extensions.Logging.Abstractions / DependencyInjection.Abstractions / Microsoft.Bcl.AsyncInterfaces 10.0.13 | MIT, .NET Foundation and Contributors |
| System.Reflection.TypeExtensions 4.7.0 | MIT, .NET Foundation and Contributors |
| SQLitePCLRaw bundle/config/core/provider 3.0.5 | Apache-2.0; preserve package notices |
| SQLite 3.54.1 native library | Public domain per package LICENSE.txt; see SQLite's copyright statement |
| Microsoft.Web.WebView2 1.0.4258.31 | BSD-style three-clause terms in package LICENSE.txt; separate installed browser-runtime terms may apply |
| xUnit v3 (mtp-off) 4.0.2 / analyzers 2.2.0 / Visual Studio runner 4.0.1 | Apache-2.0; test tooling |
| Microsoft.NET.Test.Sdk and test-platform dependencies | Package-specific notices; test tooling |

The inspected current Windows App SDK package license files are headed **MICROSOFT SOFTWARE
LICENSE TERMS — MICROSOFT WINDOWS APP SDK**, without the engineering-preview heading found
in the previously pinned WinUI 2.2.1 package. The previous Runtime 2.2.0 already had standard
terms; the earlier inventory incorrectly described both packages as preview-licensed.
These binary packages are not automatically MIT because their upstream source is open.
Their redistributable-code conditions and copyleft restrictions require review for the
actual distribution; this inventory does not certify AGPL compatibility. See the
[dependency review](docs/DEPENDENCIES.md) for the complete resolved package list and findings.

Package `.nuspec` files, license files, and transitive lock-file entries are the authoritative
inputs for a distribution inventory. Before distributing binaries, include the complete
applicable third-party license texts, copyright notices, and redistribution materials for
all bundled transitive/native dependencies. This development inventory is not a substitute
for those materials.

References: [SQLite copyright](https://www.sqlite.org/copyright.html),
[SQLitePCLRaw](https://github.com/ericsink/SQLitePCL.raw),
[xUnit license](https://github.com/xunit/xunit/blob/main/LICENSE), and
[Windows SDK terms](https://aka.ms/WinSDKLicenseURL).
