# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.1] - 2026-07-01

### Fixed

- Added the missing Unity `.meta` files for `README.md`, `CHANGELOG.md`, and `LICENSE.md`. They were committed without metas in 0.1.0; because the package is consumed read-only via git URL, Unity could not generate them and reported missing-meta errors.

## [0.1.0] - 2026-07-01

### Added

- Initial release, extracted from the `guess-me-if-you-can` project into a standalone UPM package.
- `UI Preview` Inspector tab that renders uGUI prefabs via an off-screen camera into a cached `RenderTexture`.
- Neutralization of inner canvases (screen-overlay roots, nested sub-canvases, bare fragments).
- Content-aware refit that tightly frames the painted content.
- Optional auto-selection of the UI Preview tab for UI prefabs.
- Per-user settings under Project Settings (enabled, auto-select, reference resolution, max texture size, framing padding, background color).
