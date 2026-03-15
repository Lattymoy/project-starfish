using UnityEngine;
using ProjectLegacy.Core;

namespace ProjectLegacy.UI.StartupMenu
{
    /// <summary>
    /// Comprehensive settings UI organized into sections: Display, Controls,
    /// Combat, Audio, and Advanced. Scrollable single-column layout with
    /// clear labels and appropriate controls (sliders, toggles, dropdowns).
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        private const string LogPrefix = "[ProjectLegacy]";

        [Header("Appearance")]
        [SerializeField]
        [Tooltip("Background color.")]
        private Color _backgroundColor = new Color(0.08f, 0.08f, 0.1f, 0.98f);

        [SerializeField]
        [Tooltip("Section header color.")]
        private Color _sectionHeaderColor = new Color(0.85f, 0.75f, 0.5f, 1f);

        [SerializeField]
        [Tooltip("Text color for labels.")]
        private Color _labelColor = new Color(0.85f, 0.85f, 0.85f, 1f);

        [SerializeField]
        [Tooltip("Slider track color.")]
        private Color _sliderTrackColor = new Color(0.3f, 0.3f, 0.35f, 1f);

        [SerializeField]
        [Tooltip("Slider fill color.")]
        private Color _sliderFillColor = new Color(0.85f, 0.75f, 0.5f, 1f);

        /// <summary>Whether the settings panel is currently visible.</summary>
        public bool IsActive { get; private set; }

        private Vector2 _scrollPosition;
        private Texture2D _bgTexture;
        private LegacySettings _settings;
        private bool _isDirty;

        private void Start()
        {
            _bgTexture = new Texture2D(1, 1);
            _bgTexture.SetPixel(0, 0, Color.white);
            _bgTexture.Apply();
        }

        private void OnDestroy()
        {
            if (_bgTexture != null) Destroy(_bgTexture);
        }

        /// <summary>
        /// Shows the settings panel.
        /// </summary>
        public void Show()
        {
            IsActive = true;
            _scrollPosition = Vector2.zero;
            _settings = LegacyBootstrapper.Instance != null
                ? LegacyBootstrapper.Instance.Settings
                : null;
            Debug.Log($"{LogPrefix} Settings panel shown");
        }

        /// <summary>
        /// Hides the settings panel and saves any changes.
        /// </summary>
        public void Hide()
        {
            if (_isDirty && _settings != null)
            {
                _settings.Save();
                _isDirty = false;
            }
            IsActive = false;
            Debug.Log($"{LogPrefix} Settings panel hidden");
        }

        private void OnGUI()
        {
            if (!IsActive || _bgTexture == null)
                return;

            // Full-screen background
            GUI.color = _backgroundColor;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _bgTexture);

