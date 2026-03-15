using System;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.Questing;
using ProjectLegacy.Core;

namespace ProjectLegacy.UI.Panels
{
    /// <summary>
    /// Bottom-sheet-style dialogue panel for mobile devices. Displays NPC portrait and name
    /// at the top of the sheet, scrollable NPC text in the middle, and large tappable response
    /// buttons at the bottom. The sheet slides up from the bottom of the screen with a smooth
    /// animation and can be dismissed by swiping down.
    /// </summary>
    public class MobileDialoguePanel : MonoBehaviour
    {
        private const string LogPrefix = "[ProjectLegacy]";
        private const float SheetAnimSpeed = 6f;
        private const float SwipeDismissThreshold = 80f;
        private const float ResponseButtonHeight = 52f;
        private const float ResponseButtonGap = 6f;

        #region Serialized Fields

        [Header("Layout")]
        [SerializeField]
        [Tooltip("Maximum height of the bottom sheet as a fraction of screen height.")]
        [Range(0.4f, 0.85f)]
        private float _sheetHeightRatio = 0.65f;

        [SerializeField]
        [Tooltip("Height of the NPC header area (portrait + name) in dp.")]
        private float _headerHeightDp = 72f;

        [SerializeField]
        [Tooltip("Horizontal padding in dp.")]
        private float _paddingDp = 16f;

        [SerializeField]
        [Tooltip("Portrait size in dp.")]
        private float _portraitSizeDp = 52f;

        [Header("Colors")]
        [SerializeField]
        [Tooltip("Scrim (dimming overlay) color behind the sheet.")]
        private Color _scrimColor = new Color(0f, 0f, 0f, 0.5f);

        [SerializeField]
        [Tooltip("Sheet background color.")]
        private Color _sheetColor = new Color(0.1f, 0.1f, 0.13f, 0.97f);

        [SerializeField]
        [Tooltip("NPC name text color.")]
        private Color _nameColor = new Color(0.85f, 0.72f, 0.35f, 1f);

        [SerializeField]
        [Tooltip("NPC dialogue text color.")]
        private Color _dialogueTextColor = new Color(0.88f, 0.88f, 0.85f, 1f);

        [SerializeField]
        [Tooltip("Response button background color.")]
        private Color _responseBtnColor = new Color(0.18f, 0.18f, 0.22f, 0.95f);

        [SerializeField]
        [Tooltip("Response button pressed color.")]
        private Color _responseBtnPressedColor = new Color(0.28f, 0.26f, 0.2f, 0.95f);

        [SerializeField]
        [Tooltip("Response button text color.")]
        private Color _responseBtnTextColor = Color.white;

        [SerializeField]
        [Tooltip("Portrait frame border color.")]
        private Color _portraitBorderColor = new Color(0.5f, 0.42f, 0.25f, 0.8f);

        #endregion

        #region Public Properties

        /// <summary>Whether the dialogue panel is currently visible.</summary>
        public bool IsVisible { get; private set; }

        /// <summary>The name of the NPC currently being spoken to.</summary>
        public string NpcName { get; private set; }

        /// <summary>Fired when the player selects a response. Parameter: response index.</summary>
        public event Action<int> OnResponseSelected;

        /// <summary>Fired when the dialogue is dismissed.</summary>
        public event Action OnDialogueDismissed;

        #endregion

        #region Private State

        // Content
        private string _dialogueText = "";
        private List<string> _responseOptions = new List<string>();
        private Texture2D _npcPortrait;

        // Animation
        private float _sheetT; // 0 = hidden, 1 = fully shown
        private bool _sheetTargetVisible;

        // Scroll
        private Vector2 _textScrollPosition;

        // Swipe dismiss tracking
        private int _swipeTouchId = -1;
        private Vector2 _swipeStartPos;
        private float _swipeStartTime;

        // Response button press tracking
        private int _pressedResponseIndex = -1;

        // Textures and styles
        private Texture2D _pixelTex;
        private GUIStyle _nameStyle;
        private GUIStyle _dialogueStyle;
        private GUIStyle _responseStyle;
        private GUIStyle _handleStyle;
        private bool _stylesInitialized;

