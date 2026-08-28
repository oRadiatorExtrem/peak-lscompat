# Changelog

## 0.2.0 (2026-08-28)

- Fixed: slow-motion under LSFG is fully eliminated (Time.maximumDeltaTime raised, config-driven).
- New: `ForceTargetFrameRateMode` with `HalfRefresh` auto-detection (caps base fps at half the monitor refresh for a stable LSFG input) — replaces the fixed `ForceTargetFrameRate` int.
- New: `EnableOverlay` config (default **off** for performance; F8 toggles).
- Robustness: all engine/config calls guarded; plugin can never crash the game loop.
- Refresh-rate detection: legacy `Resolution.refreshRate` + Unity 6 `refreshRateRatio` fallback chain.
- Focus/fullscreen transition logging for external-overlay diagnosis.

## 0.1.0 (2026-08-27)

- Initial mitigation: raise `Time.maximumDeltaTime` (configurable), force `runInBackground`, optional target frame rate cap, periodic pump-state logging, focus/fullscreen change events.