            // Header
            GUI.color = _sectionHeaderColor;
            GUI.Label(
                new Rect(16, 16, Screen.width - 80, 40),
                "Settings",
                new GUIStyle(GUI.skin.label)
                {
                    fontSize = 24,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = _sectionHeaderColor }
                }
            );

            // Close button
            if (GUI.Button(new Rect(Screen.width - 56, 16, 40, 40), "✕"))
            {
                Hide();
            }

            if (_settings == null)
            {
                GUI.color = _labelColor;
                GUI.Label(new Rect(16, 80, Screen.width - 32, 30), "Settings not available.");
                return;
            }

            // Scrollable content
            float contentHeight = 900f;
            Rect viewRect = new Rect(0, 0, Screen.width - 32, contentHeight);
            Rect scrollRect = new Rect(8, 70, Screen.width - 16, Screen.height - 80);

            _scrollPosition = GUI.BeginScrollView(scrollRect, _scrollPosition, viewRect);

            float y = 0f;
            float labelWidth = Screen.width * 0.45f;
            float controlWidth = Screen.width * 0.4f;
            float controlX = labelWidth + 20f;

            // --- Display Section ---
            y = DrawSectionHeader(y, "Display");
            y = DrawSlider(y, "Portrait FOV", labelWidth, controlX, controlWidth,
                _settings.PortraitFOV, 45f, 65f, "°",
                v => _settings.PortraitFOV = v);
            y = DrawSlider(y, "Render Scale", labelWidth, controlX, controlWidth,
                _settings.RenderScale, 0.5f, 1f, "",
                v => _settings.RenderScale = v);

            // --- Controls Section ---
            y = DrawSectionHeader(y, "Controls");
            y = DrawSlider(y, "Look Sensitivity", labelWidth, controlX, controlWidth,
                _settings.LookSensitivity, 0.1f, 3f, "",
                v => _settings.LookSensitivity = v);
            y = DrawSlider(y, "Move Sensitivity", labelWidth, controlX, controlWidth,
                _settings.MoveSensitivity, 0.1f, 3f, "",
                v => _settings.MoveSensitivity = v);
            y = DrawToggle(y, "Invert Y-Axis", labelWidth, controlX,
                _settings.InvertYAxis,
                v => _settings.InvertYAxis = v);
            y = DrawSlider(y, "Look Zone Size", labelWidth, controlX, controlWidth,
                _settings.LookZoneRatio, 0.5f, 0.8f, "",
                v => _settings.LookZoneRatio = v);
            y = DrawSlider(y, "Joystick Radius", labelWidth, controlX, controlWidth,
                _settings.JoystickMaxRadius, 40f, 120f, "px",
                v => _settings.JoystickMaxRadius = v);

            // --- Combat Section ---
            y = DrawSectionHeader(y, "Combat");
            y = DrawToggle(y, "Auto-Aim", labelWidth, controlX,
                _settings.AutoAimEnabled,
                v => _settings.AutoAimEnabled = v);
            y = DrawSlider(y, "Auto-Aim Strength", labelWidth, controlX, controlWidth,
                _settings.AutoAimStrength, 0f, 0.25f, "",
                v => _settings.AutoAimStrength = v);
            y = DrawSlider(y, "Lock-On Distance", labelWidth, controlX, controlWidth,
                _settings.LockOnBreakDistance, 10f, 25f, "m",
                v => _settings.LockOnBreakDistance = v);

            // --- HUD Section ---
            y = DrawSectionHeader(y, "HUD");
            y = DrawToggle(y, "Action Buttons", labelWidth, controlX,
                _settings.ShowActionButtons,
                v => _settings.ShowActionButtons = v);
            y = DrawSlider(y, "Button Opacity", labelWidth, controlX, controlWidth,
                _settings.ButtonOpacity, 0.3f, 1f, "",
                v => _settings.ButtonOpacity = v);
            y = DrawToggle(y, "Compass Strip", labelWidth, controlX,
                _settings.ShowCompass,
                v => _settings.ShowCompass = v);

            // --- Haptics Section ---
            y = DrawSectionHeader(y, "Haptics");
            y = DrawToggle(y, "Haptic Feedback", labelWidth, controlX,
                _settings.HapticsEnabled,
                v => _settings.HapticsEnabled = v);

            // --- Reset ---
            y += 20f;
            if (GUI.Button(new Rect(16, y, Screen.width - 48, 44), "Reset to Defaults"))
            {
                _settings.ResetToDefaults();
            }

            GUI.EndScrollView();
            GUI.color = Color.white;
        }

        private float DrawSectionHeader(float y, string title)
        {
            y += 16f;
            GUI.color = _sectionHeaderColor;
            GUI.Label(
                new Rect(8, y, Screen.width - 48, 28),
                title,
                new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = _sectionHeaderColor }
                }
            );
            y += 32f;

            // Divider line
            GUI.color = new Color(_sectionHeaderColor.r, _sectionHeaderColor.g, _sectionHeaderColor.b, 0.3f);
            GUI.DrawTexture(new Rect(8, y - 4, Screen.width - 48, 1), _bgTexture);

            return y;
        }

        private float DrawSlider(float y, string label, float labelWidth, float controlX,
            float controlWidth, float value, float min, float max, string suffix,
            System.Action<float> setter)
        {
            GUI.color = _labelColor;
            GUI.Label(new Rect(8, y, labelWidth, 24), label, new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = _labelColor }
            });

            float newValue = GUI.HorizontalSlider(
                new Rect(controlX, y + 4, controlWidth - 50, 16),
                value, min, max
            );

            GUI.Label(new Rect(controlX + controlWidth - 45, y, 45, 24),
                $"{newValue:F1}{suffix}", new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    alignment = TextAnchor.MiddleRight,
                    normal = { textColor = _labelColor }
                });

            if (!Mathf.Approximately(newValue, value))
            {
                setter(newValue);
                _isDirty = true;
            }

            return y + 36f;
        }

        private float DrawToggle(float y, string label, float labelWidth, float controlX,
            bool value, System.Action<bool> setter)
        {
            GUI.color = _labelColor;
            GUI.Label(new Rect(8, y, labelWidth, 24), label, new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = _labelColor }
            });

            bool newValue = GUI.Toggle(new Rect(controlX, y, 24, 24), value, "");

            if (newValue != value)
            {
                setter(newValue);
                _isDirty = true;
            }

            return y + 36f;
        }
    }
}