        // Layout
        private Rect _safeArea;
        private float _headerHeight;
        private float _padding;
        private float _portraitSize;
        private int _lastScreenWidth;
        private int _lastScreenHeight;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            _pixelTex = new Texture2D(1, 1);
            _pixelTex.SetPixel(0, 0, Color.white);
            _pixelTex.Apply();
        }

        private void Update()
        {
            if (!IsVisible && _sheetT <= 0.01f)
                return;

            AnimateSheet();
            HandleSwipeDismiss();
        }

        private void OnDestroy()
        {
            if (_pixelTex != null)
                Destroy(_pixelTex);
        }

        private void OnGUI()
        {
            if (_sheetT <= 0.01f || _pixelTex == null)
                return;

            EnsureStyles();
            RecalculateLayoutIfNeeded();

            DrawScrim();
            DrawSheet();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Shows the dialogue panel with the specified NPC and dialogue content.
        /// </summary>
        /// <param name="npcName">The NPC's display name.</param>
        /// <param name="dialogueText">The NPC's dialogue text to display.</param>
        /// <param name="responses">Available response options for the player. Can be null for monologue.</param>
        /// <param name="portrait">The NPC's portrait texture, or null for no portrait.</param>
        public void Show(string npcName, string dialogueText, List<string> responses, Texture2D portrait = null)
        {
            if (string.IsNullOrEmpty(npcName))
                npcName = "Unknown";

            NpcName = npcName;
            _dialogueText = dialogueText ?? "";
            _npcPortrait = portrait;
            _textScrollPosition = Vector2.zero;
            _pressedResponseIndex = -1;

            _responseOptions.Clear();
            if (responses != null)
            {
                _responseOptions.AddRange(responses);
            }

            IsVisible = true;
            _sheetTargetVisible = true;

            Debug.Log($"{LogPrefix} MobileDialoguePanel shown. NPC: {npcName}, Responses: {_responseOptions.Count}");
        }

        /// <summary>
        /// Shows the dialogue panel using DFU's talk manager data for the current conversation.
        /// </summary>
        public void ShowFromTalkManager()
        {
            if (GameManager.Instance == null)
            {
                Debug.LogWarning($"{LogPrefix} Cannot show dialogue — GameManager not available.");
                return;
            }

            TalkManager talkManager = GameManager.Instance.TalkManager;
            if (talkManager == null)
            {
                Debug.LogWarning($"{LogPrefix} Cannot show dialogue — TalkManager not available.");
                return;
            }

            string npcName = talkManager.StaticNPC != null
                ? talkManager.StaticNPC.DisplayName
                : "Citizen";

            // Attempt to get NPC portrait from DFU's billboard system
            Texture2D portrait = null;
            if (DaggerfallUI.Instance != null)
            {
                // Portrait loading delegates to DFU's face rendering
                portrait = GetNpcPortraitTexture();
            }

            Show(npcName, "", null, portrait);
        }

        /// <summary>
        /// Updates the dialogue text without changing other state. Used when the NPC
        /// continues speaking within the same conversation.
        /// </summary>
        /// <param name="newText">The updated dialogue text.</param>
        public void UpdateDialogueText(string newText)
        {
            _dialogueText = newText ?? "";
            _textScrollPosition = Vector2.zero;
        }

        /// <summary>
        /// Updates the available response options.
        /// </summary>
        /// <param name="responses">New response options to display.</param>
        public void UpdateResponses(List<string> responses)
        {
            _responseOptions.Clear();
            if (responses != null)
            {
                _responseOptions.AddRange(responses);
            }
            _pressedResponseIndex = -1;
        }

        /// <summary>
        /// Hides the dialogue panel with a slide-down animation.
        /// </summary>
        public void Hide()
        {
            if (!IsVisible)
                return;

            _sheetTargetVisible = false;
            IsVisible = false;

            OnDialogueDismissed?.Invoke();
            Debug.Log($"{LogPrefix} MobileDialoguePanel dismissed.");
        }

        #endregion

        #region Drawing

        private void DrawScrim()
        {
            GUI.color = new Color(_scrimColor.r, _scrimColor.g, _scrimColor.b, _scrimColor.a * _sheetT);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _pixelTex);

            // Tap scrim to dismiss (above the sheet)
            float sheetHeight = Screen.height * _sheetHeightRatio;
            float sheetTop = Screen.height - sheetHeight * _sheetT;
            Rect scrimTapArea = new Rect(0, 0, Screen.width, sheetTop);

            if (Event.current.type == EventType.MouseDown && scrimTapArea.Contains(Event.current.mousePosition))
            {
                Hide();
                Event.current.Use();
            }

            GUI.color = Color.white;
        }

