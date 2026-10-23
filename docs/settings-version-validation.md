# Settings version footer validation

Verified on Windows x64 on 2026-10-22 using synthetic Settings data only.
The source version during this check was 0.2.0; the release build must be
checked again after synchronizing the release version.

The footer in [native Settings](../src/ProcSight.App/MainWindow.Settings.cs)
reads the running App assembly's informational version and removes its `+`
build-metadata suffix. It uses the existing selectable native caption style
with the secondary theme text brush and an explicit application-version
automation name. It follows the final Settings card inside the page scroll
viewer and does not require package identity or access to the source tree.

The existing [Settings fixture](../src/ProcSight.App/MainWindow.VisualFixture.cs)
checks that the footer is last, matches the independently read assembly
major/minor/build version, and exposes selectable text and its automation name.
Its `settings-version` variant scrolls to the bottom and checks that the entire
footer fits inside the viewport before rendering the PNG.

Verification performed:

- Before the footer implementation, the Debug build succeeded and the fixture
  failed with `Settings must end with the application version footer.`
- After implementation, `dotnet build src/ProcSight.App -c Debug --no-restore
  --nologo` succeeded with zero warnings and zero errors.
- `settings-version` completed with exit code zero and a `.checks` result for
  Light/Dark at both 1200 and 660 widths. All four PNGs were visually inspected;
  each shows the complete `ProcSight 0.2.0` footer at the bottom.
- Generated `ProcSight.App.runtimeconfig.json` still requires .NET 10.0.13.
- `git diff --check` passed for the footer changes.

Reproduction after building:

```powershell
$capture = Join-Path $PWD 'artifacts/settings-version/Light-660.png'
New-Item -ItemType Directory -Path (Split-Path $capture -Parent) -Force | Out-Null
$fixture = Start-Process -FilePath 'src/ProcSight.App/bin/Debug/net10.0-windows10.0.26100.0/win-x64/ProcSight.App.exe' -ArgumentList @('--visual-fixture', ('"' + $capture + '"'), 'Light', '660', 'settings-version') -WindowStyle Hidden -PassThru
if (!$fixture.WaitForExit(45000)) { throw 'Fixture exceeded 45 seconds' }
if (Test-Path -LiteralPath ($capture + '.error')) { throw (Get-Content -LiteralPath ($capture + '.error') -Raw) }
Get-Content -LiteralPath ($capture + '.checks')
```

Captures and diagnostic companions remain under ignored
`artifacts/settings-version/`. The pre-existing wide Settings card layout
clips some right-hand action buttons in these captures; footer checks do not
establish acceptance of that unrelated layout. Keyboard selection/copy,
Narrator, high contrast and text/display scaling remain manual acceptance
checks; screenshots and property assertions do not replace them.

## Release-version recheck — 2026-10-23

After the release script synchronized 0.3.0, the Debug fixture build again passed
with zero warnings/errors. `settings-version` passed at Light/Dark and 1200/660
widths, displaying `ProcSight 0.3.0` from its actual assembly metadata. Captures
remain under ignored `artifacts/settings-version-0.3.0/`; the narrow Light view
was independently inspected. Earlier layout/accessibility limits above remain.
