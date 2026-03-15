using System;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.Entity;
using ProjectLegacy.Core;

namespace ProjectLegacy.UI.Panels
{
    /// <summary>
    /// Mobile spellbook panel displaying the player's known spells in a scrollable list.
    /// Tap a spell to select it and expand an inline detail card showing cost, effects,
    /// and description. Swipe right on a spell to assign it to a quick slot. Selecting a
    /// spell sets it as the player's active spell for casting.
    /// </summary>
    public class MobileSpellbookPanel : MonoBehaviour
    {
        private const string LogPrefix = "[ProjectLegacy]";
        private const float SwipeAssignThreshold = 60f;
        private const float CardAnimSpeed = 8f;
        private const int QuickSlotCount = 4;

        #region Serialized Fields

        [Header("Layout")]
        [SerializeField]
        [Tooltip("Height of each spell row in the list in dp.")]
        private float _rowHeightDp = 56f;

        [SerializeField]
        [Tooltip("Height of the expanded detail card in dp.")]
        private float _detailCardHeightDp = 140f;

        [SerializeField]
        [Tooltip("Title bar height in dp.")]
        private float _titleBarHeightDp = 48f;

        [SerializeField]
        [Tooltip("Horizontal padding in dp.")]
        private float _paddingDp = 14f;

        [SerializeField]
        [Tooltip("Gap between list rows in dp.")]
        private float _rowGapDp = 3f;

        [Header("Colors")]
        [SerializeField]
        [Tooltip("Background overlay color.")]
        private Color _overlayColor = new Color(0.04f, 0.04f, 0.07f, 0.94f);

        [SerializeField]
        [Tooltip("Spell row background color.")]
        private Color _rowColor = new Color(0.14f, 0.14f, 0.18f, 0.9f);

        [SerializeField]
        [Tooltip("Selected spell row highlight.")]
        private Color _selectedRowColor = new Color(0.22f, 0.2f, 0.14f, 0.95f);

        [SerializeField]
        [Tooltip("Detail card background color.")]
        private Color _cardColor = new Color(0.1f, 0.1f, 0.14f, 0.95f);

        [SerializeField]
        [Tooltip("Spell name text color.")]
        private Color _spellNameColor = Color.white;

        [SerializeField]
        [Tooltip("Magicka cost text color.")]
        private Color _costColor = new Color(0.3f, 0.55f, 0.9f, 1f);

        [SerializeField]
        [Tooltip("Effect description text color.")]
        private Color _effectTextColor = new Color(0.75f, 0.75f, 0.72f, 1f);

        [SerializeField]
        [Tooltip("Quick slot assignment indicator color.")]
        private Color _quickSlotIndicatorColor = new Color(0.4f, 0.7f, 0.3f, 0.7f);

        [SerializeField]
        [Tooltip("School of magic label color.")]
        private Color _schoolColor = new Color(0.6f, 0.55f, 0.4f, 1f);

        #endregion

        #region Public Properties

        /// <summary>Whether the spellbook panel is currently visible.</summary>
        public bool IsVisible { get; private set; }

        /// <summary>Index of the currently selected (expanded) spell, or -1 if none.</summary>
        public int SelectedSpellIndex { get; private set; } = -1;

        /// <summary>Fired when the panel is shown.</summary>
        public event Action OnPanelShown;

        /// <summary>Fired when the panel is hidden.</summary>
        public event Action OnPanelHidden;

        /// <summary>Fired when a spell is set as active. Parameter: spell settings.</summary>
        public event Action<EffectBundleSettings> OnSpellActivated;

        /// <summary>Fired when a spell is assigned to a quick slot. Parameters: spell index, slot index.</summary>
        public event Action<int, int> OnSpellAssignedToQuickSlot;

        #endregion

        #region Private State

        private PlayerEntity _playerEntity;
        private List<EffectBundleSettings> _spells = new List<EffectBundleSettings>();
        private Vector2 _scrollPosition;

        // Detail card animation
        private float _cardAnimT; // 0 = collapsed, 1 = expanded
        private int _animatingSpellIndex = -1;

