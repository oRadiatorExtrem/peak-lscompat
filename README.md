# PEAK LS Compat

BepInEx plugin that fixes the **slow-motion bug** caused by [Lossless Scaling](https://store.steampowered.com/app/993090/Lossless_Scaling/) frame generation (LSFG) in **PEAK**.

When LSFG is active, Unity clamps `Time.deltaTime` at `Time.maximumDeltaTime` (0.1667 s) whenever the external capture/overlay stalls a frame beyond that threshold. The result: the entire simulation — physics, gravity, player movement, timers — runs in slow motion while the renderer keeps drawing at normal speed. This plugin removes that time loss and keeps the game locked to real time.

**GPU-agnostic** — no graphics APIs touched, only engine timing settings.

> Built by [@oRadiatorExtrem](https://github.com/oRadiatorExtrem) with assistance from GLM-5.3 Flash (Zhipu AI).

## Quick check — is it working?

Press **F8** while playing. A status panel appears:

> **Game speed: OK - full speed**
> Frames: 55 fps
> Game world: 60 steps/s (normal: 60)

Green = everything is fine. If it ever says **SLOW**, grab a screenshot (F9 adds technical details) and [open an issue](../../issues).

The panel renders inside the game, so screenshots and recordings capture it (unlike LS's own counter, which lives on a separate layer). Press F8 again to hide it.

## Install

1. Install [BepInEx 5.x for PEAK](https://thunderstore.io/c/peak/p/BepInEx/BepInExPack_PEAK/).
2. Download `PeakLSCompat.dll` from [Releases](../../releases).
3. Copy it to `PEAK/BepInEx/plugins/`.

A safe config is generated on first launch at `BepInEx/config/com.black.peaklscompat.cfg`.

## What the plugin does

| Fix | Why it matters |
|---|---|
| Raises `Time.maximumDeltaTime` | `deltaTime` reflects real frame duration during capture stalls — no slow motion. |
| Forces `Application.runInBackground` | Simulation keeps running while the LS overlay holds focus. |
| Auto-caps base framerate (`HalfRefresh`) | Gives LSFG a stable, consistent input — smooth frame generation output. |
| Logs focus / fullscreen transitions | Helps diagnose overlay conflicts (RTSS, Afterburner, etc.). |

## Lossless Scaling setup guide (all GPU tiers)

### Before you start (applies to every PC)

- PEAK must run in **borderless fullscreen** (not exclusive — LS cannot capture exclusive fullscreen)
- **Disable V-Sync in PEAK** — the plugin caps the framerate; V-Sync in Unity games causes slow motion under LS
- Close **MSI Afterburner / RivaTuner (RTSS)** — their Present hooks corrupt LS capture and cause blur/ghosting
- **G-Sync / FreeSync support OFF** in LS unless your monitor actually supports it
- In LS `config.ini` (install folder): set `show_captured = 0` to see **output** fps (with generated frames)

### Lossless Scaling settings by GPU tier

<details>
<summary><b>Ultra-low (Intel UHD / GT 1030 / GT 740 — base fps 15–30)</b></summary>

These GPUs have almost no headroom. The strategy: run PEAK at the lowest resolution you can tolerate, cap very low, and let LSFG multiply.

| Setting | Value | Why |
|---|---|---|
| **PEAK resolution** | 960×540 or 1280×720 | Frees GPU for frame gen |
| **Plugin cap mode** | `Fixed` at `15` or `20` | Stable low base for high multiplier |
| **Scaling Type** | Off (no upscaling) | Upscale + FG too expensive — let monitor stretch |
| **LSFG version** | **LSFG 2.3** | Lighter than 3.1; still good quality |
| **LSFG mode** | Fixed ×3 or ×4 | 15 fps × 4 = 60 fps output |
| **Flow Scale** | **50%** | Cuts FG GPU cost significantly |
| **Performance Mode** | **ON** | Up to 2× GPU load reduction |
| **Capture API** | DXGI (Win10/11 pre-24H2) or WGC (Win11 24H2+) | WGC has a capture bug on older Windows |
| **Max Frame Latency** | **3** | More buffer for slow GPUs; reduces stuttering |
| **Queue Target** | **2** | Extra buffer; higher latency is acceptable at this tier |

**Expectations:** 15–20 base → 45–60 felt fps. Playable, with noticeable input latency from high multipliers. Better than unplayable slideshow without LS.

</details>

<details>
<summary><b>Low-end (GTX 1050 / GTX 1650 / RX 560 — base fps 30–50)</b></summary>

Enough headroom for ×2 frame gen. Empirically validated on a GTX 1650 Ti with PEAK.

| Setting | Value | Why |
|---|---|---|
| **PEAK resolution** | 1280×720 (best) or 1080p | 720p frees ~15 fps of headroom |
| **Plugin cap mode** | `HalfRefresh` (default) | Auto-detects monitor; stable input for FG |
| **Scaling Type** | **Off** | Do NOT combine upscaling + FG on this tier |
| **LSFG version** | **LSFG 3.1** | Best quality; GPU cost manageable at ×2 |
| **LSFG mode** | **Fixed ×2** | Most stable; lowest latency |
| **Flow Scale** | **75–85%** | Good quality without overloading GPU |
| **Performance Mode** | **ON** | Recommended; slight quality loss, big headroom gain |
| **Capture API** | WGC (Win11 24H2+) or DXGI | WGC preferred when available |
| **Max Frame Latency** | **2** | Balance of latency and stability |
| **Queue Target** | **1** | Balanced |

**Measured results (GTX 1650 Ti, PEAK):**

| Config | Base (real) | Output (felt) |
|---|---|---|
| No LS | ~60 | ~60 |
| Upscale LS1 ×1.5 + FG ×2 | 32 | 65 |
| FG ×2 only @1080p | 47–52 | 105 peak |
| **FG ×2 only @720p (best)** | ~45 | **~90 stable** |

</details>

<details>
<summary><b>Mid-range (GTX 1660 / RTX 2060 / RX 5600 XT — base fps 50–80)</b></summary>

Comfortable headroom. Can use highest quality settings.

| Setting | Value | Why |
|---|---|---|
| **PEAK resolution** | 1080p or 1440p | Native resolution works fine |
| **Plugin cap mode** | `HalfRefresh` (default) | Clean base for FG |
| **Scaling Type** | Off (or LS1 ×1.5 if you want extra sharpness) | Upscaling viable here but optional |
| **LSFG version** | **LSFG 3.1** | Best quality, reduced artifacts |
| **LSFG mode** | **Fixed ×2** | Stable and responsive |
| **Flow Scale** | **100%** (1080p) / **75%** (1440p) | Full quality at 1080p; save GPU at 1440p |
| **Performance Mode** | Off | Enough headroom; maximize quality |
| **Capture API** | **WGC** (Win11 24H2+) | Modern, stable |
| **Max Frame Latency** | **1** (Nvidia) / **2** (AMD) | Low latency |
| **Queue Target** | **1** | Balanced |

**Expectations:** 50–80 base → 100–160 felt fps. Smooth, responsive gameplay.

</details>

<details>
<summary><b>High-end (RTX 3060+ / RX 6700 XT+ — base fps 80+)</b></summary>

More GPU than PEAK needs. LS becomes a luxury for ultra-smooth feel.

| Setting | Value | Why |
|---|---|---|
| **PEAK resolution** | 1440p or 4K | Native or even DSR |
| **Plugin cap mode** | `HalfRefresh` or `Off` | `Off` if base already exceeds half refresh |
| **Scaling Type** | Off | No need at this performance level |
| **LSFG version** | **LSFG 3.1** | Maximum quality |
| **LSFG mode** | **Fixed ×2** | Best balance; ×3 only if chasing 360 Hz |
| **Flow Scale** | **100%** (1080/1440p) / **75%** (4K) | Full quality |
| **Performance Mode** | Off | Unnecessary |
| **Capture API** | **WGC** | Modern, lower CPU overhead |
| **Max Frame Latency** | **1** | Minimum latency |
| **Queue Target** | **0** | Lowest latency; GPU can handle it |

**Expectations:** 80+ base → 160+ felt fps. Buttery smooth.

</details>

<details>
<summary><b>Dual GPU setup (offload FG to a second GPU)</b></summary>

If you have a second GPU (even an old one or an iGPU), you can offload frame generation entirely — eliminating GPU contention.

1. Connect display to the **secondary GPU** (the one doing FG), not the rendering GPU
2. In Windows 11: Settings → Display → Graphics → set PEAK to use the **primary (stronger) GPU** for rendering
3. In Lossless Scaling: Settings → GPU & Display → select the **secondary GPU**
4. Restart PC

**PCIe bandwidth requirements:**
| Slot | Max output |
|---|---|
| PCIe 3.0 ×4 | 1080p @360 fps / 1440p @230 fps |
| PCIe 4.0 ×4 | 1080p @540 fps / 1440p @320 fps |

**Tips:**
- Intel/AMD iGPUs often outperform old Nvidia cards for FG workloads
- CPU impact: 1–15% depending on bottleneck
- AMD render + Nvidia secondary may cause launch failures

</details>

### Key rules

1. **GPU usage must stay below ~90%** — if the game + LS saturate the GPU, everything stutters
2. **Never use exclusive fullscreen** — LS cannot capture it
3. **Lower game resolution before lowering game settings** — resolution frees more GPU than reducing shadows/effects
4. **×2 is almost always better than ×3 or ×4** — higher multipliers add latency and artifacts
5. **A stable base fps matters more than a high base fps** — cap lower if it reduces frame time variance

## Config options

| Option | Default | Description |
|---|---|---|
| `FixMaximumDeltaTime` | `true` | Stops the slow-motion bug. Keep on. |
| `MaximumDeltaTime` | `0.5` | Max real seconds per frame accepted (0.1–5.0). Lower = less physics catch-up risk; higher = covers longer stalls. |
| `ForceRunInBackground` | `true` | Keeps sim running while LS overlay holds focus. |
| `ForceTargetFrameRateMode` | `HalfRefresh` | Base framerate cap: `Off` / `Fixed` / `HalfRefresh`. |
| `ForceTargetFrameRateValue` | `60` | Cap value when mode is `Fixed`. |
| `EnableOverlay` | `false` | Status panel. Press **F8** to toggle in game. **F9** for technical details. |

## Evidence

Measured on a GTX 1650 Ti laptop, PEAK @1080p, LSFG ×2 active, using PeakTimeDiag (custom BepInEx telemetry plugin, 1 sample/s). Same machine, two sessions: vanilla vs. fixed.

![FixedUpdate rate with and without the plugin](docs/fixedupdate-rate.png)

**Without the plugin:** frames render at ~55 fps but the physics loop (`FixedUpdate`) collapses from 60 Hz to **0.2–0.3 Hz** — the renderer draws new frames while the simulation advances at ~0.5% of real speed. **With the plugin:** `FixedUpdate` holds 60 Hz as long as the game window is focused.

![Simulation clock drift](docs/clock-drift.png)

**Without the plugin:** the simulation clock falls 465 s behind real time in 18 minutes (the game world skips ~8 minutes of physics). **With the plugin:** drift stays flat during focused play. Grey periods are background-throttled windows — the plugin forces `runInBackground` to minimize this, and the LS overlay setup matters here.

Raw CSVs and the plotting script are in [`tools/`](tools/) — regenerate with:

```bash
python tools/plot_evidence.py tools/PeakTimeDiag.csv tools/PeakTimeDiag2.csv
```

## Build from source

```
csc.exe -nologo -nostdlib- -noconfig -target:library -optimize+ ^
  -r:netstandard.dll -r:System.dll -r:BepInEx.dll ^
  -r:UnityEngine.CoreModule.dll -r:UnityEngine.dll -r:UnityEngine.IMGUIModule.dll ^
  -out:PeakLSCompat.dll Plugin.cs
```

## License

MIT — see [LICENSE](LICENSE).
