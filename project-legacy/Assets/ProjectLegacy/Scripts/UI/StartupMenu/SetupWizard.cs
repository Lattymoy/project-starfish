using UnityEngine;
using ProjectLegacy.Core;

namespace ProjectLegacy.UI.StartupMenu
{
    /// <summary>
    /// First-run setup experience that guides new users through data file
    /// location, graphics quality selection, control configuration, and
    /// preference settings. Presented as step-by-step vertical cards with
    /// a progress indicator.
    /// </summary>
    public class SetupWizard : MonoBehaviour
    {
        private const string LogPrefix = "[ProjectLegacy]";
        private const string CompletedPrefKey = "ProjectLegacy_SetupComplete";

        [Header("Wizard Steps")]
        [SerializeField]
        [Tooltip("Total number of setup steps.")]
        private int _totalSteps = 4;

        [Header("Appearance")]
        [SerializeField]
        [Tooltip("Background color for wizard cards.")]
        private Color _cardColor = new Color(0.12f, 0.12f, 0.16f, 0.95f);

        [SerializeField]
        [Tooltip("Accent color for progress indicator and buttons.")]
        private Color _accentColor = new Color(0.85f, 0.75f, 0.5f, 1f);

        [SerializeField]
        [Tooltip("Card width as fraction of screen width.")]
        private float _cardWidthRatio = 0.85f;

        [SerializeField]
        [Range(48f, 72f)]
        [Tooltip("Height of action buttons in pixels.")]
        private float _buttonHeight = 56f;

        /// <summary>Whether the setup wizard has been completed previously.</summary>
        public bool IsCompleted => PlayerPrefs.GetInt(CompletedPrefKey, 0) == 1;

        /// <summary>Current step index (0-based).</summary>
        public int CurrentStep { get; private set; }

        /// <summary>Whether the wizard is currently visible.</summary>
        public bool IsActive { get; private set; }

        private Texture2D _cardTexture;
        private float _fadeAlpha;

        private readonly string[] _stepTitles =
        {
            "Locate Game Data",
            "Graphics Quality",
            "Controls Setup",
            "Preferences"
        };

        private readonly string[] _stepDescriptions =
        {
            "Daggerfall Unity needs the original game data to run.\n\nPlease locate the 'arena2' folder from your DOS Daggerfall installation. You can get a free copy from Steam or Bethesda.net.",
            "Choose a graphics quality preset based on your device.\n\nYou can change this later in Settings.",
            "Let's set up your touch controls.\n\nThe screen is split into a Look Zone (top) and Move Zone (bottom). Try dragging in each zone to see how it feels.",
            "Final preferences before you begin.\n\nThese can all be changed later in Settings."
        };

        private void Start()
        {
            _cardTexture = new Texture2D(1, 1);
            _cardTexture.SetPixel(0, 0, Color.white);
            _cardTexture.Apply();
        }

        private void OnDestroy()
        {
            if (_cardTexture != null) Destroy(_cardTexture);
        }

        /// <summary>
        /// Shows the setup wizard starting from the first step.
        /// </summary>
        public void Show()
        {
            CurrentStep = 0;
            IsActive = true;
            _fadeAlpha = 0f;
            Debug.Log($"{LogPrefix} Setup wizard started");
        }

        /// <summary>
        /// Hides the setup wizard.
        /// </summary>
        public void Hide()
        {
            IsActive = false;
        }

        /// <summary>
        /// Advances to the next step, or completes the wizard if on the last step.
        /// </summary>
        public void NextStep()
        {
            CurrentStep++;
            if (CurrentStep >= _totalSteps)
            {
                Complete();
            }
            else
            {
                Debug.Log($"{LogPrefix} Setup wizard: step {CurrentStep + 1}/{_totalSteps}");
            }
        }

        /// <summary>
        /// Goes back to the previous step.
        /// </summary>
        public void PreviousStep()
        {
            if (CurrentStep > 0)
            {
                CurrentStep--;
            }
        }

        /// <summary>
        /// Marks the wizard as completed and hides it.
        /// </summary>
        public void Complete()
        {
            PlayerPrefs.SetInt(CompletedPrefKey, 1);
            PlayerPrefs.Save();
            Hide();
            Debug.Log($"{LogPrefix} Setup wizard completed");
        }

        /// <summary>
        /// Resets the wizard completion state so it shows again on next launch.
        /// </summary>
        public void Reset()
        {
            PlayerPrefs.DeleteKey(CompletedPrefKey);
            PlayerPrefs.Save();
        }

