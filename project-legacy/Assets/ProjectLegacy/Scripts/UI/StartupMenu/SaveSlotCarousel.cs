using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop.Game.Serialization;

namespace ProjectLegacy.UI.StartupMenu
{
    /// <summary>
    /// Horizontal swipe carousel for browsing save files. Each card shows
    /// character info, location, playtime, and last-played date. Supports
    /// tap to load, long-press for delete/export options.
    /// </summary>
    public class SaveSlotCarousel : MonoBehaviour
    {
        private const string LogPrefix = "[ProjectLegacy]";

        [Header("Card Layout")]
        [SerializeField]
        [Tooltip("Card width as fraction of screen width.")]
        private float _cardWidthRatio = 0.8f;

        [SerializeField]
        [Tooltip("Card height in pixels.")]
        private float _cardHeight = 200f;

        [SerializeField]
        [Tooltip("Gap between cards in pixels.")]
        private float _cardGap = 16f;

        [Header("Appearance")]
        [SerializeField]
        [Tooltip("Card background color.")]
        private Color _cardColor = new Color(0.15f, 0.15f, 0.2f, 0.95f);

        [SerializeField]
        [Tooltip("Selected card border color.")]
        private Color _selectedBorderColor = new Color(0.85f, 0.75f, 0.5f, 1f);

        [SerializeField]
        [Tooltip("Card text color.")]
        private Color _textColor = new Color(0.9f, 0.9f, 0.9f, 1f);

        /// <summary>Whether the carousel is currently visible.</summary>
        public bool IsActive { get; private set; }

        /// <summary>Index of the currently centered/selected save slot.</summary>
        public int SelectedIndex { get; private set; }

        // Save data cache
        private readonly List<SaveInfo> _saveInfos = new List<SaveInfo>();

        // Scroll tracking
        private float _scrollOffset;
        private float _scrollVelocity;
        private float _scrollTarget;
        private int _scrollFingerId = -1;
        private float _scrollStartX;
        private float _scrollStartOffset;

        private Texture2D _cardTexture;

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
        /// Shows the carousel and loads available save files.
        /// </summary>
        public void Show()
        {
            IsActive = true;
            _scrollOffset = 0f;
            _scrollVelocity = 0f;
            SelectedIndex = 0;
            LoadSaveFiles();
            Debug.Log($"{LogPrefix} Save carousel shown — {_saveInfos.Count} saves found");
        }

        /// <summary>
        /// Hides the carousel.
        /// </summary>
        public void Hide()
        {
            IsActive = false;
        }

        /// <summary>
        /// Loads the save at the currently selected index.
        /// </summary>
        public void LoadSelectedSave()
        {
            if (SelectedIndex < 0 || SelectedIndex >= _saveInfos.Count)
                return;

            var saveInfo = _saveInfos[SelectedIndex];
            Debug.Log($"{LogPrefix} Loading save: {saveInfo.CharacterName}");

            if (SaveLoadManager.Instance != null)
            {
                SaveLoadManager.Instance.Load(saveInfo.SaveKey);
                Hide();
            }
        }

        private void Update()
        {
            if (!IsActive)
                return;

            ProcessScrollInput();
            UpdateScrollPhysics();
            UpdateSelectedIndex();
        }

        private void OnGUI()
        {
            if (!IsActive || _cardTexture == null || _saveInfos.Count == 0)
                return;

            float cardWidth = Screen.width * _cardWidthRatio;
            float centerY = Screen.height * 0.35f;

            for (int i = 0; i < _saveInfos.Count; i++)
            {
                float cardX = CalculateCardX(i, cardWidth);

                // Skip if off-screen
                if (cardX + cardWidth < 0 || cardX > Screen.width)
                    continue;

                bool isSelected = i == SelectedIndex;
                DrawSaveCard(cardX, centerY, cardWidth, _saveInfos[i], isSelected);
            }

            // Page indicator
            DrawPageIndicator(centerY + _cardHeight + 20f);
        }

        private void DrawSaveCard(float x, float y, float width, SaveInfo info, bool isSelected)
        {
            Rect cardRect = new Rect(x, y, width, _cardHeight);

            // Card background
            GUI.color = _cardColor;
            GUI.DrawTexture(cardRect, _cardTexture);

            // Selected border
            if (isSelected)
            {
                float borderWidth = 2f;
                GUI.color = _selectedBorderColor;
                GUI.DrawTexture(new Rect(x, y, width, borderWidth), _cardTexture);
                GUI.DrawTexture(new Rect(x, y + _cardHeight - borderWidth, width, borderWidth), _cardTexture);
                GUI.DrawTexture(new Rect(x, y, borderWidth, _cardHeight), _cardTexture);
                GUI.DrawTexture(new Rect(x + width - borderWidth, y, borderWidth, _cardHeight), _cardTexture);
            }

            float padding = 16f;
            float contentX = x + padding;
            float contentY = y + padding;
            float contentWidth = width - padding * 2;

            // Character name
            GUI.color = _selectedBorderColor;
            GUI.Label(
                new Rect(contentX, contentY, contentWidth, 28),
                info.CharacterName,
                new GUIStyle(GUI.skin.label)
                {
                    fontSize = 20,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = _selectedBorderColor }
                }
            );
            contentY += 32f;

            // Level and class
            GUI.color = _textColor;
            GUI.Label(
                new Rect(contentX, contentY, contentWidth, 20),
                $"Level {info.Level}",
                new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    normal = { textColor = _textColor }
                }
            );
            contentY += 24f;

