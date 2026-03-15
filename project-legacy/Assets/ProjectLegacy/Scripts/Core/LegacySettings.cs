using UnityEngine;

namespace ProjectLegacy.Core
{
    /// <summary>
    /// Central settings store for all Project Legacy preferences.
    /// Wraps PlayerPrefs with typed accessors and sensible defaults.
    /// Can also be used as a ScriptableObject for editor-defined defaults.
    /// </summary>
    [CreateAssetMenu(fileName = "LegacyDefaults", menuName = "Project Legacy/Settings")]
    public class LegacySettings : ScriptableObject
    {
        private const string LogPrefix = "[ProjectLegacy]";
        private const string PrefsPrefix = "ProjectLegacy_";

        // --- Display ---

        [Header("Display")]
        [SerializeField]
        [Range(45f, 65f)]
        [Tooltip("Camera field of view in portrait mode.")]
        private float _portraitFOV = 55f;

        [SerializeField]
        [Range(0.5f, 1.0f)]
        [Tooltip("Render resolution scale. Lower values improve performance.")]
        private float _renderScale = 1.0f;

        // --- Controls ---

        [Header("Controls")]
        [SerializeField]
        [Range(0.1f, 3.0f)]
        [Tooltip("Camera look sensitivity multiplier.")]
        private float _lookSensitivity = 1.0f;

        [SerializeField]
        [Range(0.1f, 3.0f)]
        [Tooltip("Movement joystick sensitivity multiplier.")]
        private float _moveSensitivity = 1.0f;

        [SerializeField]
        [Tooltip("Invert the Y-axis for camera look.")]
        private bool _invertYAxis = false;

        [SerializeField]
        [Range(0.5f, 0.8f)]
        [Tooltip("Percentage of viewport height assigned to the look zone (top). Remainder is move zone.")]
        private float _lookZoneRatio = 0.6f;

        [SerializeField]
        [Range(40f, 120f)]
        [Tooltip("Maximum radius in pixels for the floating joystick.")]
        private float _joystickMaxRadius = 80f;

        // --- Gestures ---

        [Header("Gesture Thresholds")]
        [SerializeField]
        [Range(0.1f, 0.4f)]
        [Tooltip("Maximum duration in seconds for a tap gesture.")]
        private float _tapMaxDuration = 0.2f;

        [SerializeField]
        [Range(5f, 20f)]
        [Tooltip("Maximum movement in pixels for a tap gesture.")]
        private float _tapMaxDistance = 10f;

        [SerializeField]
        [Range(20f, 80f)]
        [Tooltip("Minimum movement in pixels for a swipe gesture.")]
        private float _swipeMinDistance = 40f;

        [SerializeField]
        [Range(0.15f, 0.5f)]
        [Tooltip("Maximum duration in seconds for a swipe gesture.")]
        private float _swipeMaxDuration = 0.3f;

        [SerializeField]
        [Range(0.3f, 1.0f)]
        [Tooltip("Minimum hold duration in seconds for a long-press gesture.")]
        private float _longPressMinDuration = 0.5f;

        [SerializeField]
        [Range(0.2f, 0.6f)]
        [Tooltip("Maximum gap in seconds between taps for a double-tap gesture.")]
        private float _doubleTapWindow = 0.4f;

        // --- Combat ---

        [Header("Combat")]
        [SerializeField]
        [Tooltip("Enable auto-aim assist.")]
        private bool _autoAimEnabled = true;

        [SerializeField]
        [Range(0f, 0.25f)]
        [Tooltip("Auto-aim nudge strength. Higher values are more aggressive.")]
        private float _autoAimStrength = 0.1f;

        [SerializeField]
        [Range(10f, 25f)]
        [Tooltip("Distance in meters at which lock-on breaks.")]
        private float _lockOnBreakDistance = 15f;

        // --- HUD ---

        [Header("HUD")]
        [SerializeField]
        [Tooltip("Show the optional action button overlay.")]
        private bool _showActionButtons = true;

        [SerializeField]
        [Range(0.3f, 1.0f)]
        [Tooltip("Opacity of the action button overlay.")]
        private float _buttonOpacity = 0.7f;

        [SerializeField]
        [Tooltip("Show the compass strip at the top of the viewport.")]
        private bool _showCompass = true;

        // --- Haptics ---

        [Header("Haptics")]
        [SerializeField]
        [Tooltip("Enable haptic feedback.")]
        private bool _hapticsEnabled = true;

        // --- Public Accessors ---

        /// <summary>Portrait camera FOV.</summary>
        public float PortraitFOV { get => _portraitFOV; set { _portraitFOV = Mathf.Clamp(value, 45f, 65f); Save(); } }

        /// <summary>Render resolution scale.</summary>
        public float RenderScale { get => _renderScale; set { _renderScale = Mathf.Clamp(value, 0.5f, 1f); Save(); } }

        /// <summary>Camera look sensitivity.</summary>
        public float LookSensitivity { get => _lookSensitivity; set { _lookSensitivity = Mathf.Clamp(value, 0.1f, 3f); Save(); } }

