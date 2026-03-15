using UnityEngine;

namespace ProjectLegacy.Util
{
    /// <summary>
    /// Wraps Android's haptic feedback APIs to provide tactile feedback
    /// for combat, UI interactions, and game events. Falls back to
    /// Unity's Handheld.Vibrate() when the native API is unavailable.
    /// Respects the user's haptic preference setting.
    /// </summary>
    public static class HapticFeedback
    {
        private const string LogPrefix = "[ProjectLegacy]";

        private static bool _initialized;
        private static bool _enabled = true;

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject _vibrator;
        private static bool _hasVibrator;
        private static bool _hasAmplitudeControl;
#endif

        /// <summary>
        /// Whether haptic feedback is enabled. Reads from LegacySettings
        /// if available, otherwise defaults to true.
        /// </summary>
        public static bool IsEnabled
        {
            get
            {
                if (Core.LegacyBootstrapper.Instance != null &&
                    Core.LegacyBootstrapper.Instance.Settings != null)
                {
                    return Core.LegacyBootstrapper.Instance.Settings.HapticsEnabled;
                }
                return _enabled;
            }
            set => _enabled = value;
        }

        /// <summary>
        /// Initializes the haptic feedback system. Called automatically
        /// on first use if not already initialized.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
                return;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    _hasVibrator = _vibrator != null && _vibrator.Call<bool>("hasVibrator");

                    if (AndroidVersion >= 26)
                    {
                        _hasAmplitudeControl = _vibrator.Call<bool>("hasAmplitudeControl");
                    }
                }

                Debug.Log($"{LogPrefix} Haptic feedback initialized — hasVibrator: {_hasVibrator}, amplitudeControl: {_hasAmplitudeControl}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"{LogPrefix} Failed to initialize haptic feedback: {e.Message}");
                _hasVibrator = false;
            }
#endif

            _initialized = true;
        }

        /// <summary>
        /// Light tap — used for UI button presses and minor interactions.
        /// Duration: ~10ms, low intensity.
        /// </summary>
        public static void LightTap()
        {
            if (!IsEnabled) return;
            Vibrate(10, 80);
        }

        /// <summary>
        /// Medium tap — used for combat hits and significant interactions.
        /// Duration: ~20ms, medium intensity.
        /// </summary>
        public static void MediumTap()
        {
            if (!IsEnabled) return;
            Vibrate(20, 150);
        }

        /// <summary>
        /// Heavy tap — used for critical hits, blocks, and important events.
        /// Duration: ~40ms, high intensity.
        /// </summary>
        public static void HeavyTap()
        {
            if (!IsEnabled) return;
            Vibrate(40, 255);
        }

        /// <summary>
        /// Success pattern — used for quest completions, level ups, etc.
        /// Two short pulses.
        /// </summary>
        public static void Success()
        {
            if (!IsEnabled) return;
            VibratePattern(new long[] { 0, 15, 80, 15 }, new int[] { 0, 150, 0, 200 }, -1);
        }

        /// <summary>
        /// Error pattern — used for failed actions.
        /// Three rapid short pulses.
        /// </summary>
        public static void Error()
        {
            if (!IsEnabled) return;
            VibratePattern(new long[] { 0, 10, 50, 10, 50, 10 }, new int[] { 0, 200, 0, 200, 0, 200 }, -1);
        }

        /// <summary>
        /// Performs a vibration with the specified duration and amplitude.
        /// </summary>
        /// <param name="durationMs">Vibration duration in milliseconds.</param>
        /// <param name="amplitude">Vibration amplitude (1-255). Ignored on devices without amplitude control.</param>
        public static void Vibrate(int durationMs, int amplitude = -1)
        {
            EnsureInitialized();

#if UNITY_ANDROID && !UNITY_EDITOR
            if (!_hasVibrator) return;

            try
            {
                if (_hasAmplitudeControl && amplitude > 0 && AndroidVersion >= 26)
                {
                    // Use VibrationEffect for fine-grained control (API 26+)
                    using (var vibrationEffect = new AndroidJavaClass("android.os.VibrationEffect"))
                    {
                        var effect = vibrationEffect.CallStatic<AndroidJavaObject>(
                            "createOneShot", (long)durationMs, Mathf.Clamp(amplitude, 1, 255));
                        _vibrator.Call("vibrate", effect);
                    }
                }
                else
                {
                    // Legacy vibrate
                    _vibrator.Call("vibrate", (long)durationMs);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"{LogPrefix} Vibration failed: {e.Message}");
            }
#else
            // Editor/non-Android fallback
            if (durationMs > 20)
            {
                Handheld.Vibrate();
            }
#endif
        }

        /// <summary>
        /// Performs a vibration pattern.
        /// </summary>
        /// <param name="pattern">Alternating wait/vibrate durations in milliseconds.</param>
        /// <param name="amplitudes">Amplitude for each vibration segment (API 26+).</param>
        /// <param name="repeat">Index to repeat from, or -1 for no repeat.</param>
        public static void VibratePattern(long[] pattern, int[] amplitudes, int repeat)
        {
            EnsureInitialized();

#if UNITY_ANDROID && !UNITY_EDITOR
            if (!_hasVibrator) return;

            try
            {
                if (_hasAmplitudeControl && AndroidVersion >= 26)
                {
                    using (var vibrationEffect = new AndroidJavaClass("android.os.VibrationEffect"))
                    {
                        var effect = vibrationEffect.CallStatic<AndroidJavaObject>(
                            "createWaveform", pattern, amplitudes, repeat);
                        _vibrator.Call("vibrate", effect);
                    }
                }
                else
                {
                    _vibrator.Call("vibrate", pattern, repeat);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"{LogPrefix} Vibration pattern failed: {e.Message}");
            }
#else
            Handheld.Vibrate();
#endif
        }

        /// <summary>
        /// Cancels any ongoing vibration.
        /// </summary>
        public static void Cancel()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_vibrator != null)
            {
                try
                {
                    _vibrator.Call("cancel");
                }
                catch (System.Exception) { }
            }
#endif
        }

        private static void EnsureInitialized()
        {
            if (!_initialized)
                Initialize();
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static int AndroidVersion
        {
            get
            {
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    return version.GetStatic<int>("SDK_INT");
                }
            }
        }
#endif
    }
}