        private void DrawSheet()
        {
            float sheetHeight = Screen.height * _sheetHeightRatio;
            float sheetY = Screen.height - sheetHeight * _sheetT;
            Rect sheetRect = new Rect(0, sheetY, Screen.width, sheetHeight);

            // Sheet background with rounded top corners (simulated)
            GUI.color = _sheetColor;
            GUI.DrawTexture(sheetRect, _pixelTex);

            // Top edge highlight
            GUI.color = new Color(0.3f, 0.3f, 0.35f, 0.5f);
            GUI.DrawTexture(new Rect(sheetRect.x, sheetRect.y, sheetRect.width, 1f), _pixelTex);

            // Drag handle
            float handleWidth = 40f;
            GUI.color = new Color(0.4f, 0.4f, 0.4f, 0.6f);
            GUI.DrawTexture(
                new Rect(Screen.width * 0.5f - handleWidth * 0.5f, sheetY + 8f, handleWidth, 4f),
                _pixelTex
            );

            float contentX = _safeArea.x + _padding;
            float contentWidth = _safeArea.width - _padding * 2f;
            float yOffset = sheetY + 20f;

            // NPC header
            yOffset = DrawNpcHeader(contentX, yOffset, contentWidth);

            // Separator
            GUI.color = new Color(0.3f, 0.3f, 0.33f, 0.5f);
            GUI.DrawTexture(new Rect(contentX, yOffset, contentWidth, 1f), _pixelTex);
            yOffset += 8f;

            // Calculate space for responses at bottom
            float responsesHeight = _responseOptions.Count * (ResponseButtonHeight + ResponseButtonGap) + _padding;
            float textAreaBottom = sheetRect.yMax - responsesHeight;
            float textAreaHeight = textAreaBottom - yOffset;

            // Dialogue text (scrollable)
            DrawDialogueText(contentX, yOffset, contentWidth, textAreaHeight);

            // Response buttons
            DrawResponseButtons(contentX, textAreaBottom, contentWidth);

            GUI.color = Color.white;
        }

