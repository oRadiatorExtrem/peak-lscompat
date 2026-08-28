# PEAK LS Compat

BepInEx plugin that makes [Lossless Scaling](https://store.steampowered.com/app/993090/Lossless_Scaling/) (LSFG frame generation) safe to use with **PEAK**.

Without it, activating LS frame generation can make the whole game run in slow motion (physics, gravity, player speed — everything). This happens because Unity clamps `Time.deltaTime` at `Time.maximumDeltaTime` (0.1667 s) whenever a frame takes longer than that, which is common while an external capture/overlay interacts with the game's presentation path. The plugin removes that time loss and keeps the simulation locked to real time.

Works on **any GPU** — the plugin touches no graphics APIs, only engine timing settings.

## Install (manual)

1. Install [BepInEx 5.x for PEAK](https://thunderstore.io/c/peak/p/BepInEx/BepInExPack_PEAK/) (required, plugin depends on it).
2. Download `PeakLSCompat.dll` from [Releases](../../releases).
3. Copy it to `PEAK/BepInEx/plugins/`.

That's it. A safe config is generated on first launch at `BepInEx/config/com.black.peaklscompat.cfg`.

## Recommended Lossless Scaling setup

**Any GPU:**

- Game in **borderless fullscreen** (not exclusive)
- Capture API: **WGC** (Windows 11 24H2+), Max Frame Latency **2**
- **G-Sync support OFF** unless your display actually supports G-Sync
- LSFG mode: **Fixed ×2**, target ≈ monitor refresh (or slightly below)

**Weak GPUs (e.g. GTX 1650 Ti):** frame generation needs GPU headroom. If your base fps collapses with LSFG on:

1. Enable **upscaling first**: Scaling Type **LS1** (Performance), factor ~**1.5**, Auto Scale on — this lowers the render cost.
2. Then keep LSFG Fixed ×2 on top.
3. Expect modest net gains: frame gen trades real frames for generated ones. On a GPU already at 100% load the base fps drops, and LSFG needs base ≥ 30–40 fps (60 ideal).

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
