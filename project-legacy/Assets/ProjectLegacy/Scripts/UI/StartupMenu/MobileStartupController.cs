using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Serialization;

namespace ProjectLegacy.UI.StartupMenu
{
    /// <summary>
    /// Replaces DFU's default startup screens with a mobile-optimized vertical menu.
    /// Features large touch targets, animated transitions, and a dark theme
    /// matching Daggerfall's aesthetic. Only active when portrait mode is enabled.
    /// </summary>
    public class MobileStartupController : MonoBehaviour
    {
        private const string LogPrefix = "[ProjectLegacy]";

        [Header("Menu Layout")]
        [SerializeField]
        [Tooltip("Vertical spacing between menu buttons in pixels.")]
        private float _buttonSpacing = 16f;

        [SerializeField]
        [Tooltip("Button width as a fraction of screen width.")]
        private float _buttonWidthRatio = 0.75f;

        [SerializeField]
        [Range(48f, 80f)]
        [Tooltip("Button height in pixels.")]
        private float _buttonHeight = 64f;

        [Header("Appearance")]
        [SerializeField]
        [Tooltip("Background color.")]
        private Color _backgroundColor = new Color(0.05f, 0.05f, 0.08f, 1f);

        [SerializeField]
        [Tooltip("Button background color.")]
        private Color _buttonColor = new Color(0.15f, 0.15f, 0.2f, 1f);

        [SerializeField]
        [Tooltip("Button text color.")]
        private Color _buttonTextColor = new Color(0.9f, 0.85f, 0.7f, 1f);

        [SerializeField]
        [Tooltip("Button hover/press color.")]
        private Color _buttonPressedColor = new Color(0.25f, 0.25f, 0.35f, 1f);

        [SerializeField]
        [Tooltip("Title text color.")]
        private Color _titleColor = new Color(0.85f, 0.75f, 0.5f, 1f);

        /// <summary>Whether the startup menu is currently visible.</summary>
        public bool IsActive { get; private set; }

        private Texture2D _bgTexture;
        private Texture2D _buttonTexture;
        private GUIStyle _titleStyle;
        private GUIStyle _subtitleStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _buttonPressedStyle;
        private bool _stylesInitialized;
        private bool _hasSaveData;
        private float _fadeAlpha;
        private int _pressedButton = -1;

        private void Start()
        {
            _bgTexture = new Texture2D(1, 1);
            _bgTexture.SetPixel(0, 0, Color.white);
            _bgTexture.Apply();

            _buttonTexture = new Texture2D(1, 1);
            _buttonTexture.SetPixel(0, 0, Color.white);
            _buttonTexture.Apply();

            CheckForSaveData();
        }

        private void OnDestroy()
        {
            if (_bgTexture != null) Destroy(_bgTexture);
            if (_buttonTexture != null) Destroy(_buttonTexture);
        }

        /// <summary>
        /// Shows the startup menu and disables DFU's default startup UI.
        /// </summary>
        public void Show()
        {
            IsActive = true;
            _fadeAlpha = 0f;
            CheckForSaveData();
            Debug.Log($"{LogPrefix} Mobile startup menu shown");
        }

        /// <summary>
        /// Hides the startup menu.
        /// </summary>
        public void Hide()
        {
            IsActive = false;
            Debug.Log($"{LogPrefix} Mobile startup menu hidden");
        }

        private void Update()
        {
            if (!IsActive)
                return;

            // Fade in animation
            if (_fadeAlpha < 1f)
            {
                _fadeAlpha = Mathf.MoveTowards(_fadeAlpha, 1f, Time.deltaTime * 3f);
            }
        }