        // Swipe tracking per row
        private int _swipeTouchId = -1;
        private Vector2 _swipeStartPos;
        private float _swipeStartTime;
        private int _swipeRowIndex = -1;
        private float _swipeOffsetX; // Current horizontal offset for visual feedback

        // Textures and styles
        private Texture2D _pixelTex;
        private GUIStyle _titleStyle;
        private GUIStyle _spellNameStyle;
        private GUIStyle _costStyle;
        private GUIStyle _effectStyle;
        private GUIStyle _schoolStyle;
        private GUIStyle _closeBtnStyle;
        private GUIStyle _emptyStyle;
        private bool _stylesInitialized;

        // Layout cache
        private Rect _safeArea;
        private float _rowHeight;
        private float _detailCardHeight;
        private float _titleBarHeight;
        private float _padding;
        private float _rowGap;
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
            if (!IsVisible)
                return;

            CachePlayerEntity();
            AnimateDetailCard();
            HandleSwipeInput();
        }

        private void OnDestroy()
        {
            if (_pixelTex != null)
                Destroy(_pixelTex);
        }

        private void OnGUI()
        {
            if (!IsVisible || _pixelTex == null)
                return;

            EnsureStyles();
            RecalculateLayoutIfNeeded();

            DrawBackground();
            DrawTitleBar();
            DrawSpellList();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Shows the spellbook panel, loading spells from the player entity.
        /// </summary>
        public void Show()
        {
            if (IsVisible)
                return;

            CachePlayerEntity();
            if (_playerEntity == null)
            {
                Debug.LogWarning($"{LogPrefix} Cannot show spellbook — player entity not available.");
                return;
            }

            IsVisible = true;
            SelectedSpellIndex = -1;
            _cardAnimT = 0f;
            _animatingSpellIndex = -1;
            _scrollPosition = Vector2.zero;
            _swipeOffsetX = 0f;

            RefreshSpellList();
            SuppressGameInput(true);

            OnPanelShown?.Invoke();
            Debug.Log($"{LogPrefix} MobileSpellbookPanel shown. Spells: {_spells.Count}");
        }

        /// <summary>
        /// Hides the spellbook panel and restores game input.
        /// </summary>
        public void Hide()
        {
            if (!IsVisible)
                return;

            IsVisible = false;
            SelectedSpellIndex = -1;

            SuppressGameInput(false);

            OnPanelHidden?.Invoke();
            Debug.Log($"{LogPrefix} MobileSpellbookPanel hidden.");
        }

        /// <summary>
        /// Selects a spell by index, expanding its detail card. Selecting the same spell
        /// collapses it. Also sets the spell as the player's active readied spell.
        /// </summary>
        /// <param name="index">Index into the spell list.</param>
        public void SelectSpell(int index)
        {
            if (index < 0 || index >= _spells.Count)
                return;

            if (SelectedSpellIndex == index)
            {
                // Toggle off
                SelectedSpellIndex = -1;
                _animatingSpellIndex = index;
                Debug.Log($"{LogPrefix} Spell deselected.");
                return;
            }

            SelectedSpellIndex = index;
            _animatingSpellIndex = index;
            _cardAnimT = 0f;

            // Set as active spell in DFU
            SetActiveSpell(_spells[index]);

            Debug.Log($"{LogPrefix} Spell selected: {_spells[index].Name}");
        }

        /// <summary>
        /// Assigns the spell at the given index to a quick slot.
        /// </summary>
        /// <param name="spellIndex">Index into the spell list.</param>
        /// <param name="slotIndex">Quick slot index (0-based).</param>
        public void AssignToQuickSlot(int spellIndex, int slotIndex)
        {
            if (spellIndex < 0 || spellIndex >= _spells.Count)
                return;
            if (slotIndex < 0 || slotIndex >= QuickSlotCount)
                return;

            OnSpellAssignedToQuickSlot?.Invoke(spellIndex, slotIndex);
            Debug.Log($"{LogPrefix} Spell '{_spells[spellIndex].Name}' assigned to quick slot {slotIndex}");
        }

        #endregion

        #region Drawing

        private void DrawBackground()
        {
            GUI.color = _overlayColor;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _pixelTex);
            GUI.color = Color.white;
        }

