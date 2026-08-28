using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace PeakLSCompat
{
    // PEAK LS Compat — makes Lossless Scaling (LSFG frame generation) safe to
    // use with PEAK on any GPU.
    //
    // Problem it fixes (measured with PeakTimeDiag, r/PeakGame 1ndv7sw):
    //   While LSFG is active, external capture/overlay interference can stall
    //   or slow the game's render pump. Unity clamps Time.deltaTime at
    //   Time.maximumDeltaTime (default 0.1667s), so simulation time falls
    //   behind real time and the whole game (physics via FixedUpdate,
    //   movement, timers) runs in slow motion.
    //
    // What this plugin does (GPU-agnostic, no graphics APIs touched):
    //   1. Raises Time.maximumDeltaTime so deltaTime reflects the real frame
    //      duration even during present/capture stalls -> no slow motion.
    //   2. Forces Application.runInBackground = true so the simulation keeps
    //      running while the LS overlay/window holds focus.
    //   3. Optionally caps Application.targetFrameRate to a stable base
    //      framerate (recommended: half the monitor refresh) so frame
    //      generation has a clean, steady input.
    //   4. Logs focus/fullscreen transitions to help diagnose external
    //      overlay conflicts (RTSS/Afterburner hooks are known to break LS
    //      capture -> disable them for this game).
    //
    // Recommended Lossless Scaling settings for weak GPUs (e.g. GTX 1650 Ti):
    //   - Borderless fullscreen, CaptureApi WGC (Win11 24H2+), MaxFrameLatency 2
    //   - G-Sync support OFF unless the display actually supports G-Sync
    //   - Upscale first (Scaling Type LS1, factor ~1.5), then LSFG Fixed x2
    //   - Close MSI Afterburner / RTSS while playing (their Present hooks
    //     corrupt LS capture -> blur/ghosting artifacts)
    [BepInPlugin("com.black.peaklscompat", "PEAK LS Compat", "0.2.0")]
    public class LSCompatPlugin : BaseUnityPlugin
    {
        private ManualLogSource _log;
        private bool _lastFocused = true;
        private FullScreenMode _lastFsMode;
        private float _nextStateLog;
        private float _lastPumpCheck;
        private int _pumpFrames;
        private int _pendingTargetFps = -1;

        public static ConfigEntry<bool> FixMaximumDeltaTime;
        public static ConfigEntry<float> MaximumDeltaTime;
        public static ConfigEntry<bool> ForceRunInBackground;
        public static ConfigEntry<string> ForceTargetFrameRateMode; // Off / Fixed / HalfRefresh
        public static ConfigEntry<int> ForceTargetFrameRateValue;
        public static ConfigEntry<bool> EnableOverlay;
        public static ConfigEntry<float> StateLogInterval;

        private void Awake()
        {
            _log = Logger;
            try
            {
                FixMaximumDeltaTime = Config.Bind("Time", "FixMaximumDeltaTime", true,
                    "Raise Time.maximumDeltaTime so deltaTime is not clamped during LS capture stalls (fixes slow motion). Keep true.");
                MaximumDeltaTime = Config.Bind("Time", "MaximumDeltaTime", 1.0f,
                    new ConfigDescription("Value for Time.maximumDeltaTime in seconds. 1.0 covers stalls up to 1 s per frame.",
                        new AcceptableValueRange<float>(0.1f, 5f)));
                ForceRunInBackground = Config.Bind("Time", "ForceRunInBackground", true,
                    "Force Application.runInBackground = true (keeps the sim running while the LS overlay holds focus).");
                ForceTargetFrameRateMode = Config.Bind("Time", "ForceTargetFrameRateMode", "HalfRefresh",
                    new ConfigDescription("Cap the game's targetFrameRate for a stable LSFG input base. Off = leave game setting; Fixed = use ForceTargetFrameRateValue; HalfRefresh = half the detected monitor refresh rate.",
                        new AcceptableValueList<string>("Off", "Fixed", "HalfRefresh")));
                ForceTargetFrameRateValue = Config.Bind("Time", "ForceTargetFrameRateValue", 60,
                    new ConfigDescription("Fixed cap used when ForceTargetFrameRateMode = Fixed.",
                        new AcceptableValueRange<int>(30, 240)));
                EnableOverlay = Config.Bind("Diagnostics", "EnableOverlay", false,
                    "On-screen status overlay (toggle with F8). Costs a little CPU when on; off by default for maximum performance.");
                StateLogInterval = Config.Bind("Diagnostics", "StateLogInterval", 5f,
                    new ConfigDescription("Seconds between periodic state log lines (BepInEx log).",
                        new AcceptableValueRange<float>(1f, 60f)));
            }
            catch (Exception e)
            {
                _log.LogError("[PeakLSCompat] config load failed, using built-in safe defaults: " + e.Message);
            }

            try
            {
                _lastFsMode = Screen.fullScreenMode;
            }
            catch { /* headless/odd setups */ }

            _log.LogInfo(string.Format(
                "[PeakLSCompat] v{0} ready: maximumDeltaTime={1:F4} runInBackground={2} targetFPS={3} vsync={4}",
                Info.Metadata.Version.ToString(), Time.maximumDeltaTime, Application.runInBackground,
                Application.targetFrameRate, QualitySettings.vSyncCount));
        }

        private void Start()
        {
            _nextStateLog = Time.unscaledTime + Math.Max(1f, StateLogInterval.Value);
            _lastPumpCheck = Time.unscaledTime;
        }

        private void Update()
        {
            try
            {
                ApplyTimeFixes();
                LogTransitions();
                PumpHealth();
            }
            catch (Exception e)
            {
                // never let the compat layer kill the game loop
                _log.LogError("[PeakLSCompat] update error (suppressed): " + e.Message);
            }
        }

        private void ApplyTimeFixes()
        {
            if (FixMaximumDeltaTime.Value && Math.Abs(Time.maximumDeltaTime - MaximumDeltaTime.Value) > 0.001f)
            {
                Time.maximumDeltaTime = Math.Max(0.1f, MaximumDeltaTime.Value);
                _log.LogInfo(string.Format("[PeakLSCompat] Time.maximumDeltaTime -> {0:F2}", Time.maximumDeltaTime));
            }

            if (ForceRunInBackground.Value && !Application.runInBackground)
            {
                Application.runInBackground = true;
                _log.LogInfo("[PeakLSCompat] runInBackground -> true");
            }

            int desired = ResolveTargetFps();
            if (desired > 0 && Application.targetFrameRate != desired)
            {
                Application.targetFrameRate = desired;
                _log.LogInfo(string.Format("[PeakLSCompat] targetFrameRate -> {0}", desired));
            }
        }

        // Returns 0 when no cap should be applied.
        private int ResolveTargetFps()
        {
            string mode;
            try { mode = ForceTargetFrameRateMode.Value; } catch { return 0; }

            if (string.Equals(mode, "Off", StringComparison.OrdinalIgnoreCase)) return 0;

            if (string.Equals(mode, "Fixed", StringComparison.OrdinalIgnoreCase))
                return Math.Max(30, Math.Min(240, ForceTargetFrameRateValue.Value));

            if (string.Equals(mode, "HalfRefresh", StringComparison.OrdinalIgnoreCase))
            {
                if (_pendingTargetFps > 0) return _pendingTargetFps;
                int hz = GetRefreshRate();
                if (hz > 30)
                {
                    _pendingTargetFps = hz / 2; // integer division; 144 -> 72, 60 -> 30
                    // avoid absurd caps from odd refresh values
                    if (_pendingTargetFps < 30) _pendingTargetFps = 30;
                    if (_pendingTargetFps > 120) _pendingTargetFps = 120;
                    _log.LogInfo(string.Format("[PeakLSCompat] detected refresh {0} Hz -> base cap {1} fps", hz, _pendingTargetFps));
                    return _pendingTargetFps;
                }
                return 0; // unknown refresh; leave the game's own setting
            }
            return 0;
        }

        // Best-effort monitor refresh rate detection (no external dependencies).
        private static int GetRefreshRate()
        {
            try
            {
                var res = Screen.currentResolution;
                int legacy = res.refreshRate; // deprecated in Unity 6 but functional
                if (legacy > 30 && legacy < 500) return legacy;

                // Unity 6: refreshRateRatio (numerator/denominator) via reflection
                var ratioField = res.GetType().GetField("refreshRateRatio");
                if (ratioField == null) ratioField = (System.Reflection.FieldInfo)(object)null;
                object ratio = ratioField != null ? ratioField.GetValue(res) : null;
                if (ratio == null)
                {
                    var prop = res.GetType().GetProperty("refreshRateRatio");
                    if (prop != null) ratio = prop.GetValue(res, null);
                }
                if (ratio != null)
                {
                    object num = null, den = null;
                    foreach (var m in ratio.GetType().GetFields())
                    {
                        if (m.Name == "numerator") num = m.GetValue(ratio);
                        if (m.Name == "denominator") den = m.GetValue(ratio);
                    }
                    foreach (var m in ratio.GetType().GetProperties())
                    {
                        if (m.Name == "numerator") num = m.GetValue(ratio, null);
                        if (m.Name == "denominator") den = m.GetValue(ratio, null);
                    }
                    if (num != null && den != null)
                    {
                        long n = Convert.ToInt64(num);
                        long d = Convert.ToInt64(den);
                        if (d > 0 && n > 0) return (int)Math.Round((double)n / d);
                    }
                }
            }
            catch { }
            return 0;
        }

        private void LogTransitions()
        {
            bool focused = Application.isFocused;
            if (focused != _lastFocused)
            {
                _log.LogInfo(string.Format("[PeakLSCompat] FOCUS CHANGE: {0}", focused));
                _lastFocused = focused;
            }
            var fsMode = Screen.fullScreenMode;
            if (fsMode != _lastFsMode)
            {
                _log.LogInfo(string.Format("[PeakLSCompat] FULLSCREEN MODE CHANGE: {0} -> {1}", _lastFsMode, fsMode));
                _lastFsMode = fsMode;
            }
        }

        private void PumpHealth()
        {
            _pumpFrames++;
            float now = Time.unscaledTime;
            if (now >= _nextStateLog)
            {
                float window = now - _lastPumpCheck;
                float pumpHz = window > 0f ? _pumpFrames / window : 0f;
                _log.LogInfo(string.Format(
                    "[PeakLSCompat] state: pump={0:F1}Hz maxDt={1:F3} targetFPS={2} focused={3} fsMode={4} runInBg={5}",
                    pumpHz, Time.maximumDeltaTime, Application.targetFrameRate,
                    Application.isFocused, Screen.fullScreenMode, Application.runInBackground));
                _pumpFrames = 0;
                _lastPumpCheck = now;
                _nextStateLog = now + Math.Max(1f, StateLogInterval.Value);
            }
        }

        private void OnGUI()
        {
            try
            {
                if (EnableOverlay == null || !EnableOverlay.Value) return;
                GUILayout.Window(0x5D1A6, new Rect(10f, 10f, 480f, 150f), (GUI.WindowFunction)DrawWindow, "PEAK LS Compat");
            }
            catch { }
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label(string.Format(
                "PEAK LS Compat\nmaxDt: {0:F2}\ntargetFPS: {1}\nmode: {2}\nfocused: {3}",
                Time.maximumDeltaTime, Application.targetFrameRate,
                ForceTargetFrameRateMode.Value, Application.isFocused));
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }
    }
}