        private float DrawNpcHeader(float x, float yOffset, float contentWidth)
        {
            float portraitX = x;
            float textX = x + _portraitSize + 12f;
            float textWidth = contentWidth - _portraitSize - 12f;

            // Portrait frame
            if (_npcPortrait != null)
            {
                // Border
                GUI.color = _portraitBorderColor;
                GUI.DrawTexture(
                    new Rect(portraitX - 2f, yOffset - 2f, _portraitSize + 4f, _portraitSize + 4f),
                    _pixelTex
                );

                // Portrait
                GUI.color = Color.white;
                GUI.DrawTexture(
                    new Rect(portraitX, yOffset, _portraitSize, _portraitSize),
                    _npcPortrait
                );
            }
            else
            {
                // Placeholder portrait
                GUI.color = new Color(0.2f, 0.2f, 0.25f, 0.8f);
                GUI.DrawTexture(new Rect(portraitX, yOffset, _portraitSize, _portraitSize), _pixelTex);

                GUI.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                var placeholderStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 20,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.5f, 0.5f, 0.5f, 0.5f) }
                };
                GUI.Label(new Rect(portraitX, yOffset, _portraitSize, _portraitSize), "?", placeholderStyle);
            }

            // NPC name
            GUI.color = _nameColor;
            GUI.Label(
                new Rect(textX, yOffset + 4f, textWidth, 22f),
                NpcName ?? "Unknown",
                _nameStyle
            );

            // Subtitle/role (if available from DFU)
            GUI.color = new Color(0.6f, 0.6f, 0.6f, 1f);
            string subtitle = GetNpcSubtitle();
            if (!string.IsNullOrEmpty(subtitle))
            {
                var subtitleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10,
                    alignment = TextAnchor.UpperLeft,
                    normal = { textColor = new Color(0.6f, 0.6f, 0.6f, 1f) }
                };
                GUI.Label(new Rect(textX, yOffset + 26f, textWidth, 16f), subtitle, subtitleStyle);
            }

            GUI.color = Color.white;
            return yOffset + _headerHeight;
        }

        private void DrawDialogueText(float x, float yOffset, float width, float height)
        {
            if (string.IsNullOrEmpty(_dialogueText))
                return;

            Rect viewport = new Rect(x, yOffset, width, height);

            // Estimate text height for scrolling
            float textHeight = _dialogueStyle.CalcHeight(new GUIContent(_dialogueText), width - 8f);
            float scrollContentHeight = Mathf.Max(textHeight + 20f, height);
            Rect contentRect = new Rect(0, 0, width - 16f, scrollContentHeight);

            _textScrollPosition = GUI.BeginScrollView(viewport, _textScrollPosition, contentRect);

            GUI.color = _dialogueTextColor;
            GUI.Label(new Rect(4f, 4f, contentRect.width - 8f, scrollContentHeight), _dialogueText, _dialogueStyle);

            GUI.EndScrollView();
            GUI.color = Color.white;
        }

        private void DrawResponseButtons(float x, float yOffset, float contentWidth)
        {
            if (_responseOptions.Count == 0)
                return;

            for (int i = 0; i < _responseOptions.Count; i++)
            {
                float btnY = yOffset + i * (ResponseButtonHeight + ResponseButtonGap);
                Rect btnRect = new Rect(x, btnY, contentWidth, ResponseButtonHeight);

                bool isPressed = _pressedResponseIndex == i;

                // Button background
                GUI.color = isPressed ? _responseBtnPressedColor : _responseBtnColor;
                GUI.DrawTexture(btnRect, _pixelTex);

                // Left accent bar
                GUI.color = _nameColor;
                GUI.DrawTexture(new Rect(btnRect.x, btnRect.y, 3f, btnRect.height), _pixelTex);

                // Button text
                GUI.color = _responseBtnTextColor;
                Rect textRect = new Rect(btnRect.x + 14f, btnRect.y, btnRect.width - 20f, btnRect.height);
                GUI.Label(textRect, _responseOptions[i], _responseStyle);

                // Touch handling
                HandleResponseButtonInput(i, btnRect);
            }

            GUI.color = Color.white;
        }

        private void HandleResponseButtonInput(int index, Rect btnRect)
        {
            for (int t = 0; t < Input.touchCount; t++)
            {
                Touch touch = Input.GetTouch(t);
                Vector2 guiPos = new Vector2(touch.position.x, Screen.height - touch.position.y);

                if (!btnRect.Contains(guiPos))
                    continue;

                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        _pressedResponseIndex = index;
                        break;

                    case TouchPhase.Ended:
                        if (_pressedResponseIndex == index)
                        {
                            SelectResponse(index);
                        }
                        _pressedResponseIndex = -1;
                        break;

                    case TouchPhase.Canceled:
                        _pressedResponseIndex = -1;
                        break;
                }
            }

            // Fallback for mouse/editor
            if (Event.current.type == EventType.MouseDown && btnRect.Contains(Event.current.mousePosition))
            {
                SelectResponse(index);
                Event.current.Use();
            }
        }

        #endregion

        #region Interaction

        private void SelectResponse(int index)
        {
            if (index < 0 || index >= _responseOptions.Count)
                return;

            Debug.Log($"{LogPrefix} Dialogue response selected: [{index}] {_responseOptions[index]}");
            OnResponseSelected?.Invoke(index);
        }

        private void HandleSwipeDismiss()
        {
            for (int t = 0; t < Input.touchCount; t++)
            {
                Touch touch = Input.GetTouch(t);

                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        if (_swipeTouchId < 0)
                        {
                            _swipeTouchId = touch.fingerId;
                            _swipeStartPos = touch.position;
                            _swipeStartTime = Time.time;
                        }
                        break;

                    case TouchPhase.Ended:
                        if (touch.fingerId == _swipeTouchId)
                        {
                            float dy = _swipeStartPos.y - touch.position.y; // Positive = swipe down in screen coords
                            float dt = Time.time - _swipeStartTime;

                            if (dy > SwipeDismissThreshold && dt < 0.4f)
                            {
                                Hide();
                            }

                            _swipeTouchId = -1;
                        }
                        break;

                    case TouchPhase.Canceled:
                        if (touch.fingerId == _swipeTouchId)
                        {
                            _swipeTouchId = -1;
                        }
                        break;
                }
            }
        }

        #endregion

        #region Animation

        private void AnimateSheet()
        {
            float target = _sheetTargetVisible ? 1f : 0f;
            _sheetT = Mathf.MoveTowards(_sheetT, target, Time.deltaTime * SheetAnimSpeed);

            // Clean up when fully hidden
            if (!_sheetTargetVisible && _sheetT <= 0.01f)
            {
                _sheetT = 0f;
            }
        }

        #endregion

        #region Helpers

        private Texture2D GetNpcPortraitTexture()
        {
            // DFU renders NPC portraits through its face rendering system.
            // This is a hook point for integration — the actual texture
            // would be obtained from DaggerfallUI or the billboard system.
            if (GameManager.Instance == null || GameManager.Instance.TalkManager == null)
                return null;

            // Attempt to read from the talk manager's static NPC face
            TalkManager talkManager = GameManager.Instance.TalkManager;
            if (talkManager.StaticNPC != null)
            {
                // DFU face textures are loaded via ImageReader
                // Return null here — the caller should pass the portrait via Show()
                return null;
            }

            return null;
        }

        private string GetNpcSubtitle()
        {
            if (GameManager.Instance == null || GameManager.Instance.TalkManager == null)
                return null;

            TalkManager talkManager = GameManager.Instance.TalkManager;
            if (talkManager.StaticNPC != null)
            {
                // Return faction/guild info if available
                int factionId = talkManager.StaticNPC.Data.factionID;
                if (factionId > 0)
                {
                    FactionFile.FactionData factionData;
                    if (GameManager.Instance.PlayerEntity.FactionData.GetFactionData(factionId, out factionData))
                    {
                        return factionData.name;
                    }
                }
            }

            return null;
        }

        private void RecalculateLayoutIfNeeded()
        {
            if (_lastScreenWidth == Screen.width && _lastScreenHeight == Screen.height)
                return;

            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;

            ScreenLayoutResolver layout = LegacyBootstrapper.Instance != null
                ? LegacyBootstrapper.Instance.ScreenLayout
                : null;

            _safeArea = layout != null ? layout.SafeArea : Screen.safeArea;

            float dpScale = layout != null ? layout.DpToPixels(1f) : 1f;
            _headerHeight = _headerHeightDp * dpScale;
            _padding = _paddingDp * dpScale;
            _portraitSize = _portraitSizeDp * dpScale;
        }

        private void EnsureStyles()
        {
            if (_stylesInitialized)
                return;

            _nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = _nameColor }
            };

            _dialogueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                richText = true,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = _dialogueTextColor },
                padding = new RectOffset(0, 0, 0, 0)
            };

            _responseStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Normal,
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = _responseBtnTextColor }
            };

            _handleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 8,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.5f, 0.5f, 0.5f, 0.5f) }
            };

            _stylesInitialized = true;
        }

        #endregion
    }
}