        private void OnGUI()
        {
            if (!IsActive || _bgTexture == null)
                return;

            InitStyles();

            // Full-screen dark background
            GUI.color = new Color(_backgroundColor.r, _backgroundColor.g, _backgroundColor.b, _fadeAlpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _bgTexture);

            GUI.color = new Color(1f, 1f, 1f, _fadeAlpha);

            float centerX = Screen.width / 2f;
            float currentY = Screen.height * 0.12f;

            // Title
            GUI.color = new Color(_titleColor.r, _titleColor.g, _titleColor.b, _fadeAlpha);
            Rect titleRect = new Rect(0, currentY, Screen.width, 60);
            GUI.Label(titleRect, "DAGGERFALL", _titleStyle);
            currentY += 55f;

            // Subtitle
            GUI.color = new Color(_buttonTextColor.r, _buttonTextColor.g, _buttonTextColor.b, _fadeAlpha * 0.7f);
            Rect subtitleRect = new Rect(0, currentY, Screen.width, 30);
            GUI.Label(subtitleRect, "PROJECT LEGACY", _subtitleStyle);
            currentY += 60f;

            float buttonWidth = Screen.width * _buttonWidthRatio;
            float buttonX = centerX - buttonWidth / 2f;

            // Continue button (only if save exists)
            if (_hasSaveData)
            {
                if (DrawMenuButton(buttonX, currentY, buttonWidth, "Continue", 0))
                {
                    OnContinuePressed();
                }
                currentY += _buttonHeight + _buttonSpacing;
            }

            // New Game
            if (DrawMenuButton(buttonX, currentY, buttonWidth, "New Game", 1))
            {
                OnNewGamePressed();
            }
            currentY += _buttonHeight + _buttonSpacing;

            // Load Game
            if (DrawMenuButton(buttonX, currentY, buttonWidth, "Load Game", 2))
            {
                OnLoadGamePressed();
            }
            currentY += _buttonHeight + _buttonSpacing;

            // Settings
            if (DrawMenuButton(buttonX, currentY, buttonWidth, "Settings", 3))
            {
                OnSettingsPressed();
            }
            currentY += _buttonHeight + _buttonSpacing;

            // Credits
            if (DrawMenuButton(buttonX, currentY, buttonWidth, "Credits", 4))
            {
                OnCreditsPressed();
            }

            // Version info at bottom
            GUI.color = new Color(1f, 1f, 1f, _fadeAlpha * 0.3f);
            GUI.Label(
                new Rect(0, Screen.height - 30, Screen.width, 25),
                "Project Legacy — Early Development",
                new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 10,
                    normal = { textColor = new Color(1f, 1f, 1f, 0.3f) }
                }
            );

            GUI.color = Color.white;
        }

        private bool DrawMenuButton(float x, float y, float width, string text, int buttonId)
        {
            Rect rect = new Rect(x, y, width, _buttonHeight);
            bool pressed = false;
            bool isHovered = _pressedButton == buttonId;

            // Draw button background
            GUI.color = new Color(
                isHovered ? _buttonPressedColor.r : _buttonColor.r,
                isHovered ? _buttonPressedColor.g : _buttonColor.g,
                isHovered ? _buttonPressedColor.b : _buttonColor.b,
                _fadeAlpha * 0.9f
            );
            GUI.DrawTexture(rect, _buttonTexture);

            // Draw button text
            GUI.color = new Color(_buttonTextColor.r, _buttonTextColor.g, _buttonTextColor.b, _fadeAlpha);
            GUI.Label(rect, text, isHovered ? _buttonPressedStyle : _buttonStyle);

            // Check touch input
            _pressedButton = -1;
            for (int t = 0; t < UnityEngine.Input.touchCount; t++)
            {
                Touch touch = UnityEngine.Input.GetTouch(t);
                Vector2 guiPos = new Vector2(touch.position.x, Screen.height - touch.position.y);

                if (!rect.Contains(guiPos))
                    continue;

                if (touch.phase == TouchPhase.Began || touch.phase == TouchPhase.Stationary)
                {
                    _pressedButton = buttonId;
                }

                if (touch.phase == TouchPhase.Ended)
                {
                    pressed = true;
                    Util.HapticFeedback.MediumTap();
                }
            }

            GUI.color = Color.white;
            return pressed;
        }

        private void OnContinuePressed()
        {
            Debug.Log($"{LogPrefix} Continue pressed — loading most recent save");
            Hide();
            // REQUIRES DFU PATCH: SaveLoadManager may need a public method to
            // load the most recent save file.
            if (SaveLoadManager.Instance != null)
            {
                SaveLoadManager.Instance.LoadMostRecentSave();
            }
        }

        private void OnNewGamePressed()
        {
            Debug.Log($"{LogPrefix} New Game pressed");
            Hide();
            // Start DFU's character creation flow
            if (DaggerfallUI.Instance != null)
            {
                DaggerfallUI.Instance.StartNewGame();
            }
        }

        private void OnLoadGamePressed()
        {
            Debug.Log($"{LogPrefix} Load Game pressed — showing save carousel");
            // TODO: Show SaveSlotCarousel
        }

        private void OnSettingsPressed()
        {
            Debug.Log($"{LogPrefix} Settings pressed — showing settings panel");
            // TODO: Show SettingsPanel
        }

        private void OnCreditsPressed()
        {
            Debug.Log($"{LogPrefix} Credits pressed");
            // TODO: Show credits scroll
        }

        private void CheckForSaveData()
        {
            _hasSaveData = false;
            if (SaveLoadManager.Instance != null)
            {
                string[] saveKeys = SaveLoadManager.Instance.GetAllSaveKeys();
                _hasSaveData = saveKeys != null && saveKeys.Length > 0;
            }
        }

        private void InitStyles()
        {
            if (_stylesInitialized)
                return;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 36,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _titleColor }
            };

            _subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _buttonTextColor }
            };

            _buttonStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _buttonTextColor }
            };

            _buttonPressedStyle = new GUIStyle(_buttonStyle)
            {
                normal = { textColor = Color.white }
            };

            _stylesInitialized = true;
        }
    }
}