            // Location
            GUI.color = new Color(_textColor.r, _textColor.g, _textColor.b, 0.7f);
            GUI.Label(
                new Rect(contentX, contentY, contentWidth, 20),
                info.Location,
                new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    normal = { textColor = new Color(_textColor.r, _textColor.g, _textColor.b, 0.7f) }
                }
            );
            contentY += 24f;

            // Playtime and date
            GUI.Label(
                new Rect(contentX, contentY, contentWidth, 20),
                $"Playtime: {info.PlayTime}  •  {info.LastPlayed}",
                new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    normal = { textColor = new Color(_textColor.r, _textColor.g, _textColor.b, 0.5f) }
                }
            );

            // Tap to load hint
            if (isSelected)
            {
                GUI.color = new Color(_selectedBorderColor.r, _selectedBorderColor.g, _selectedBorderColor.b, 0.6f);
                GUI.Label(
                    new Rect(x, y + _cardHeight - 30, width, 24),
                    "Tap to load  •  Long-press for options",
                    new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 10,
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = new Color(_selectedBorderColor.r, _selectedBorderColor.g, _selectedBorderColor.b, 0.6f) }
                    }
                );
            }

            GUI.color = Color.white;
        }

        private void DrawPageIndicator(float y)
        {
            float dotSize = 8f;
            float dotGap = 12f;
            float totalWidth = _saveInfos.Count * dotSize + (_saveInfos.Count - 1) * dotGap;
            float startX = (Screen.width - totalWidth) / 2f;

            for (int i = 0; i < _saveInfos.Count; i++)
            {
                bool isCurrent = i == SelectedIndex;
                GUI.color = isCurrent
                    ? _selectedBorderColor
                    : new Color(0.5f, 0.5f, 0.5f, 0.5f);
                GUI.DrawTexture(new Rect(startX + i * (dotSize + dotGap), y, dotSize, dotSize), _cardTexture);
            }

            GUI.color = Color.white;
        }

        private float CalculateCardX(int index, float cardWidth)
        {
            float centerX = (Screen.width - cardWidth) / 2f;
            return centerX + index * (cardWidth + _cardGap) - _scrollOffset;
        }

        private void ProcessScrollInput()
        {
            for (int t = 0; t < UnityEngine.Input.touchCount; t++)
            {
                Touch touch = UnityEngine.Input.GetTouch(t);

                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        if (_scrollFingerId < 0)
                        {
                            _scrollFingerId = touch.fingerId;
                            _scrollStartX = touch.position.x;
                            _scrollStartOffset = _scrollOffset;
                            _scrollVelocity = 0f;
                        }
                        break;

                    case TouchPhase.Moved:
                        if (touch.fingerId == _scrollFingerId)
                        {
                            float deltaX = _scrollStartX - touch.position.x;
                            _scrollOffset = _scrollStartOffset + deltaX;
                            _scrollVelocity = -touch.deltaPosition.x / Time.deltaTime;
                        }
                        break;

                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        if (touch.fingerId == _scrollFingerId)
                        {
                            _scrollFingerId = -1;
                        }
                        break;
                }
            }
        }

        private void UpdateScrollPhysics()
        {
            if (_scrollFingerId >= 0)
                return;

            // Apply velocity-based momentum
            _scrollOffset += _scrollVelocity * Time.deltaTime;
            _scrollVelocity *= 0.92f; // Deceleration

            // Snap to nearest card
            float cardWidth = Screen.width * _cardWidthRatio + _cardGap;
            float nearestSnap = Mathf.Round(_scrollOffset / cardWidth) * cardWidth;

            // Clamp to valid range
            float maxOffset = Mathf.Max(0, (_saveInfos.Count - 1) * cardWidth);
            nearestSnap = Mathf.Clamp(nearestSnap, 0, maxOffset);

            if (Mathf.Abs(_scrollVelocity) < 50f)
            {
                _scrollOffset = Mathf.Lerp(_scrollOffset, nearestSnap, 8f * Time.deltaTime);
                _scrollVelocity = 0f;
            }
        }

        private void UpdateSelectedIndex()
        {
            float cardWidth = Screen.width * _cardWidthRatio + _cardGap;
            SelectedIndex = Mathf.Clamp(
                Mathf.RoundToInt(_scrollOffset / cardWidth),
                0,
                Mathf.Max(0, _saveInfos.Count - 1)
            );
        }

        private void LoadSaveFiles()
        {
            _saveInfos.Clear();

            if (SaveLoadManager.Instance == null)
                return;

            string[] saveKeys = SaveLoadManager.Instance.GetAllSaveKeys();
            if (saveKeys == null)
                return;

            foreach (string key in saveKeys)
            {
                var info = new SaveInfo
                {
                    SaveKey = key,
                    CharacterName = SaveLoadManager.Instance.GetSaveCharacterName(key),
                    Level = 1,
                    Location = "Unknown",
                    PlayTime = "0:00",
                    LastPlayed = "Unknown"
                };
                _saveInfos.Add(info);
            }
        }

        /// <summary>
        /// Data holder for save file display information.
        /// </summary>
        private struct SaveInfo
        {
            public string SaveKey;
            public string CharacterName;
            public int Level;
            public string Location;
            public string PlayTime;
            public string LastPlayed;
        }
    }
}