        /// <summary>Movement sensitivity.</summary>
        public float MoveSensitivity { get => _moveSensitivity; set { _moveSensitivity = Mathf.Clamp(value, 0.1f, 3f); Save(); } }

        /// <summary>Whether Y-axis is inverted for camera look.</summary>
        public bool InvertYAxis { get => _invertYAxis; set { _invertYAxis = value; Save(); } }

        /// <summary>Ratio of viewport height assigned to look zone.</summary>
        public float LookZoneRatio { get => _lookZoneRatio; set { _lookZoneRatio = Mathf.Clamp(value, 0.5f, 0.8f); Save(); } }

        /// <summary>Maximum joystick drag radius in pixels.</summary>
        public float JoystickMaxRadius { get => _joystickMaxRadius; set { _joystickMaxRadius = Mathf.Clamp(value, 40f, 120f); Save(); } }

        /// <summary>Tap gesture maximum duration.</summary>
        public float TapMaxDuration => _tapMaxDuration;

        /// <summary>Tap gesture maximum movement distance.</summary>
        public float TapMaxDistance => _tapMaxDistance;

        /// <summary>Swipe gesture minimum distance.</summary>
        public float SwipeMinDistance => _swipeMinDistance;

        /// <summary>Swipe gesture maximum duration.</summary>
        public float SwipeMaxDuration => _swipeMaxDuration;

        /// <summary>Long-press gesture minimum hold duration.</summary>
        public float LongPressMinDuration => _longPressMinDuration;

        /// <summary>Double-tap gesture maximum gap between taps.</summary>
        public float DoubleTapWindow => _doubleTapWindow;

        /// <summary>Whether auto-aim is enabled.</summary>
        public bool AutoAimEnabled { get => _autoAimEnabled; set { _autoAimEnabled = value; Save(); } }

        /// <summary>Auto-aim nudge strength.</summary>
        public float AutoAimStrength { get => _autoAimStrength; set { _autoAimStrength = Mathf.Clamp(value, 0f, 0.25f); Save(); } }

        /// <summary>Lock-on break distance in meters.</summary>
        public float LockOnBreakDistance { get => _lockOnBreakDistance; set { _lockOnBreakDistance = Mathf.Clamp(value, 10f, 25f); Save(); } }

        /// <summary>Whether action buttons are visible.</summary>
        public bool ShowActionButtons { get => _showActionButtons; set { _showActionButtons = value; Save(); } }

        /// <summary>Action button opacity.</summary>
        public float ButtonOpacity { get => _buttonOpacity; set { _buttonOpacity = Mathf.Clamp(value, 0.3f, 1f); Save(); } }

        /// <summary>Whether the compass strip is visible.</summary>
        public bool ShowCompass { get => _showCompass; set { _showCompass = value; Save(); } }

        /// <summary>Whether haptic feedback is enabled.</summary>
        public bool HapticsEnabled { get => _hapticsEnabled; set { _hapticsEnabled = value; Save(); } }

        /// <summary>
        /// Loads settings from PlayerPrefs, falling back to ScriptableObject defaults.
        /// </summary>
        /// <returns>A LegacySettings instance with persisted values.</returns>
        public static LegacySettings Load()
        {
            var settings = Resources.Load<LegacySettings>("LegacyDefaults");
            if (settings == null)
            {
                settings = CreateInstance<LegacySettings>();
                Debug.Log($"{LogPrefix} No LegacyDefaults asset found — using built-in defaults.");
            }

            settings.LoadFromPlayerPrefs();
            return settings;
        }

