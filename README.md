# PEAK LS Compat

BepInEx plugin that makes [Lossless Scaling](https://store.steampowered.com/app/993090/Lossless_Scaling/) (LSFG frame generation) safe to use with **PEAK**.

Without it, activating LS frame generation can make the whole game run in slow motion (physics, gravity, player speed — everything). This happens because Unity clamps `Time.deltaTime` at `Time.maximumDeltaTime` (0.1667 s) whenever a frame takes longer than that, which is common while an external capture/overlay interacts with the game's presentation path. The plugin removes that time loss and keeps the simulation locked to real time.

Works on **any GPU** — the plugin touches no graphics APIs, only engine timing settings.

## Install (manual)

1. Install [BepInEx 5.x for PEAK](https://thunderstore.io/c/peak/p/BepInEx/BepInExPack_PEAK/) (required, plugin depends on it).
2. Download `PeakLSCompat.dll` from [Releases](../../releases).
3. Copy it to `PEAK/BepInEx/plugins/`.

That's it. A safe config is generated on first launch at `BepInEx/config/com.black.peaklscompat.cfg`.

## Recommended Lossless Scaling setup (validated empirically)

**Any GPU:**

- Game in **borderless fullscreen** (not exclusive)
- Capture API: **WGC** (Windows 11 24H2+), Max Frame Latency **2**
- **G-Sync support OFF** unless your display actually supports G-Sync
- **Scaling Type: Off** — frame generation only
- LSFG mode: **Fixed ×2**, target ≈ monitor refresh (or slightly below)
- In `config.ini` (LS install folder): set `show_captured = 0` so the FPS counter shows the **output** framerate (with generated frames), not the base

**Weak GPUs (e.g. GTX 1650 Ti):** frame gen costs real GPU headroom (capture + interpolation ≈ 15–25 fps of base). Measured on a GTX 1650 Ti with PEAK @1080p:

| Config | Base (real) | Output (felt) |
|---|---|---|
| No LS | ~60 | ~60 |
| Upscale LS1 ×1.5 + FG ×2 | 32 | 65 |
| **FG ×2 only (recommended)** | **47–52** | **105+** |

→ **Do not combine upscaling with frame gen on weak GPUs** — the upscale's cost ate more than it freed. Instead, lower the **game's own resolution** (e.g. 1280×720) and let the monitor stretch it; with the plugin's `HalfRefresh` cap the base holds at refresh/2 and FG ×2 delivers the full refresh rate.

**Important:** close **MSI Afterburner / RivaTuner (RTSS)** while playing. Their Present hooks interfere with LS capture and cause heavy blur/ghosting artifacts (confirmed by testing).

## Config options

| Option | Default | What it does |
|---|---|---|
| `FixMaximumDeltaTime` | `true` | Stops the slow-motion bug. Keep on. |
| `MaximumDeltaTime` | `1.0` | Max real seconds per frame accepted. |
| `ForceRunInBackground` | `true` | Keeps sim running while the LS overlay holds focus. |
| `ForceTargetFrameRateMode` | `HalfRefresh` | Caps base framerate to half your monitor refresh (stable input for LSFG). `Off` / `Fixed` / `HalfRefresh`. |
| `ForceTargetFrameRateValue` | `60` | Cap used when mode is `Fixed`. |
| `EnableOverlay` | `false` | On-screen status overlay (F8 toggles). Off for max performance. |

## Build

```
csc.exe -nologo -nostdlib- -noconfig -target:library -optimize+ ^
  -r:netstandard.dll -r:System.dll -r:BepInEx.dll ^
  -r:UnityEngine.CoreModule.dll -r:UnityEngine.dll -r:UnityEngine.IMGUIModule.dll ^
  -out:PeakLSCompat.dll Plugin.cs
```

## License

MIT
