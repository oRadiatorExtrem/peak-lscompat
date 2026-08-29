## New in this version

**A status panel you can actually read.** Press **F8** while playing and you'll see:

> **Game speed: OK - full speed**
> Frames: 55 fps
> Game world: 60 steps/s (normal: 60)

- One big verdict in plain language (green = good, red = the slow-motion bug) — no technical knowledge needed.
- The numbers are **measured live from the game**, so a screenshot of the panel is proof the fix is working.
- **F9** shows technical details if you want them (engine timing, fps cap, focus state).
- **F8 actually toggles the panel now** — it was documented before but not implemented. Oops.

## What this plugin does

With [Lossless Scaling](https://store.steampowered.com/app/993090/Lossless_Scaling/) frame generation active, PEAK can fall into slow motion: everything (jumping, climbing, physics) runs slower than it should while frames keep rendering normally. This plugin fixes that. Works on any GPU — it touches engine timing only, no graphics APIs.

Measured evidence is in the [README](https://github.com/oRadiatorExtrem/peak-lscompat#evidence): without the fix, the game world ran at ~0.5% of real speed and lost 465 seconds of simulation in an 18-minute session. With the fix, it stays locked to real time.

## Install

1. Requires [BepInEx 5.x for PEAK](https://thunderstore.io/c/peak/p/BepInEx/BepInExPack_PEAK/)
2. Download `PeakLSCompat.dll` below
3. Drop it into `PEAK/BepInEx/plugins/`
4. Play. Press F8 any time to check it's working.

See the [README](https://github.com/oRadiatorExtrem/peak-lscompat#recommended-lossless-scaling-setup-validated-empirically) for the recommended Lossless Scaling settings (validated on a GTX 1650 Ti).
