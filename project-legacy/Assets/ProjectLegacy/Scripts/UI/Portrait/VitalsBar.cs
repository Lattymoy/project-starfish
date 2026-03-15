using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;

namespace ProjectLegacy.UI.Portrait
{
    /// <summary>
    /// Compact health/mana/stamina display showing three horizontal bars
    /// in a single row. Tapping shows numeric values. Bars flash when
    /// values drop below critical thresholds.
    /// </summary>
    public class VitalsBar : MonoBehaviour
    {
        private const string LogPrefix = "[ProjectLegacy]";

        [Header("Bar Colors")]
        [SerializeField]
        [Tooltip("Health bar color.")]
        private Color _healthColor = new Color(0.85f, 0.15f, 0.15f, 1f);

        [SerializeField]
        [Tooltip("Magicka bar color.")]
        private Color _magickaColor = new Color(0.2f, 0.4f, 0.9f, 1f);

        [SerializeField]
        [Tooltip("Stamina bar color.")]
        private Color _staminaColor = new Color(0.2f, 0.75f, 0.2f, 1f);

        [SerializeField]
        [Tooltip("Bar background color.")]
        private Color _backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.7f);

        [Header("Layout")]
        [SerializeField]
        [Tooltip("Height of each bar in pixels.")]
        private float _barHeight = 8f;

        [SerializeField]
        [Tooltip("Gap between bars in pixels.")]
        private float _barGap = 2f;

        [SerializeField]
        [Tooltip("Horizontal padding in pixels.")]
        private float _horizontalPadding = 10f;

        [Header("Critical Threshold")]
        [SerializeField]
        [Range(0.1f, 0.4f)]
        [Tooltip("Percentage below which bars flash to indicate danger.")]
        private float _criticalThreshold = 0.25f;

        [SerializeField]
        [Range(1f, 5f)]
        [Tooltip("Flash speed in pulses per second.")]
        private float _flashSpeed = 3f;

        /// <summary>Whether numeric values are currently displayed.</summary>
        public bool ShowingNumbers { get; private set; }

        // Cached player entity reference
        private PlayerEntity _playerEntity;
        private Texture2D _barTexture;
        private float _showNumbersTimer;

        // Smoothed values for animation
        private float _smoothedHealth;
        private float _smoothedMagicka;
        private float _smoothedStamina;

        private void Start()
        {
            _barTexture = new Texture2D(1, 1);
            _barTexture.SetPixel(0, 0, Color.white);
            _barTexture.Apply();
        }

        private void Update()
        {
            CachePlayerEntity();
            UpdateSmoothedValues();

            if (ShowingNumbers)
            {
                _showNumbersTimer -= Time.deltaTime;
                if (_showNumbersTimer <= 0f)
                {
                    ShowingNumbers = false;
                }
            }
        }

        private void OnDestroy()
        {
            if (_barTexture != null)
                Destroy(_barTexture);
        }

        /// <summary>
        /// Shows numeric vital values for a brief duration.
        /// Called when the user taps the vitals bar.
        /// </summary>
        public void ShowNumericValues()
        {
            ShowingNumbers = true;
            _showNumbersTimer = 3f;
        }

        private void OnGUI()
        {
            if (_barTexture == null)
                return;

            CachePlayerEntity();
            if (_playerEntity == null)
                return;

            var layout = Core.LegacyBootstrapper.Instance != null
                ? Core.LegacyBootstrapper.Instance.ScreenLayout
                : null;

            float barAreaY;
            float barAreaWidth;
            float barAreaX;

            if (layout != null)
            {
                barAreaX = layout.BottomBarRect.x + _horizontalPadding;
                barAreaY = Screen.height - layout.BottomBarRect.y - layout.BottomBarRect.height;
                barAreaWidth = layout.BottomBarRect.width * 0.5f - _horizontalPadding * 2f;
            }
            else
            {
                barAreaX = _horizontalPadding;
                barAreaY = Screen.height - _barHeight * 3 - _barGap * 2 - 20f;
                barAreaWidth = Screen.width * 0.5f - _horizontalPadding * 2f;
            }

            float healthRatio = _playerEntity.MaxHealth > 0
                ? _smoothedHealth / _playerEntity.MaxHealth : 0f;
            float magickaRatio = _playerEntity.MaxMagicka > 0
                ? _smoothedMagicka / _playerEntity.MaxMagicka : 0f;
            float staminaRatio = _playerEntity.MaxFatigue > 0
                ? _smoothedStamina / _playerEntity.MaxFatigue : 0f;

            DrawVitalBar(barAreaX, barAreaY, barAreaWidth, healthRatio, _healthColor,
                _playerEntity.CurrentHealth, _playerEntity.MaxHealth, "HP");

            DrawVitalBar(barAreaX, barAreaY + _barHeight + _barGap, barAreaWidth,
                magickaRatio, _magickaColor,
                _playerEntity.CurrentMagicka, _playerEntity.MaxMagicka, "MP");

            DrawVitalBar(barAreaX, barAreaY + (_barHeight + _barGap) * 2, barAreaWidth,
                staminaRatio, _staminaColor,
                (int)(_playerEntity.CurrentFatigue / 64), (int)(_playerEntity.MaxFatigue / 64), "ST");
        }

        private void DrawVitalBar(float x, float y, float width, float ratio, Color color,
            int current, int max, string label)
        {
            // Background
            GUI.color = _backgroundColor;
            GUI.DrawTexture(new Rect(x, y, width, _barHeight), _barTexture);

            // Foreground bar
            Color barColor = color;
            if (ratio < _criticalThreshold)
            {
                // Flash effect
                float flash = Mathf.PingPong(Time.time * _flashSpeed, 1f);
                barColor = Color.Lerp(color, Color.white, flash * 0.5f);
            }

            GUI.color = barColor;
            GUI.DrawTexture(new Rect(x, y, width * Mathf.Clamp01(ratio), _barHeight), _barTexture);

            // Numeric overlay
            if (ShowingNumbers)
            {
                GUI.color = Color.white;
                var style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = (int)(_barHeight + 2),
                    alignment = TextAnchor.MiddleLeft,
                    fontStyle = FontStyle.Bold
                };
                GUI.Label(new Rect(x + 2, y - 1, width, _barHeight + 2),
                    $"{label}: {current}/{max}", style);
            }

            GUI.color = Color.white;
        }

        private void CachePlayerEntity()
        {
            if (_playerEntity == null && GameManager.Instance != null)
            {
                _playerEntity = GameManager.Instance.PlayerEntity;
            }
        }

        private void UpdateSmoothedValues()
        {
            if (_playerEntity == null)
                return;

            float smoothSpeed = 8f * Time.deltaTime;
            _smoothedHealth = Mathf.Lerp(_smoothedHealth, _playerEntity.CurrentHealth, smoothSpeed);
            _smoothedMagicka = Mathf.Lerp(_smoothedMagicka, _playerEntity.CurrentMagicka, smoothSpeed);
            _smoothedStamina = Mathf.Lerp(_smoothedStamina, _playerEntity.CurrentFatigue, smoothSpeed);
        }
    }
}
