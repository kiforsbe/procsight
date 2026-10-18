# MSI distribution revision

Date: 2026-10-18. Implements the user's request for an installer without MSIX certificate trust.
This extends the existing MSIX/release design; Direct and Store behavior remain supported.

- Add a `Msi` packaging/release profile. Publish the normal unpackaged WinUI application,
  self-contained for .NET and Windows App SDK, with no automatic elevated app launch.
- Author a standard x64, per-machine Windows Installer database using Windows Installer
  Automation and an embedded cabinet created by Windows MakeCab. No new third-party build
  package, WiX EULA acceptance, downloaded bootstrapper or custom executable action is needed.
- Install only the inspected published payload under Program Files/ProcSight, with a Start-menu
  shortcut, stable upgrade identity and one stable 64-bit component per file. Reject older
  products; support normal repair/uninstall. Close ProcSight before upgrade. Do not force-close it.
- Windows build 26100+ and x64 are checked by native installer conditions. Installation requires
  administrator consent; app startup remains asInvoker. No certificate-store operations exist.
- MSI is installable unsigned subject to Windows security policy/reputation checks. Signing is
  optional through the existing explicit certificate/timestamp inputs. No trust changes are made.
- Reuse locked restore, vulnerability gates, source version, license gathering and artifact
  commit/hash metadata. Reject MSI-incompatible major/minor components above 255 before release
  edits; do not silently truncate SemVer. Release/tag handling remains explicit and local.
- MSI uses existing unpackaged data/credential scopes; MSIX data stays separate. Never migrate,
  read or remove user settings, knowledge, accounts or credentials during installation/uninstall.
- Inspect the actual MSI tables and embedded payload; run synthetic database/cabinet tests and
  real package creation without installing it. Full interactive install, UAC, upgrade, repair and
  uninstall acceptance must be recorded separately; artifact construction does not establish them.

Implementation sequence: failing synthetic MSI contracts; native database/cabinet authoring;
profile/publish/release integration; inspection and signed/unsigned metadata; documentation,
script/build/tests and actual unsigned MSI creation; independent review and logical commits.