        private void Update()
        {
            if (!IsActive)
                return;

            if (_fadeAlpha < 1f)
            {
                _fadeAlpha = Mathf.MoveTowards(_fadeAlpha, 1f, Time.deltaTime * 3f);
            }
        }

        private void OnGUI()
        {
            if (!IsActive || _cardTexture == null)
                return;

            float cardWidth = Screen.width * _cardWidthRatio;
            float cardX = (Screen.width - cardWidth) / 2f;
            float cardY = Screen.height * 0.1f;
            float cardHeight = Screen.height * 0.75f;

            // Card background
            GUI.color = new Color(_cardColor.r, _cardColor.g, _cardColor.b, _fadeAlpha * _cardColor.a);
            GUI.DrawTexture(new Rect(cardX, cardY, cardWidth, cardHeight), _cardTexture);

            // Progress indicator
            DrawProgressIndicator(cardX, cardY + 16f, cardWidth);

            // Step title
            float contentY = cardY + 60f;
            GUI.color = new Color(_accentColor.r, _accentColor.g, _accentColor.b, _fadeAlpha);
            GUI.Label(
                new Rect(cardX + 20f, contentY, cardWidth - 40f, 30f),
                $"Step {CurrentStep + 1}: {_stepTitles[CurrentStep]}",
                new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = _accentColor }
                }
            );
            contentY += 40f;

            // Step description
            GUI.color = new Color(1f, 1f, 1f, _fadeAlpha * 0.8f);
            GUI.Label(
                new Rect(cardX + 20f, contentY, cardWidth - 40f, 120f),
                _stepDescriptions[CurrentStep],
                new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    wordWrap = true,
                    alignment = TextAnchor.UpperLeft,
                    normal = { textColor = new Color(0.8f, 0.8f, 0.8f, 1f) }
                }
            );

            // Navigation buttons at bottom
            float navY = cardY + cardHeight - _buttonHeight - 20f;

            // Back button (if not first step)
            if (CurrentStep > 0)
            {
                if (DrawNavButton(cardX + 20f, navY, cardWidth * 0.35f, "Back", false))
                {
                    PreviousStep();
                }
            }

            // Next / Done button
            string nextLabel = CurrentStep >= _totalSteps - 1 ? "Done" : "Next";
            float nextWidth = cardWidth * 0.35f;
            float nextX = cardX + cardWidth - nextWidth - 20f;

            if (DrawNavButton(nextX, navY, nextWidth, nextLabel, true))
            {
                NextStep();
            }

            GUI.color = Color.white;
        }

        private void DrawProgressIndicator(float x, float y, float width)
        {
            float dotSize = 10f;
            float dotGap = 16f;
            float totalWidth = _totalSteps * dotSize + (_totalSteps - 1) * dotGap;
            float startX = x + (width - totalWidth) / 2f;

            for (int i = 0; i < _totalSteps; i++)
            {
                float dotX = startX + i * (dotSize + dotGap);
                bool isCurrent = i == CurrentStep;
                bool isCompleted = i < CurrentStep;

                GUI.color = new Color(
                    _accentColor.r, _accentColor.g, _accentColor.b,
                    (isCurrent || isCompleted) ? _fadeAlpha : _fadeAlpha * 0.3f
                );

                float size = isCurrent ? dotSize + 2f : dotSize;
                float offset = isCurrent ? -1f : 0f;
                GUI.DrawTexture(new Rect(dotX + offset, y + offset, size, size), _cardTexture);
            }
        }

        private bool DrawNavButton(float x, float y, float width, string text, bool isPrimary)
        {
            Rect rect = new Rect(x, y, width, _buttonHeight);
            bool pressed = false;

            GUI.color = isPrimary
                ? new Color(_accentColor.r, _accentColor.g, _accentColor.b, _fadeAlpha * 0.9f)
                : new Color(0.3f, 0.3f, 0.35f, _fadeAlpha * 0.9f);
            GUI.DrawTexture(rect, _cardTexture);

            GUI.color = new Color(1f, 1f, 1f, _fadeAlpha);
            GUI.Label(rect, text, new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = isPrimary ? Color.black : Color.white }
            });

            for (int t = 0; t < UnityEngine.Input.touchCount; t++)
            {
                Touch touch = UnityEngine.Input.GetTouch(t);
                Vector2 guiPos = new Vector2(touch.position.x, Screen.height - touch.position.y);

                if (rect.Contains(guiPos) && touch.phase == TouchPhase.Ended)
                {
                    pressed = true;
                    Util.HapticFeedback.LightTap();
                }
            }

            return pressed;
        }
    }
}