        /// <summary>
        /// Persists current settings to PlayerPrefs.
        /// </summary>
        public void Save()
        {
            PlayerPrefs.SetFloat(PrefsPrefix + "PortraitFOV", _portraitFOV);
            PlayerPrefs.SetFloat(PrefsPrefix + "RenderScale", _renderScale);
            PlayerPrefs.SetFloat(PrefsPrefix + "LookSensitivity", _lookSensitivity);
            PlayerPrefs.SetFloat(PrefsPrefix + "MoveSensitivity", _moveSensitivity);
            PlayerPrefs.SetInt(PrefsPrefix + "InvertYAxis", _invertYAxis ? 1 : 0);
            PlayerPrefs.SetFloat(PrefsPrefix + "LookZoneRatio", _lookZoneRatio);
            PlayerPrefs.SetFloat(PrefsPrefix + "JoystickMaxRadius", _joystickMaxRadius);
            PlayerPrefs.SetFloat(PrefsPrefix + "TapMaxDuration", _tapMaxDuration);
            PlayerPrefs.SetFloat(PrefsPrefix + "TapMaxDistance", _tapMaxDistance);
            PlayerPrefs.SetFloat(PrefsPrefix + "SwipeMinDistance", _swipeMinDistance);
            PlayerPrefs.SetFloat(PrefsPrefix + "SwipeMaxDuration", _swipeMaxDuration);
            PlayerPrefs.SetFloat(PrefsPrefix + "LongPressMinDuration", _longPressMinDuration);
            PlayerPrefs.SetFloat(PrefsPrefix + "DoubleTapWindow", _doubleTapWindow);
            PlayerPrefs.SetInt(PrefsPrefix + "AutoAimEnabled", _autoAimEnabled ? 1 : 0);
            PlayerPrefs.SetFloat(PrefsPrefix + "AutoAimStrength", _autoAimStrength);
            PlayerPrefs.SetFloat(PrefsPrefix + "LockOnBreakDistance", _lockOnBreakDistance);
            PlayerPrefs.SetInt(PrefsPrefix + "ShowActionButtons", _showActionButtons ? 1 : 0);
            PlayerPrefs.SetFloat(PrefsPrefix + "ButtonOpacity", _buttonOpacity);
            PlayerPrefs.SetInt(PrefsPrefix + "ShowCompass", _showCompass ? 1 : 0);
            PlayerPrefs.SetInt(PrefsPrefix + "HapticsEnabled", _hapticsEnabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Resets all settings to their default values and saves.
        /// </summary>
        public void ResetToDefaults()
        {
            _portraitFOV = 55f;
            _renderScale = 1.0f;
            _lookSensitivity = 1.0f;
            _moveSensitivity = 1.0f;
            _invertYAxis = false;
            _lookZoneRatio = 0.6f;
            _joystickMaxRadius = 80f;
            _tapMaxDuration = 0.2f;
            _tapMaxDistance = 10f;
            _swipeMinDistance = 40f;
            _swipeMaxDuration = 0.3f;
            _longPressMinDuration = 0.5f;
            _doubleTapWindow = 0.4f;
            _autoAimEnabled = true;
            _autoAimStrength = 0.1f;
            _lockOnBreakDistance = 15f;
            _showActionButtons = true;
            _buttonOpacity = 0.7f;
            _showCompass = true;
            _hapticsEnabled = true;
            Save();

            Debug.Log($"{LogPrefix} Settings reset to defaults.");
        }

        private void LoadFromPlayerPrefs()
        {
            _portraitFOV = PlayerPrefs.GetFloat(PrefsPrefix + "PortraitFOV", _portraitFOV);
            _renderScale = PlayerPrefs.GetFloat(PrefsPrefix + "RenderScale", _renderScale);
            _lookSensitivity = PlayerPrefs.GetFloat(PrefsPrefix + "LookSensitivity", _lookSensitivity);
            _moveSensitivity = PlayerPrefs.GetFloat(PrefsPrefix + "MoveSensitivity", _moveSensitivity);
            _invertYAxis = PlayerPrefs.GetInt(PrefsPrefix + "InvertYAxis", _invertYAxis ? 1 : 0) == 1;
            _lookZoneRatio = PlayerPrefs.GetFloat(PrefsPrefix + "LookZoneRatio", _lookZoneRatio);
            _joystickMaxRadius = PlayerPrefs.GetFloat(PrefsPrefix + "JoystickMaxRadius", _joystickMaxRadius);
            _tapMaxDuration = PlayerPrefs.GetFloat(PrefsPrefix + "TapMaxDuration", _tapMaxDuration);
            _tapMaxDistance = PlayerPrefs.GetFloat(PrefsPrefix + "TapMaxDistance", _tapMaxDistance);
            _swipeMinDistance = PlayerPrefs.GetFloat(PrefsPrefix + "SwipeMinDistance", _swipeMinDistance);
            _swipeMaxDuration = PlayerPrefs.GetFloat(PrefsPrefix + "SwipeMaxDuration", _swipeMaxDuration);
            _longPressMinDuration = PlayerPrefs.GetFloat(PrefsPrefix + "LongPressMinDuration", _longPressMinDuration);
            _doubleTapWindow = PlayerPrefs.GetFloat(PrefsPrefix + "DoubleTapWindow", _doubleTapWindow);
            _autoAimEnabled = PlayerPrefs.GetInt(PrefsPrefix + "AutoAimEnabled", _autoAimEnabled ? 1 : 0) == 1;
            _autoAimStrength = PlayerPrefs.GetFloat(PrefsPrefix + "AutoAimStrength", _autoAimStrength);
            _lockOnBreakDistance = PlayerPrefs.GetFloat(PrefsPrefix + "LockOnBreakDistance", _lockOnBreakDistance);
            _showActionButtons = PlayerPrefs.GetInt(PrefsPrefix + "ShowActionButtons", _showActionButtons ? 1 : 0) == 1;
            _buttonOpacity = PlayerPrefs.GetFloat(PrefsPrefix + "ButtonOpacity", _buttonOpacity);
            _showCompass = PlayerPrefs.GetInt(PrefsPrefix + "ShowCompass", _showCompass ? 1 : 0) == 1;
            _hapticsEnabled = PlayerPrefs.GetInt(PrefsPrefix + "HapticsEnabled", _hapticsEnabled ? 1 : 0) == 1;
        }
    }
}
