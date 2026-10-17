# Third-party components

ProcSight source is licensed under [AGPL-3.0](LICENSE). Dependency licenses remain separate.
The repository pins package versions and tracks NuGet lock files; it does not contain a
redistributable installer or promise compatibility of every future distribution.

| Component | Package license metadata |
| --- | --- |
| Microsoft.WindowsAppSDK.WinUI 2.2.1 | Microsoft license file supplied in the package |
| Microsoft.WindowsAppSDK.Runtime 2.2.0 | Microsoft license file supplied in the package |
| Microsoft.Windows.SDK.BuildTools 10.0.26100.4654 | Windows SDK license terms linked by the package |
| Microsoft.Windows.SDK.BuildTools.MSIX 1.7.251221100 | Windows SDK license terms supplied in sdk_license.txt; build-only tooling |
| Microsoft.Data.Sqlite / Microsoft.Data.Sqlite.Core 10.0.13 | MIT, Microsoft Corporation |
| System.Management 10.0.13 | MIT, Microsoft Corporation |
| Microsoft.Diagnostics.Tracing.TraceEvent 4.0.0 | MIT, .NET Foundation and Contributors; [license](https://github.com/microsoft/perfview/blob/v4.0.0/LICENSE.TXT) |
| Microsoft.Diagnostics.NETCore.Client 0.2.661903 | MIT, .NET Foundation and Contributors |
| Microsoft.Extensions.Logging.Abstractions 8.0.3 / DependencyInjection.Abstractions 8.0.2 | MIT, .NET Foundation and Contributors |
| System.Reflection.TypeExtensions 4.7.0 | MIT, .NET Foundation and Contributors |
| SQLitePCLRaw dependencies | Apache-2.0; preserve package notices |
| Native SQLite | Public domain; see SQLite's copyright statement |
| xUnit / xUnit Visual Studio runner | Apache-2.0; test tooling |
| Microsoft.NET.Test.Sdk and test-platform dependencies | Package-specific notices; test tooling |

The inspected Windows App SDK package license files are headed **MICROSOFT SOFTWARE
LICENSE TERMS — MICROSOFT WINDOWS APP SDK ENGINEERING PREVIEW**. Do not label these packages
MIT based on the upstream source repository's license. Review the actual pinned package
terms and runtime prerequisites before producing executable distributions.

Package `.nuspec` files, license files, and transitive lock-file entries are the authoritative
inputs for a distribution inventory. Before distributing binaries, include the complete
applicable third-party license texts, copyright notices, and redistribution materials for
all bundled transitive/native dependencies. This development inventory is not a substitute
for those materials.

References: [SQLite copyright](https://www.sqlite.org/copyright.html),
[SQLitePCLRaw](https://github.com/ericsink/SQLitePCL.raw),
[xUnit license](https://github.com/xunit/xunit/blob/main/LICENSE), and
[Windows SDK terms](https://aka.ms/WinSDKLicenseURL).
