using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace PeakLSCompat
{
    // PEAK LS Compat — fixes the slow-motion bug caused by Lossless Scaling
    // (LSFG frame generation) in PEAK. GPU-agnostic, no graphics APIs touched.
    //
    // Built by @oRadiatorExtrem with assistance from GLM-5.3 Flash (Zhipu AI).
    //
    // Root cause: while LSFG is active, external capture/overlay interference
    // stalls the render pump. Unity clamps Time.deltaTime at
    // Time.maximumDeltaTime (default 0.1667s), so simulation falls behind
    // real time — the whole game runs in slow motion.
    //
    // Fixes applied:
    //   1. Raises Time.maximumDeltaTime so deltaTime reflects real frame
    //      duration during capture stalls -> no slow motion.
    //   2. Forces Application.runInBackground so the simulation keeps
    //      running while the LS overlay holds focus.
    //   3. Auto-caps targetFrameRate (HalfRefresh mode) for stable LSFG input.
    //   4. Logs focus/fullscreen transitions for overlay conflict diagnosis.
    [BepInPlugin("com.black.peaklscompat", "PEAK LS Compat", "0.3.1")]
    public class LSCompatPlugin : BaseUnityPlugin
    {
        internal static LSCompatPlugin Instance;

        private ManualLogSource _log;
        private bool _lastFocused = true;
        private FullScreenMode _lastFsMode;
        private float _nextStateLog;
        private float _lastPumpCheck;
        private int _pumpFrames;
        private int _pendingTargetFps = -1;
        private float _lastRefreshCheck;
        private GameObject _overlayObj;

        // live measurements read by the overlay component
        internal int _pumpFramesFast;
        internal float _pumpFastStart;
        internal float _pumpHzFast;
        internal int _fixedSteps;
        internal float _fixedWinStart;
        internal float _fixedHzMeasured;
        internal float _speedWinStart = -1f;
        internal float _speedSimStart;
        internal float _speedRatio = 1f;
        internal bool _speedReady;
        internal bool _showAdvanced;

        public static ConfigEntry<bool> FixMaximumDeltaTime;
        public static ConfigEntry<float> MaximumDeltaTime;
        public static ConfigEntry<bool> ForceRunInBackground;
        public static ConfigEntry<string> ForceTargetFrameRateMode;
        public static ConfigEntry<int> ForceTargetFrameRateValue;
        public static ConfigEntry<bool> EnableOverlay;
        public static ConfigEntry<float> StateLogInterval;

        private void Awake()
        {
            Instance = this;
            _log = Logger;
            try
            {
                FixMaximumDeltaTime = Config.Bind("Time", "FixMaximumDeltaTime", true,
                    "Raise Time.maximumDeltaTime so deltaTime is not clamped during LS capture stalls (fixes slow motion). Keep true.");
                MaximumDeltaTime = Config.Bind("Time", "MaximumDeltaTime", 0.5f,
                    new ConfigDescription("Value for Time.maximumDeltaTime in seconds. 0.5 covers typical LS stalls while limiting physics catch-up to ~30 steps (avoids spiral of death).",
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
                    "Player-friendly status overlay. Press F8 in game to show/hide it at any time (no config edit needed); F9 toggles technical details. Off by default for maximum performance.");
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
            catch { }

            _log.LogInfo(string.Format(
                "[PeakLSCompat] v{0} ready: maximumDeltaTime={1:F4} runInBackground={2} targetFPS={3} vsync={4}",
                Info.Metadata.Version.ToString(), Time.maximumDeltaTime, Application.runInBackground,
                Application.targetFrameRate, QualitySettings.vSyncCount));
        }

        private void Start()
        {
            _nextStateLog = Time.unscaledTime + Math.Max(1f, StateLogInterval.Value);
            _lastPumpCheck = Time.unscaledTime;
            SyncOverlay();
        }

        private void Update()
        {
            try
            {
                ApplyTimeFixes();
                LogTransitions();
                PumpHealth();
                HandleOverlayKeys();
                UpdateMeasures();
            }
            catch (Exception e)
            {
                _log.LogError("[PeakLSCompat] update error (suppressed): " + e.Message);
            }
        }

        private void FixedUpdate()
        {
            try
            {
                _fixedSteps++;
                float now = Time.unscaledTime;
                if (_fixedWinStart <= 0f) _fixedWinStart = now;
                else if (now - _fixedWinStart >= 1f)
                {
                    _fixedHzMeasured = _fixedSteps / (now - _fixedWinStart);
                    _fixedSteps = 0;
                    _fixedWinStart = now;
                }
            }
            catch { }
        }

        private void HandleOverlayKeys()
        {
            if (EnableOverlay == null) return;
            if (Input.GetKeyDown(KeyCode.F8))
            {
                EnableOverlay.Value = !EnableOverlay.Value;
                SyncOverlay();
                _log.LogInfo("[PeakLSCompat] overlay " + (EnableOverlay.Value ? "ON" : "OFF"));
            }
            if (Input.GetKeyDown(KeyCode.F9)) _showAdvanced = !_showAdvanced;
        }

        // Create/destroy the overlay GameObject so OnGUI only exists when visible.
        // Eliminates ~0.7 KB/frame GC allocation from Unity's IMGUI event system.
        private void SyncOverlay()
        {
            bool want = EnableOverlay != null && EnableOverlay.Value;
            if (want && _overlayObj == null)
            {
                _overlayObj = new GameObject("PeakLSCompat_Overlay");
                _overlayObj.AddComponent<LSCompatOverlay>();
                UnityEngine.Object.DontDestroyOnLoad(_overlayObj);
            }
            else if (!want && _overlayObj != null)
            {
                UnityEngine.Object.Destroy(_overlayObj);
                _overlayObj = null;
            }
        }

        private void UpdateMeasures()
        {
            _pumpFramesFast++;
            float now = Time.unscaledTime;
            if (_pumpFastStart <= 0f) _pumpFastStart = now;
            else if (now - _pumpFastStart >= 1f)
            {
                _pumpHzFast = _pumpFramesFast / (now - _pumpFastStart);
                _pumpFramesFast = 0;
                _pumpFastStart = now;
            }

            if (_speedWinStart < 0f)
            {
                _speedWinStart = now;
                _speedSimStart = Time.time;
            }
            else if (now - _speedWinStart >= 2f)
            {
                float wall = now - _speedWinStart;
                float sim = Time.time - _speedSimStart;
                _speedRatio = wall > 0.001f ? sim / wall : 1f;
                _speedReady = true;
                _speedWinStart = now;
                _speedSimStart = Time.time;
            }
        }

        private void ApplyTimeFixes()
        {
            if (FixMaximumDeltaTime == null || MaximumDeltaTime == null) return;

            if (FixMaximumDeltaTime.Value && Math.Abs(Time.maximumDeltaTime - MaximumDeltaTime.Value) > 0.001f)
            {
                Time.maximumDeltaTime = Math.Max(0.1f, MaximumDeltaTime.Value);
                _log.LogInfo(string.Format("[PeakLSCompat] Time.maximumDeltaTime -> {0:F2}", Time.maximumDeltaTime));
            }

            if (ForceRunInBackground != null && ForceRunInBackground.Value && !Application.runInBackground)
            {
                Application.runInBackground = true;
                _log.LogInfo("[PeakLSCompat] runInBackground -> true");
            }

            int desired = ResolveTargetFps();
            if (desired > 0)
            {
                if (QualitySettings.vSyncCount != 0)
                {
                    _log.LogInfo(string.Format("[PeakLSCompat] disabling vSync (was {0}) so targetFrameRate cap works", QualitySettings.vSyncCount));
                    QualitySettings.vSyncCount = 0;
                }
                if (Application.targetFrameRate != desired)
                {
                    Application.targetFrameRate = desired;
                    _log.LogInfo(string.Format("[PeakLSCompat] targetFrameRate -> {0}", desired));
                }
            }
        }

        private int ResolveTargetFps()
        {
            string mode;
            try { mode = ForceTargetFrameRateMode.Value; } catch { return 0; }

            if (string.Equals(mode, "Off", StringComparison.OrdinalIgnoreCase)) return 0;

            if (string.Equals(mode, "Fixed", StringComparison.OrdinalIgnoreCase))
                return Math.Max(30, Math.Min(240, ForceTargetFrameRateValue.Value));

            if (string.Equals(mode, "HalfRefresh", StringComparison.OrdinalIgnoreCase))
            {
                float now = Time.unscaledTime;
                if (_pendingTargetFps > 0 && now - _lastRefreshCheck < 30f)
                    return _pendingTargetFps;

                int hz = GetRefreshRate();
                if (hz > 30)
                {
                    int cap = hz / 2;
                    if (cap < 30) cap = 30;
                    if (cap > 120) cap = 120;
                    if (cap != _pendingTargetFps)
                        _log.LogInfo(string.Format("[PeakLSCompat] detected refresh {0} Hz -> base cap {1} fps", hz, cap));
                    _pendingTargetFps = cap;
                    _lastRefreshCheck = now;
                    return _pendingTargetFps;
                }
                return 0;
            }
            return 0;
        }

        private static int GetRefreshRate()
        {
            try
            {
                var res = Screen.currentResolution;
                int legacy = res.refreshRate;
                if (legacy > 30 && legacy < 500) return legacy;

                var ratioField = res.GetType().GetField("refreshRateRatio");
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
    }

    // Separate component so OnGUI (and its ~0.7 KB/frame GC allocation) only
    // exists while the overlay is actually visible. Destroyed when F8 hides it.
    internal class LSCompatOverlay : MonoBehaviour
    {
        private GUIStyle _bigStyle;
        private bool _stylesMade;

        private void OnGUI()
        {
            try
            {
                var p = LSCompatPlugin.Instance;
                if (p == null) return;
                GUILayout.Window(0x5D1A6, new Rect(10f, 10f, 470f, 230f), (GUI.WindowFunction)DrawWindow, "PEAK + Lossless Scaling");
            }
            catch { }
        }

        private void DrawWindow(int id)
        {
            var p = LSCompatPlugin.Instance;
            if (p == null) return;

            if (!_stylesMade)
            {
                _bigStyle = new GUIStyle(GUI.skin.label);
                _bigStyle.fontSize = 17;
                _bigStyle.fontStyle = FontStyle.Bold;
                _stylesMade = true;
            }

            float expectedFixed = Time.fixedDeltaTime > 0f ? 1f / Time.fixedDeltaTime : 60f;

            string speedText;
            Color c;
            if (!p._speedReady) { speedText = "measuring..."; c = Color.yellow; }
            else if (p._speedRatio >= 0.95f && p._fixedHzMeasured >= expectedFixed * 0.5f)
            { speedText = "OK - full speed"; c = new Color(0.35f, 0.85f, 0.4f); }
            else if (p._speedRatio >= 0.85f)
            { speedText = "slightly behind"; c = new Color(0.95f, 0.8f, 0.2f); }
            else
            { speedText = "SLOW - the slow-motion bug"; c = new Color(1f, 0.35f, 0.3f); }

            _bigStyle.normal.textColor = c;
            GUILayout.Label("Game speed: " + speedText, _bigStyle);
            GUILayout.Space(8f);

            GUILayout.Label(string.Format("Frames: {0} fps", p._pumpHzFast.ToString("F0")));
            GUILayout.Label(string.Format("Game world: {0} steps/s (normal: {1})",
                p._fixedHzMeasured.ToString("F0"), expectedFixed.ToString("F0")));
            GUILayout.Label("Window focused: " + (Application.isFocused ? "yes" : "no"));

            GUILayout.Space(6f);
            GUILayout.Label("F8: show/hide   F9: technical details", GUILayout.Width(430f));

            if (p._showAdvanced)
            {
                GUILayout.Space(6f);
                GUILayout.Label(string.Format(
                    "maxDeltaTime: {0:F2} | targetFPS: {1} | cap: {2}\nspeed ratio: {3:F2} | world lag: {4:F1}s | focused: {5}",
                    Time.maximumDeltaTime, Application.targetFrameRate,
                    LSCompatPlugin.ForceTargetFrameRateMode.Value, p._speedRatio,
                    Time.unscaledTime - Time.time, Application.isFocused));
            }

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }
    }
}