        private void DrawTitleBar()
        {
            Rect titleRect = new Rect(_safeArea.x, _safeArea.y, _safeArea.width, _titleBarHeight);

            GUI.color = new Color(0.08f, 0.08f, 0.1f, 0.98f);
            GUI.DrawTexture(titleRect, _pixelTex);

            GUI.color = Color.white;
            GUI.Label(
                new Rect(titleRect.x + _padding, titleRect.y, titleRect.width - 60f, titleRect.height),
                $"Spellbook ({_spells.Count})",
                _titleStyle
            );

            // Close button
            Rect closeRect = new Rect(titleRect.xMax - 50f, titleRect.y, 50f, titleRect.height);
            GUI.Label(closeRect, "X", _closeBtnStyle);
            if (Event.current.type == EventType.MouseDown && closeRect.Contains(Event.current.mousePosition))
            {
                Hide();
                Event.current.Use();
            }
        }

        private void DrawSpellList()
        {
            float listTop = _safeArea.y + _titleBarHeight;
            float listHeight = _safeArea.height - _titleBarHeight;
            Rect viewport = new Rect(_safeArea.x, listTop, _safeArea.width, listHeight);

            float totalHeight = CalculateTotalListHeight();
            Rect contentRect = new Rect(0, 0, viewport.width - 16f, totalHeight);

            _scrollPosition = GUI.BeginScrollView(viewport, _scrollPosition, contentRect);

            if (_spells.Count == 0)
            {
                GUI.color = new Color(0.5f, 0.5f, 0.5f, 1f);
                GUI.Label(
                    new Rect(_padding, 40f, contentRect.width - _padding * 2f, 30f),
                    "No spells known. Visit a Mages Guild to purchase spells.",
                    _emptyStyle
                );
                GUI.color = Color.white;
            }

            float yOffset = 0f;

            for (int i = 0; i < _spells.Count; i++)
            {
                float xOffset = 0f;

                // Apply swipe offset for the row being swiped
                if (_swipeRowIndex == i)
                {
                    xOffset = _swipeOffsetX;
                }

                yOffset = DrawSpellRow(i, xOffset, yOffset, contentRect.width);

                // Draw expanded detail card if this spell is selected
                if (SelectedSpellIndex == i && _cardAnimT > 0.01f)
                {
                    yOffset = DrawDetailCard(i, yOffset, contentRect.width);
                }
            }

            GUI.EndScrollView();
        }

        private float DrawSpellRow(int index, float xOffset, float yOffset, float contentWidth)
        {
            EffectBundleSettings spell = _spells[index];
            bool isSelected = SelectedSpellIndex == index;

            Rect rowRect = new Rect(_padding + xOffset, yOffset, contentWidth - _padding * 2f, _rowHeight);

            // Row background
            GUI.color = isSelected ? _selectedRowColor : _rowColor;
            GUI.DrawTexture(rowRect, _pixelTex);

            // Selection indicator
            if (isSelected)
            {
                GUI.color = new Color(0.85f, 0.72f, 0.35f, 0.8f);
                GUI.DrawTexture(new Rect(rowRect.x, rowRect.y, 3f, rowRect.height), _pixelTex);
            }

            float textX = rowRect.x + 12f;
            float textWidth = rowRect.width - 24f;

            // Spell name
            GUI.color = _spellNameColor;
            GUI.Label(
                new Rect(textX, rowRect.y + 4f, textWidth * 0.65f, _rowHeight * 0.5f),
                spell.Name ?? "Unknown Spell",
                _spellNameStyle
            );

            // Magicka cost (right-aligned)
            int cost = CalculateSpellCost(spell);
            GUI.color = _costColor;
            GUI.Label(
                new Rect(textX + textWidth * 0.65f, rowRect.y + 4f, textWidth * 0.35f, _rowHeight * 0.5f),
                $"{cost} MP",
                _costStyle
            );

            // School of magic
            string school = GetSpellSchool(spell);
            GUI.color = _schoolColor;
            GUI.Label(
                new Rect(textX, rowRect.y + _rowHeight * 0.5f, textWidth, _rowHeight * 0.4f),
                school,
                _schoolStyle
            );

            // Swipe-right quick slot indicator
            if (xOffset > 20f)
            {
                float indicatorWidth = Mathf.Min(xOffset, _rowHeight);
                Rect indicatorRect = new Rect(
                    _padding, yOffset,
                    indicatorWidth, _rowHeight
                );
                GUI.color = _quickSlotIndicatorColor;
                GUI.DrawTexture(indicatorRect, _pixelTex);

                GUI.color = Color.white;
                int slotNum = Mathf.Clamp(Mathf.FloorToInt(xOffset / (_rowHeight * 1.5f)), 0, QuickSlotCount - 1) + 1;
                var slotStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };
                GUI.Label(indicatorRect, $"Q{slotNum}", slotStyle);
            }

