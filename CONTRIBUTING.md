# Contributing to ProcSight

ProcSight is an unreleased Windows 11 development preview focused on read-only process
and service investigation. Bug reports, focused improvements, documentation and accessible
native UI contributions are welcome. Follow the [code of conduct](CODE_OF_CONDUCT.md).

## Before starting

- Search existing issues and use the bug report or feature request form.
- Discuss substantial features, new dependencies and changes to privacy or privilege
  boundaries in an issue before investing in implementation.
- Read [README.md](README.md), [DEVELOPERS.md](DEVELOPERS.md),
  [ARCHITECTURE.md](ARCHITECTURE.md) and repository [instructions](AGENTS.md).
- Follow [SECURITY.md](SECURITY.md) for vulnerabilities. Use [SUPPORT.md](SUPPORT.md)
  for troubleshooting and questions.

## Develop and verify

See the [developer guide](DEVELOPERS.md) for the pinned Windows/.NET toolchain,
build/run commands, native fixtures and packaging requirements. Use synthetic test
data and fake providers; real authentication is performed manually by the user.
Never commit credentials, process inventories, raw command lines, personal paths,
window captions, knowledge databases or memory dumps.

Keep changes focused and preserve read-only collection, process-lifetime checks,
rule evidence/scope and explicit online-sharing boundaries. For UI changes, apply
[the native design guide](docs/WINUI3-DESIGN.md) and check adaptive layout and accessibility.
Do not add CI/CD or publication automation without a separately agreed scope.

Run the relevant checks and, for application changes, the standard verification:

```powershell
./scripts/verify.ps1 -Configuration Release
```

Record exact commands, results and remaining manual checks. Synthetic tests do not
establish live-provider behavior, installation acceptance or release readiness.
For documentation-only changes, check local links and run `git diff --check`.

## Submit a pull request

Use the PR template to explain the problem, resulting behavior, verification and
limitations. Link related issues. Update affected documentation in the same change
and keep applicable Unreleased changelog entries short and informative. Include
sanitized synthetic captures for UI changes; avoid unrelated formatting or refactoring.

Use logical commits. Do not include generated build output or change dependency pins,
version fields or release tags incidentally. Release preparation is a separate local
workflow described in the developer guide.

Contributions use the repository's [GNU AGPL v3.0 license](LICENSE). Preserve copyright,
license and [third-party notices](THIRD_PARTY_NOTICES.md); no contributor agreement is required.
