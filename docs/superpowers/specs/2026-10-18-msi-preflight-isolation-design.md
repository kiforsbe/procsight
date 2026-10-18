# MSI preflight isolation revision

Date: 2026-10-18. Revision to MSI packaging after installed-version collision in release tests.

Opening a validation MSI with the real product identity in a normal Windows Installer session
can collide with an installed version. IgnoreMachineState avoids that identity check but forbids
InstallValidate, so it cannot replace the current preflight.

Keep the original MSI unchanged. Create an owned disposable copy; assign fresh product, upgrade,
package and per-component GUIDs using Windows Installer database APIs. Preserve all table layouts,
versions, file mappings and payload streams. Redirect costing targets into the temporary directory.
Open only that copy in the normal engine, reject custom-action tables and execute the explicit
read-only action allowlist through InstallValidate. Never initialize/execute a transaction.
Close handles before bounded cleanup, on success or failure. Production identity/mapping/hash
inspection continues against the untouched original. These checks do not establish installed
repair/upgrade/uninstall behavior. Tests assert identity uniqueness, original-byte preservation,
unchanged registration state and successful native preflight; no fixture is installed.
