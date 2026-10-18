# Lightweight local Windows testing

Build on the host; keep only a clean Windows installation and ProcSight in the guest.
Do not install Visual Studio, SDKs or a full development image inside a packaging-test VM.
A disposable test environment is useful for MSI/MSIX install, launch, repair, upgrade,
downgrade rejection and uninstall. It does not replace host performance/hardware testing.

| Environment | Best use | Limitation |
| --- | --- | --- |
| Windows Sandbox | Disposable installation and UI smoke tests | Supported Pro/Enterprise/Education editions; unavailable on Home; state disappears on close |
| Small VirtualBox VM | Local snapshot-based tests on Home, open-source base hypervisor | Separate guest Windows installation and disk space; host drivers/tooling |
| Small VMware Workstation VM | Local snapshot-based tests on Home, desktop graphics testing | Separate guest Windows installation and proprietary host tooling |

Microsoft documents [Sandbox edition support](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/)
and [configuration options](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-configure-using-wsb-file).
Sandbox shares OS memory pages with its host, which can reduce overhead compared with a
separate VM; see its [architecture](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-architecture).
It is the first choice for disposable testing when the host edition supports it. A .wsb
configuration can cap memory at 4096 MB, disable networking and map installer inputs
read-only, with a separate writable log-output folder. Do not map the whole repository,
user profile, credentials or source secrets. No Sandbox feature is enabled by this guide.

For Home, use a normal Windows 11 VM with **2 virtual CPUs, 4 GB RAM and an 80 GB dynamically
growing disk** as an initial test configuration. These are configuration choices, not
measured resource usage; a sparse disk grows as the guest writes data. Keep a clean baseline
snapshot, restore it before each lifecycle test and shut down the guest when finished.
The free hypervisor does not supply a Windows guest license. Keep activation/licensing
appropriate to the chosen official Windows image.

[VirtualBox base packages](https://www.virtualbox.org/wiki/Downloads) use GPLv3; its separate
Extension Pack has different terms and is not needed for this basic setup.
[VMware Workstation is free for personal, educational and commercial use](https://blogs.vmware.com/cloud-foundation/2024/11/11/vmware-fusion-and-workstation-are-now-free-for-all-users/).
Use a current vendor-supported stable host build and review its release notes/advisories
before installation. The current host's already-active Windows hypervisor/VBS may affect
third-party VM performance; measure rather than disabling host security as a default.
No hypervisor or guest image has been downloaded or installed by this work.

Inside the guest, disable networking for local installer tests, transfer only the package
and synthetic fixtures, and enable guest graphics acceleration when testing WinUI rendering.
Record the guest Windows build, hypervisor, graphics configuration and elevation state.
Test install and uninstall through real Windows Installer, retaining verbose logs outside
the disposable guest. Verify files/shortcuts/registration are removed while synthetic
per-user settings survive. A full cycle is required before claiming installer acceptance.
Guest GPU, disk, network, ETW and timing behavior can differ from physical hardware.
