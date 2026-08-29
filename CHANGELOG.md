# Changelog

## 0.3.1 (2026-08-29)

### Performance
- **Zero GC when overlay is off**: extracted the status overlay into a separate `LSCompatOverlay` component that is created only when F8 turns it on and destroyed when turned off. Previously, having `OnGUI()` on the main plugin class caused Unity to allocate ~0.7 KB/frame in the IMGUI event system even with an early return — this GC pressure triggered collection spikes every few seconds on weak GPUs.
- **Safer default `MaximumDeltaTime`**: lowered from 1.0 to **0.5** seconds. The old value allowed ~60 FixedUpdate catch-up steps after a stall, risking a physics "spiral of death" on complex scenes. 0.5 limits catch-up to ~30 steps while still covering typical LS capture stalls. Configurable if needed.

### Fixes
- Fixed: `targetFrameRate` was silently ignored when PEAK had V-Sync enabled (`QualitySettings.vSyncCount > 0`). The plugin now disables vSyncCount automatically when a framerate cap mode is active (HalfRefresh or Fixed).
- Fixed: null `ConfigEntry` fields after a partial config load failure could cause NullReferenceException every frame in `ApplyTimeFixes()`. Added null guards.
- Fixed: `HalfRefresh` refresh rate was cached forever after first detection. Now re-detects every 30 seconds so moving the game window to a different monitor picks up the new rate.

### Cleanup
- Removed unused `System.Collections.Generic` import and a no-op null reassignment in `GetRefreshRate()`.
- Expanded README: full Lossless Scaling setup guide covering 4 GPU tiers (ultra-low to high-end), dual GPU setup, and 5 key rules validated from community and official sources.

## 0.3.0 (2026-08-28)

- Overlay redesign, player-friendly: big "Game speed: OK / SLOW" verdict, rendered fps and world simulation steps in plain language; technical details (maxDeltaTime, targetFPS, speed ratio, world lag) moved behind F9.
- Fixed: F8 actually toggles the overlay in game now (previously it was documented but not implemented).
- Overlay now shows live measurements (1-2 s windows), not just config values — a screenshot of it is self-contained evidence that the fix is working.

## 0.2.0 (2026-08-28)

- Fixed: slow-motion under LSFG is fully eliminated (Time.maximumDeltaTime raised, config-driven).
- New: `ForceTargetFrameRateMode` with `HalfRefresh` auto-detection (caps base fps at half the monitor refresh for a stable LSFG input) — replaces the fixed `ForceTargetFrameRate` int.
- New: `EnableOverlay` config (default **off** for performance; F8 toggles).
- Robustness: all engine/config calls guarded; plugin can never crash the game loop.
- Refresh-rate detection: legacy `Resolution.refreshRate` + Unity 6 `refreshRateRatio` fallback chain.
- Focus/fullscreen transition logging for external-overlay diagnosis.

## 0.1.0 (2026-08-27)

- Initial mitigation: raise `Time.maximumDeltaTime` (configurable), force `runInBackground`, optional target frame rate cap, periodic pump-state logging, focus/fullscreen change events.
