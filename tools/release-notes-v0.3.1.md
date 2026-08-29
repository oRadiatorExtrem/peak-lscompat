## What changed

### Performance improvements

- **Zero GC when overlay is off.** The status overlay was extracted into its own component (`LSCompatOverlay`). Previously, Unity's IMGUI system allocated ~0.7 KB/frame just because `OnGUI()` existed on the plugin class — even with the early return. This caused garbage collection spikes every few seconds, especially noticeable on weak GPUs. Now the overlay component only exists while F8 has it visible; when hidden, **zero GC overhead**.

- **Safer physics catch-up.** Default `MaximumDeltaTime` lowered from 1.0 to **0.5 seconds**. The old value could trigger up to ~60 FixedUpdate steps in a single frame after a stall (the "[spiral of death](https://docs.unity3d.com/2023.1/Documentation/Manual/TimeFrameManagement.html)"). 0.5 limits catch-up to ~30 steps while still covering typical Lossless Scaling capture stalls. Configurable in the `.cfg` file if you need more headroom.

### Bug fixes

- **V-Sync no longer silently blocks the framerate cap.** `Application.targetFrameRate` is [ignored by Unity when `vSyncCount > 0`](https://docs.unity3d.com/ScriptReference/Application-targetFrameRate.html). If PEAK had V-Sync enabled in quality settings, the plugin's HalfRefresh cap did nothing. The plugin now automatically disables vSyncCount when a cap mode is active and logs the change.

- **Multi-monitor support.** The detected refresh rate was cached forever after first launch. Moving the game window to a monitor with a different refresh rate (60 Hz → 144 Hz) kept the old cap. Now re-detects every 30 seconds.

- **Config resilience.** If BepInEx config parsing failed partway, some config entries stayed null and caused a NullReferenceException every frame (caught but spamming the log). Added null guards.

### Documentation

- **Full Lossless Scaling setup guide** covering 4 GPU tiers — from Intel UHD / GT 1030 all the way to RTX 3060+ — with per-tier settings tables (LSFG version, flow scale, performance mode, capture API, frame latency, queue target). Includes a dual GPU section and 5 key rules. Settings validated against [official LS guides](https://sageinfinity.github.io/docs/), [community benchmarks](https://steamcommunity.com/app/993090/discussions/), and the developer's own GTX 1650 Ti measurements.

- README restructured for clarity: quick status check first, install, what the plugin does (table), tiered LS guide, config reference, evidence section.

## Transparency

This plugin was built by [@oRadiatorExtrem](https://github.com/oRadiatorExtrem) with assistance from:
- **GLM-5.3 Flash** (Zhipu AI) — initial development and design
- **Claude Opus 4.6** (Anthropic) — code review, performance fixes (v0.3.1), and documentation improvements

All source code is in a single file ([Plugin.cs](Plugin.cs)) — fully auditable. The plugin touches only Unity engine timing settings (`Time.maximumDeltaTime`, `Application.runInBackground`, `Application.targetFrameRate`, `QualitySettings.vSyncCount`). No graphics APIs, no network calls, no file I/O beyond BepInEx's own config system.

## Install

1. Requires [BepInEx 5.x for PEAK](https://thunderstore.io/c/peak/p/BepInEx/BepInExPack_PEAK/)
2. Download `PeakLSCompat.dll` below
3. Drop it into `PEAK/BepInEx/plugins/`
4. Play. Press F8 any time to check it's working.

See the [README](https://github.com/oRadiatorExtrem/PeakLSCompat#lossless-scaling-setup-guide-all-gpu-tiers) for the recommended Lossless Scaling settings for your GPU tier.