            GUI.color = Color.white;

            // Tap to select
            if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
            {
                SelectSpell(index);
                Event.current.Use();
            }

            return yOffset + _rowHeight + _rowGap;
        }

        private float DrawDetailCard(int index, float yOffset, float contentWidth)
        {
            if (index < 0 || index >= _spells.Count)
                return yOffset;

            EffectBundleSettings spell = _spells[index];
            float cardHeight = _detailCardHeight * _cardAnimT;
            Rect cardRect = new Rect(_padding, yOffset, contentWidth - _padding * 2f, cardHeight);

            // Card background
            GUI.color = _cardColor;
            GUI.DrawTexture(cardRect, _pixelTex);

            // Top border accent
            GUI.color = new Color(0.85f, 0.72f, 0.35f, 0.5f);
            GUI.DrawTexture(new Rect(cardRect.x, cardRect.y, cardRect.width, 2f), _pixelTex);

            if (_cardAnimT < 0.3f)
            {
                GUI.color = Color.white;
                return yOffset + cardHeight + _rowGap;
            }

            float innerX = cardRect.x + 12f;
            float innerWidth = cardRect.width - 24f;
            float innerY = cardRect.y + 8f;

            // Spell effects
            GUI.color = _effectTextColor;
            if (spell.Effects != null)
            {
                for (int e = 0; e < spell.Effects.Length; e++)
                {
                    EffectEntry effect = spell.Effects[e];
                    string effectDesc = FormatEffectDescription(effect);

                    GUI.Label(
                        new Rect(innerX, innerY, innerWidth, 18f),
                        $"- {effectDesc}",
                        _schoolStyle
                    );
                    innerY += 20f;

                    // Prevent overflow
                    if (innerY > cardRect.yMax - 40f)
                        break;
                }
            }

            // Target type
            innerY += 4f;
            GUI.color = _schoolColor;
            string targetStr = spell.TargetType.ToString();
            GUI.Label(new Rect(innerX, innerY, innerWidth, 16f), $"Target: {targetStr}", _schoolStyle);
            innerY += 20f;

            // Element
            string elementStr = spell.ElementType.ToString();
            GUI.Label(new Rect(innerX, innerY, innerWidth, 16f), $"Element: {elementStr}", _schoolStyle);

            // Cast button at bottom of card
            float castBtnHeight = 36f;
            Rect castBtnRect = new Rect(
                cardRect.x + 12f,
                cardRect.yMax - castBtnHeight - 8f,
                cardRect.width - 24f,
                castBtnHeight
            );

            GUI.color = new Color(0.2f, 0.35f, 0.2f, 0.9f);
            GUI.DrawTexture(castBtnRect, _pixelTex);
            GUI.color = Color.white;
            var castStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(castBtnRect, "Set as Active Spell", castStyle);

            if (Event.current.type == EventType.MouseDown && castBtnRect.Contains(Event.current.mousePosition))
            {
                SetActiveSpell(spell);
                Event.current.Use();
            }

            GUI.color = Color.white;
            return yOffset + cardHeight + _rowGap;
        }

        #endregion

        #region Input Handling

        private void HandleSwipeInput()
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
                            _swipeRowIndex = GetRowIndexAtScreenY(Screen.height - touch.position.y);
                            _swipeOffsetX = 0f;
                        }
                        break;

                    case TouchPhase.Moved:
                        if (touch.fingerId == _swipeTouchId && _swipeRowIndex >= 0)
                        {
                            float dx = touch.position.x - _swipeStartPos.x;
                            // Only allow right swipe
                            _swipeOffsetX = Mathf.Max(0f, dx);
                        }
                        break;

                    case TouchPhase.Ended:
                        if (touch.fingerId == _swipeTouchId)
                        {
                            if (_swipeRowIndex >= 0 && _swipeOffsetX > SwipeAssignThreshold)
                            {
                                int slotIndex = Mathf.Clamp(
                                    Mathf.FloorToInt(_swipeOffsetX / (_rowHeight * 1.5f)),
                                    0, QuickSlotCount - 1
                                );
                                AssignToQuickSlot(_swipeRowIndex, slotIndex);
                            }

                            _swipeTouchId = -1;
                            _swipeRowIndex = -1;
                            _swipeOffsetX = 0f;
                        }
                        break;

                    case TouchPhase.Canceled:
                        _swipeTouchId = -1;
                        _swipeRowIndex = -1;
                        _swipeOffsetX = 0f;
                        break;
                }
            }
        }

        private int GetRowIndexAtScreenY(float guiY)
        {
            float listTop = _safeArea.y + _titleBarHeight;
            float relativeY = guiY - listTop + _scrollPosition.y;

            if (relativeY < 0)
                return -1;

            // Account for expanded cards when determining which row was touched
            float yAccum = 0f;
            for (int i = 0; i < _spells.Count; i++)
            {
                float rowTotal = _rowHeight + _rowGap;
                if (SelectedSpellIndex == i)
                {
                    rowTotal += _detailCardHeight * _cardAnimT + _rowGap;
                }

                if (relativeY >= yAccum && relativeY < yAccum + _rowHeight)
                    return i;

                yAccum += rowTotal;
            }

            return -1;
        }

        #endregion

        #region Spell Data

        private void RefreshSpellList()
        {
            _spells.Clear();

            if (_playerEntity == null)
                return;

            // Read spells from the player entity's spell book
            EffectBundleSettings[] knownSpells = _playerEntity.GetSpells();
            if (knownSpells == null)
                return;

            for (int i = 0; i < knownSpells.Length; i++)
            {
                _spells.Add(knownSpells[i]);
            }

            Debug.Log($"{LogPrefix} Spellbook refreshed: {_spells.Count} spells loaded.");
        }

        private void SetActiveSpell(EffectBundleSettings spell)
        {
            if (GameManager.Instance == null)
                return;

            EntityEffectManager playerEffectManager = GameManager.Instance.PlayerEffectManager;
            if (playerEffectManager == null)
            {
                Debug.LogWarning($"{LogPrefix} Cannot set active spell — PlayerEffectManager not available.");
                return;
            }

            playerEffectManager.SetReadySpell(new EntityEffectBundle(spell, GameManager.Instance.PlayerEntityBehaviour));
            OnSpellActivated?.Invoke(spell);
            Debug.Log($"{LogPrefix} Active spell set: {spell.Name}");
        }

        private int CalculateSpellCost(EffectBundleSettings spell)
        {
            if (spell.Effects == null || spell.Effects.Length == 0)
                return 0;

            // Use DFU's spell cost calculation when available
            if (GameManager.Instance != null)
            {
                int cost;
                FormulaHelper.CalculateTotalEffectCosts(spell.Effects, spell.TargetType, out cost);
                return cost;
            }

            // Fallback estimate
            return spell.Effects.Length * 5;
        }

        private string GetSpellSchool(EffectBundleSettings spell)
        {
            if (spell.Effects == null || spell.Effects.Length == 0)
                return "Unknown School";

            // Determine school from the first effect's key
            EffectEntry firstEffect = spell.Effects[0];
            if (string.IsNullOrEmpty(firstEffect.Key))
                return "Unknown School";

            // DFU uses effect keys to look up the school; provide a simplified mapping
            string key = firstEffect.Key.ToLower();

            if (key.Contains("destruction") || key.Contains("damage") || key.Contains("drain"))
                return "Destruction";
            if (key.Contains("restoration") || key.Contains("heal") || key.Contains("cure"))
                return "Restoration";
            if (key.Contains("alteration") || key.Contains("levitate") || key.Contains("water"))
                return "Alteration";
            if (key.Contains("illusion") || key.Contains("invisib") || key.Contains("chameleon"))
                return "Illusion";
            if (key.Contains("mysticism") || key.Contains("teleport") || key.Contains("soul"))
                return "Mysticism";
            if (key.Contains("thaumaturgy") || key.Contains("detect") || key.Contains("identify"))
                return "Thaumaturgy";

            return "Arcane";
        }

        private string FormatEffectDescription(EffectEntry effect)
        {
            if (effect.Key == null)
                return "Unknown effect";

            string desc = effect.Key;

            // Include magnitude range if present
            if (effect.Settings.DurationBase > 0)
            {
                desc += $" (Duration: {effect.Settings.DurationBase}";
                if (effect.Settings.DurationPlus > 0)
                    desc += $"+{effect.Settings.DurationPlus}/level";
                desc += ")";
            }

            if (effect.Settings.MagnitudeBaseMin > 0 || effect.Settings.MagnitudeBaseMax > 0)
            {
                desc += $" [{effect.Settings.MagnitudeBaseMin}-{effect.Settings.MagnitudeBaseMax}";
                if (effect.Settings.MagnitudePlusMin > 0 || effect.Settings.MagnitudePlusMax > 0)
                    desc += $" +{effect.Settings.MagnitudePlusMin}-{effect.Settings.MagnitudePlusMax}/level";
                desc += "]";
            }

            return desc;
        }

        #endregion

        #region Utility

        private float CalculateTotalListHeight()
        {
            float total = 0f;
            for (int i = 0; i < _spells.Count; i++)
            {
                total += _rowHeight + _rowGap;
                if (SelectedSpellIndex == i)
                {
                    total += _detailCardHeight * _cardAnimT + _rowGap;
                }
            }
            return total + 80f; // bottom padding
        }

        private void AnimateDetailCard()
        {
            float target = SelectedSpellIndex >= 0 ? 1f : 0f;
            _cardAnimT = Mathf.MoveTowards(_cardAnimT, target, Time.deltaTime * CardAnimSpeed);
        }

        private void CachePlayerEntity()
        {
            if (_playerEntity == null && GameManager.Instance != null)
            {
                _playerEntity = GameManager.Instance.PlayerEntity;
            }
        }

        private void SuppressGameInput(bool suppress)
        {
            if (GameManager.Instance != null)
            {
                InputManager.Instance.IsPaused = suppress;
            }
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
            _rowHeight = _rowHeightDp * dpScale;
            _detailCardHeight = _detailCardHeightDp * dpScale;
            _titleBarHeight = _titleBarHeightDp * dpScale;
            _padding = _paddingDp * dpScale;
            _rowGap = _rowGapDp * dpScale;
        }

        private void EnsureStyles()
        {
            if (_stylesInitialized)
                return;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };

            _spellNameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = _spellNameColor }
            };

            _costStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = _costColor }
            };

            _effectStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = _effectTextColor }
            };

            _schoolStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = _schoolColor }
            };

            _closeBtnStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.8f, 0.4f, 0.4f, 1f) }
            };

            _emptyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color(0.5f, 0.5f, 0.5f, 1f) }
            };

            _stylesInitialized = true;
        }

        #endregion
    }
}
