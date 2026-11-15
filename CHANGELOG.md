# Changelog

## 1.0.0

First public release.

- Native Windows Arena and Process Explorer with real CPU/private-memory
  metrics, application grouping, and pressure-aware explanations.
- Opt-in elevated physical disk and TCP/UDP tracing, with corrected kernel
  provider initialization and honest failure status.
- Safe Mode confirmation, bounded PID/creation-time termination, and
  system/self/owner/session protection.
- Per-user setup with Start-menu integration, optional desktop shortcut, and
  Windows uninstall support; self-contained portable alternative.
- SHA-256 release checksums, CodeQL, dependency review, Dependabot, and
  Windows build/application/installer verification.

Current limitations: unsigned Windows builds; no automatic updater, native
ARM64 build, Store/winget package, or optional tray monitor.
