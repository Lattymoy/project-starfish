using System;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Entity;
using ProjectLegacy.Core;

namespace ProjectLegacy.UI.Panels
{
    /// <summary>
    /// Full-screen inventory overlay for mobile devices. Displays items in a scrollable
    /// three-column grid with swipeable category tabs at the top, a tap-to-inspect bottom
    /// sheet for item details, drag-to-equip support, and a weight/gold status bar at the
    /// bottom of the screen.
    /// </summary>
    public class MobileInventoryPanel : MonoBehaviour
    {
        private const string LogPrefix = "[ProjectLegacy]";
        private const int ColumnCount = 3;
        private const float LongPressDuration = 0.45f;
        private const float BottomSheetAnimSpeed = 6f;
        private const float SwipeCategoryThreshold = 60f;

        #region Serialized Fields

        [Header("Layout")]
        [SerializeField]
        [Tooltip("Height of each inventory cell in dp.")]
        private float _cellHeightDp = 80f;

        [SerializeField]
        [Tooltip("Gap between cells in dp.")]
        private float _cellGapDp = 4f;

        [SerializeField]
        [Tooltip("Height of the category tab bar in dp.")]
        private float _tabBarHeightDp = 44f;

        [SerializeField]
        [Tooltip("Height of the bottom weight/gold bar in dp.")]
        private float _statusBarHeightDp = 36f;

        [SerializeField]
        [Tooltip("Height of the item detail bottom sheet in dp.")]
        private float _bottomSheetHeightDp = 200f;

        [Header("Colors")]
        [SerializeField]
        [Tooltip("Background overlay color.")]
        private Color _overlayColor = new Color(0.05f, 0.05f, 0.08f, 0.92f);

        [SerializeField]
        [Tooltip("Cell background color.")]
        private Color _cellColor = new Color(0.18f, 0.18f, 0.22f, 0.9f);

        [SerializeField]
        [Tooltip("Selected cell highlight color.")]
        private Color _selectedCellColor = new Color(0.35f, 0.30f, 0.20f, 0.95f);

        [SerializeField]
        [Tooltip("Active tab indicator color.")]
        private Color _activeTabColor = new Color(0.85f, 0.72f, 0.35f, 1f);

        [SerializeField]
        [Tooltip("Inactive tab text color.")]
        private Color _inactiveTabColor = new Color(0.6f, 0.6f, 0.6f, 1f);

        [SerializeField]
        [Tooltip("Status bar background color.")]
        private Color _statusBarColor = new Color(0.1f, 0.1f, 0.12f, 0.95f);

        [SerializeField]
        [Tooltip("Weight bar fill color.")]
        private Color _weightBarColor = new Color(0.6f, 0.45f, 0.2f, 1f);

        [SerializeField]
        [Tooltip("Weight bar overloaded color.")]
        private Color _weightOverloadColor = new Color(0.85f, 0.2f, 0.15f, 1f);

        [Header("Quick Slots")]
        [SerializeField]
        [Tooltip("Number of quick slots available for drag-to-assign.")]
        private int _quickSlotCount = 4;

        #endregion

        #region Public Properties

        /// <summary>Whether the inventory panel is currently visible.</summary>
        public bool IsVisible { get; private set; }

        /// <summary>The currently selected item, or null if none.</summary>
        public DaggerfallUnityItem SelectedItem { get; private set; }

        /// <summary>The active category filter index.</summary>
        public int ActiveCategoryIndex { get; private set; }

        /// <summary>Fired when the panel is shown.</summary>
        public event Action OnPanelShown;

        /// <summary>Fired when the panel is hidden.</summary>
        public event Action OnPanelHidden;

        /// <summary>Fired when an item is equipped. Parameter: the equipped item.</summary>
        public event Action<DaggerfallUnityItem> OnItemEquipped;

        /// <summary>Fired when an item is assigned to a quick slot. Parameters: item, slot index.</summary>
        public event Action<DaggerfallUnityItem, int> OnItemAssignedToQuickSlot;

        #endregion

        #region Category Definitions

        private static readonly string[] CategoryNames =
        {
            "All", "Weapons", "Armor", "Clothing", "Potions", "Scrolls", "Misc"
        };

        private static readonly ItemGroups[] CategoryFilters =
        {
            // All = no filter (handled specially)
            ItemGroups.None,
            ItemGroups.Weapons,
            ItemGroups.Armor,
            ItemGroups.MensClothing,
            ItemGroups.UselessItems1,  // potions mapped at runtime
            ItemGroups.Books,
            ItemGroups.MiscItems
        };

        #endregion

        #region Private State

        private PlayerEntity _playerEntity;
        private List<DaggerfallUnityItem> _filteredItems = new List<DaggerfallUnityItem>();
        private Vector2 _scrollPosition;
        private float _bottomSheetT; // 0 = hidden, 1 = fully visible
        private bool _bottomSheetVisible;

        // Touch tracking for grid interaction
        private int _dragTouchId = -1;
        private Vector2 _dragStartPos;
        private float _dragStartTime;
        private bool _isDragging;
        private DaggerfallUnityItem _dragItem;
        private Vector2 _dragCurrentPos;

        // Swipe tracking for category tabs
        private int _swipeTouchId = -1;
        private Vector2 _swipeStartPos;
        private float _swipeStartTime;

        // Textures and styles
        private Texture2D _pixelTex;
        private GUIStyle _itemNameStyle;
        private GUIStyle _itemDetailStyle;
        private GUIStyle _tabStyle;
        private GUIStyle _tabActiveStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _descriptionStyle;
        private bool _stylesInitialized;

        // Layout cache (pixel values, recomputed on resolution change)
        private float _cellHeight;
        private float _cellGap;
        private float _tabBarHeight;
        private float _statusBarHeight;
        private float _bottomSheetHeight;
        private Rect _safeArea;
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
            AnimateBottomSheet();
            HandleTouchInput();
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

            DrawOverlayBackground();
            DrawCategoryTabs();
            DrawItemGrid();
            DrawStatusBar();
            DrawBottomSheet();
            DrawDragGhost();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Shows the inventory panel, pausing game input and refreshing the item list.
        /// </summary>
        public void Show()
        {
            if (IsVisible)
                return;

            CachePlayerEntity();
            if (_playerEntity == null)
            {
                Debug.LogWarning($"{LogPrefix} Cannot show inventory — player entity not available.");
                return;
            }

            IsVisible = true;
            _scrollPosition = Vector2.zero;
            _bottomSheetVisible = false;
            _bottomSheetT = 0f;
            SelectedItem = null;

            RefreshFilteredItems();
            SuppressGameInput(true);

            OnPanelShown?.Invoke();
            Debug.Log($"{LogPrefix} MobileInventoryPanel shown. Category: {CategoryNames[ActiveCategoryIndex]}, Items: {_filteredItems.Count}");
        }

        /// <summary>
        /// Hides the inventory panel and restores game input.
        /// </summary>
        public void Hide()
        {
            if (!IsVisible)
                return;

            IsVisible = false;
            SelectedItem = null;
            _bottomSheetVisible = false;
            _isDragging = false;
            _dragItem = null;

            SuppressGameInput(false);

            OnPanelHidden?.Invoke();
            Debug.Log($"{LogPrefix} MobileInventoryPanel hidden.");
        }

        /// <summary>
        /// Switches to the specified category tab and refreshes the item list.
        /// </summary>
        /// <param name="categoryIndex">Index into the category array (0 = All).</param>
        public void SetCategory(int categoryIndex)
        {
            if (categoryIndex < 0 || categoryIndex >= CategoryNames.Length)
                return;

            ActiveCategoryIndex = categoryIndex;
            _scrollPosition = Vector2.zero;
            RefreshFilteredItems();
            Debug.Log($"{LogPrefix} Inventory category changed: {CategoryNames[categoryIndex]} ({_filteredItems.Count} items)");
        }

        /// <summary>
        /// Attempts to equip the specified item through the DFU equip system.
        /// </summary>
        /// <param name="item">The item to equip.</param>
        /// <returns>True if the item was successfully equipped.</returns>
        public bool TryEquipItem(DaggerfallUnityItem item)
        {
            if (item == null || _playerEntity == null)
                return false;

            if (!item.IsEquipped)
            {
                _playerEntity.ItemEquipTable.EquipItem(item);
                OnItemEquipped?.Invoke(item);
                Debug.Log($"{LogPrefix} Equipped item: {item.ItemName}");
                RefreshFilteredItems();
                return true;
            }

            // Already equipped — unequip
            _playerEntity.ItemEquipTable.UnequipItem(item);
            Debug.Log($"{LogPrefix} Unequipped item: {item.ItemName}");
            RefreshFilteredItems();
            return true;
        }

        /// <summary>
        /// Assigns an item to the specified quick slot index.
        /// </summary>
        /// <param name="item">The item to assign.</param>
        /// <param name="slotIndex">Zero-based quick slot index.</param>
        public void AssignToQuickSlot(DaggerfallUnityItem item, int slotIndex)
        {
            if (item == null || slotIndex < 0 || slotIndex >= _quickSlotCount)
                return;

            OnItemAssignedToQuickSlot?.Invoke(item, slotIndex);
            Debug.Log($"{LogPrefix} Assigned {item.ItemName} to quick slot {slotIndex}");
        }

        #endregion

        #region Drawing

        private void DrawOverlayBackground()
        {
            GUI.color = _overlayColor;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _pixelTex);
            GUI.color = Color.white;
        }

        private void DrawCategoryTabs()
        {
            float tabY = _safeArea.y;
            float tabWidth = _safeArea.width / CategoryNames.Length;

            // Tab bar background
            GUI.color = new Color(0.08f, 0.08f, 0.1f, 0.95f);
            GUI.DrawTexture(new Rect(_safeArea.x, tabY, _safeArea.width, _tabBarHeight), _pixelTex);

            for (int i = 0; i < CategoryNames.Length; i++)
            {
                Rect tabRect = new Rect(_safeArea.x + i * tabWidth, tabY, tabWidth, _tabBarHeight);
                bool isActive = i == ActiveCategoryIndex;

                // Active indicator line
                if (isActive)
                {
                    GUI.color = _activeTabColor;
                    GUI.DrawTexture(
                        new Rect(tabRect.x + 4f, tabRect.yMax - 3f, tabRect.width - 8f, 3f),
                        _pixelTex
                    );
                }

                // Tab label
                GUI.color = isActive ? _activeTabColor : _inactiveTabColor;
                GUI.Label(tabRect, CategoryNames[i], isActive ? _tabActiveStyle : _tabStyle);

                // Tap detection
                if (Event.current.type == EventType.MouseDown && tabRect.Contains(Event.current.mousePosition))
                {
                    SetCategory(i);
                    Event.current.Use();
                }
            }

            GUI.color = Color.white;
        }

        private void DrawItemGrid()
        {
            float gridTop = _safeArea.y + _tabBarHeight;
            float gridBottom = _safeArea.yMax - _statusBarHeight;
            float gridHeight = gridBottom - gridTop;
            Rect gridViewport = new Rect(_safeArea.x, gridTop, _safeArea.width, gridHeight);

            int rowCount = Mathf.CeilToInt((float)_filteredItems.Count / ColumnCount);
            float totalContentHeight = rowCount * (_cellHeight + _cellGap);
            Rect contentRect = new Rect(0, 0, gridViewport.width - 16f, totalContentHeight);

            _scrollPosition = GUI.BeginScrollView(gridViewport, _scrollPosition, contentRect);

            float cellWidth = (contentRect.width - _cellGap * (ColumnCount - 1)) / ColumnCount;

            for (int i = 0; i < _filteredItems.Count; i++)
            {
                int col = i % ColumnCount;
                int row = i / ColumnCount;
                float cellX = col * (cellWidth + _cellGap);
                float cellY = row * (_cellHeight + _cellGap);
                Rect cellRect = new Rect(cellX, cellY, cellWidth, _cellHeight);

                DrawItemCell(cellRect, _filteredItems[i]);
            }

            GUI.EndScrollView();
        }

        private void DrawItemCell(Rect rect, DaggerfallUnityItem item)
        {
            if (item == null)
                return;

            bool isSelected = SelectedItem == item;
            bool isEquipped = item.IsEquipped;

            // Cell background
            Color bgColor = isSelected ? _selectedCellColor : _cellColor;
            if (isEquipped)
            {
                bgColor = Color.Lerp(bgColor, new Color(0.2f, 0.4f, 0.2f, 0.9f), 0.4f);
            }

            GUI.color = bgColor;
            GUI.DrawTexture(rect, _pixelTex);

            // Equipped indicator strip
            if (isEquipped)
            {
                GUI.color = new Color(0.3f, 0.7f, 0.3f, 1f);
                GUI.DrawTexture(new Rect(rect.x, rect.y, 3f, rect.height), _pixelTex);
            }

            // Item name
            GUI.color = Color.white;
            float textPadding = isEquipped ? 8f : 4f;
            Rect nameRect = new Rect(
                rect.x + textPadding, rect.y + 4f,
                rect.width - textPadding - 4f, rect.height * 0.5f
            );
            GUI.Label(nameRect, item.ItemName, _itemNameStyle);

            // Item details (weight, value)
            Rect detailRect = new Rect(
                rect.x + textPadding, rect.y + rect.height * 0.5f,
                rect.width - textPadding - 4f, rect.height * 0.4f
            );
            string weightStr = $"{item.weightInKg:F1} kg";
            string valueStr = $"{item.value} gp";
            string stackStr = item.stackCount > 1 ? $" x{item.stackCount}" : "";
            GUI.Label(detailRect, $"{weightStr}  {valueStr}{stackStr}", _itemDetailStyle);

            // Tap detection
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                SelectItem(item);
                Event.current.Use();
            }
        }

        private void DrawStatusBar()
        {
            if (_playerEntity == null)
                return;

            float barY = _safeArea.yMax - _statusBarHeight;
            Rect barRect = new Rect(_safeArea.x, barY, _safeArea.width, _statusBarHeight);

            // Background
            GUI.color = _statusBarColor;
            GUI.DrawTexture(barRect, _pixelTex);

            float padding = 8f;
            float barInnerWidth = barRect.width - padding * 2f;
            float halfWidth = barInnerWidth * 0.5f;

            // Weight bar
            float currentWeight = _playerEntity.CarriedWeight;
            float maxWeight = _playerEntity.MaxEncumbrance;
            float weightRatio = maxWeight > 0 ? Mathf.Clamp01(currentWeight / maxWeight) : 0f;
            bool overloaded = currentWeight > maxWeight;

            Rect weightBgRect = new Rect(barRect.x + padding, barY + 8f, halfWidth - 4f, 10f);
            GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            GUI.DrawTexture(weightBgRect, _pixelTex);

            GUI.color = overloaded ? _weightOverloadColor : _weightBarColor;
            GUI.DrawTexture(new Rect(weightBgRect.x, weightBgRect.y, weightBgRect.width * weightRatio, weightBgRect.height), _pixelTex);

            GUI.color = Color.white;
            GUI.Label(
                new Rect(barRect.x + padding, barY + 20f, halfWidth, 14f),
                $"Weight: {currentWeight:F1}/{maxWeight:F0}",
                _statusStyle
            );

            // Gold display
            int goldAmount = _playerEntity.GoldPieces;
            GUI.Label(
                new Rect(barRect.x + padding + halfWidth + 4f, barY + 8f, halfWidth - 4f, barRect.height - 8f),
                $"Gold: {goldAmount:N0}",
                _statusStyle
            );
        }

        private void DrawBottomSheet()
        {
            if (_bottomSheetT <= 0.01f || SelectedItem == null)
                return;

            float sheetY = Screen.height - _bottomSheetHeight * _bottomSheetT;
            Rect sheetRect = new Rect(0, sheetY, Screen.width, _bottomSheetHeight);

            // Shadow
            GUI.color = new Color(0f, 0f, 0f, 0.4f * _bottomSheetT);
            GUI.DrawTexture(new Rect(0, sheetY - 4f, Screen.width, 4f), _pixelTex);

            // Background
            GUI.color = new Color(0.12f, 0.12f, 0.15f, 0.98f);
            GUI.DrawTexture(sheetRect, _pixelTex);

            // Drag handle
            float handleWidth = 40f;
            GUI.color = new Color(0.4f, 0.4f, 0.4f, 0.6f);
            GUI.DrawTexture(
                new Rect(Screen.width * 0.5f - handleWidth * 0.5f, sheetY + 6f, handleWidth, 4f),
                _pixelTex
            );

            float contentX = _safeArea.x + 16f;
            float contentWidth = _safeArea.width - 32f;
            float yOffset = sheetY + 20f;

            GUI.color = Color.white;

            // Item name header
            GUI.Label(
                new Rect(contentX, yOffset, contentWidth, 24f),
                SelectedItem.ItemName,
                _headerStyle
            );
            yOffset += 28f;

            // Equipped status
            if (SelectedItem.IsEquipped)
            {
                GUI.color = new Color(0.4f, 0.8f, 0.4f, 1f);
                GUI.Label(new Rect(contentX, yOffset, contentWidth, 18f), "[Equipped]", _itemDetailStyle);
                GUI.color = Color.white;
                yOffset += 20f;
            }

            // Stats line
            string statsLine = $"Weight: {SelectedItem.weightInKg:F1} kg    Value: {SelectedItem.value} gp";
            if (SelectedItem.stackCount > 1)
                statsLine += $"    Stack: {SelectedItem.stackCount}";
            GUI.Label(new Rect(contentX, yOffset, contentWidth, 18f), statsLine, _itemDetailStyle);
            yOffset += 22f;

            // Condition bar
            if (SelectedItem.maxCondition > 0)
            {
                float condRatio = Mathf.Clamp01((float)SelectedItem.currentCondition / SelectedItem.maxCondition);
                GUI.Label(new Rect(contentX, yOffset, 80f, 16f), "Condition:", _itemDetailStyle);

                Rect condBg = new Rect(contentX + 85f, yOffset + 2f, 120f, 10f);
                GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);
                GUI.DrawTexture(condBg, _pixelTex);

                Color condColor = condRatio > 0.5f
                    ? Color.Lerp(Color.yellow, Color.green, (condRatio - 0.5f) * 2f)
                    : Color.Lerp(Color.red, Color.yellow, condRatio * 2f);
                GUI.color = condColor;
                GUI.DrawTexture(new Rect(condBg.x, condBg.y, condBg.width * condRatio, condBg.height), _pixelTex);
                GUI.color = Color.white;
                yOffset += 20f;
            }

            // Action buttons
            float buttonWidth = (contentWidth - 8f) * 0.5f;
            float buttonHeight = 40f;
            float buttonY = sheetRect.yMax - buttonHeight - 16f;

            // Equip / Unequip button
            string equipLabel = SelectedItem.IsEquipped ? "Unequip" : "Equip";
            Rect equipBtnRect = new Rect(contentX, buttonY, buttonWidth, buttonHeight);
            GUI.color = new Color(0.2f, 0.2f, 0.25f, 0.9f);
            GUI.DrawTexture(equipBtnRect, _pixelTex);
            GUI.color = Color.white;
            GUI.Label(equipBtnRect, equipLabel, _tabActiveStyle);

            if (Event.current.type == EventType.MouseDown && equipBtnRect.Contains(Event.current.mousePosition))
            {
                TryEquipItem(SelectedItem);
                Event.current.Use();
            }

            // Drop button
            Rect dropBtnRect = new Rect(contentX + buttonWidth + 8f, buttonY, buttonWidth, buttonHeight);
            GUI.color = new Color(0.3f, 0.15f, 0.15f, 0.9f);
            GUI.DrawTexture(dropBtnRect, _pixelTex);
            GUI.color = new Color(0.9f, 0.5f, 0.5f, 1f);
            GUI.Label(dropBtnRect, "Drop", _tabActiveStyle);

            if (Event.current.type == EventType.MouseDown && dropBtnRect.Contains(Event.current.mousePosition))
            {
                DropSelectedItem();
                Event.current.Use();
            }

            GUI.color = Color.white;
        }

        private void DrawDragGhost()
        {
            if (!_isDragging || _dragItem == null)
                return;

            Vector2 guiPos = new Vector2(_dragCurrentPos.x, Screen.height - _dragCurrentPos.y);
            float ghostSize = _cellHeight * 0.8f;
            Rect ghostRect = new Rect(guiPos.x - ghostSize * 0.5f, guiPos.y - ghostSize * 0.5f, ghostSize, ghostSize);

            GUI.color = new Color(0.3f, 0.3f, 0.35f, 0.7f);
            GUI.DrawTexture(ghostRect, _pixelTex);
            GUI.color = new Color(1f, 1f, 1f, 0.8f);
            GUI.Label(ghostRect, _dragItem.ItemName, _itemNameStyle);
            GUI.color = Color.white;

            // Quick slot drop zones at the bottom
            float zoneHeight = 50f;
            float zoneWidth = _safeArea.width / _quickSlotCount;
            float zoneY = _safeArea.yMax - _statusBarHeight - zoneHeight;

            for (int i = 0; i < _quickSlotCount; i++)
            {
                Rect slotZone = new Rect(_safeArea.x + i * zoneWidth, zoneY, zoneWidth, zoneHeight);
                bool hovered = slotZone.Contains(guiPos);

                GUI.color = hovered
                    ? new Color(0.4f, 0.6f, 0.3f, 0.6f)
                    : new Color(0.2f, 0.2f, 0.25f, 0.4f);
                GUI.DrawTexture(slotZone, _pixelTex);

                GUI.color = Color.white;
                GUI.Label(slotZone, $"Slot {i + 1}", _tabStyle);
            }
        }

        #endregion

        #region Input Handling

        private void HandleTouchInput()
        {
            for (int t = 0; t < Input.touchCount; t++)
            {
                Touch touch = Input.GetTouch(t);

                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        HandleTouchBegan(touch);
                        break;

                    case TouchPhase.Moved:
                        HandleTouchMoved(touch);
                        break;

                    case TouchPhase.Ended:
                        HandleTouchEnded(touch);
                        break;

                    case TouchPhase.Canceled:
                        CancelDrag();
                        _swipeTouchId = -1;
                        break;
                }
            }

            // Check for long-press to initiate drag
            if (_dragTouchId >= 0 && !_isDragging)
            {
                float holdDuration = Time.time - _dragStartTime;
                if (holdDuration >= LongPressDuration && _dragItem != null)
                {
                    _isDragging = true;
                    Debug.Log($"{LogPrefix} Drag started: {_dragItem.ItemName}");
                }
            }
        }

        private void HandleTouchBegan(Touch touch)
        {
            Vector2 guiPos = new Vector2(touch.position.x, Screen.height - touch.position.y);

            // Check if touch is in tab bar area for category swiping
            Rect tabArea = new Rect(_safeArea.x, _safeArea.y, _safeArea.width, _tabBarHeight);
            if (tabArea.Contains(guiPos))
            {
                _swipeTouchId = touch.fingerId;
                _swipeStartPos = touch.position;
                _swipeStartTime = Time.time;
                return;
            }

            // Track for potential drag
            if (_dragTouchId < 0)
            {
                _dragTouchId = touch.fingerId;
                _dragStartPos = touch.position;
                _dragCurrentPos = touch.position;
                _dragStartTime = Time.time;
                _isDragging = false;

                // Determine which item was touched
                _dragItem = GetItemAtScreenPosition(guiPos);
            }
        }

        private void HandleTouchMoved(Touch touch)
        {
            if (touch.fingerId == _swipeTouchId)
            {
                // Handled on end
                return;
            }

            if (touch.fingerId == _dragTouchId)
            {
                _dragCurrentPos = touch.position;
            }
        }

        private void HandleTouchEnded(Touch touch)
        {
            // Category swipe
            if (touch.fingerId == _swipeTouchId)
            {
                float dx = touch.position.x - _swipeStartPos.x;
                float dt = Time.time - _swipeStartTime;

                if (Mathf.Abs(dx) > SwipeCategoryThreshold && dt < 0.4f)
                {
                    int dir = dx < 0 ? 1 : -1;
                    int newIndex = Mathf.Clamp(ActiveCategoryIndex + dir, 0, CategoryNames.Length - 1);
                    SetCategory(newIndex);
                }

                _swipeTouchId = -1;
                return;
            }

            // Drag drop
            if (touch.fingerId == _dragTouchId)
            {
                if (_isDragging && _dragItem != null)
                {
                    Vector2 guiPos = new Vector2(touch.position.x, Screen.height - touch.position.y);
                    int slotIndex = GetQuickSlotAtPosition(guiPos);
                    if (slotIndex >= 0)
                    {
                        AssignToQuickSlot(_dragItem, slotIndex);
                    }
                }

                CancelDrag();
            }
        }

        private void CancelDrag()
        {
            _dragTouchId = -1;
            _isDragging = false;
            _dragItem = null;
        }

        private int GetQuickSlotAtPosition(Vector2 guiPos)
        {
            float zoneHeight = 50f;
            float zoneWidth = _safeArea.width / _quickSlotCount;
            float zoneY = _safeArea.yMax - _statusBarHeight - zoneHeight;

            for (int i = 0; i < _quickSlotCount; i++)
            {
                Rect slotZone = new Rect(_safeArea.x + i * zoneWidth, zoneY, zoneWidth, zoneHeight);
                if (slotZone.Contains(guiPos))
                    return i;
            }

            return -1;
        }

        private DaggerfallUnityItem GetItemAtScreenPosition(Vector2 guiPos)
        {
            float gridTop = _safeArea.y + _tabBarHeight;
            float cellWidth = (_safeArea.width - 16f - _cellGap * (ColumnCount - 1)) / ColumnCount;

            float relX = guiPos.x - _safeArea.x;
            float relY = guiPos.y - gridTop + _scrollPosition.y;

            if (relX < 0 || relY < 0)
                return null;

            int col = Mathf.FloorToInt(relX / (cellWidth + _cellGap));
            int row = Mathf.FloorToInt(relY / (_cellHeight + _cellGap));

            if (col < 0 || col >= ColumnCount)
                return null;

            int index = row * ColumnCount + col;
            if (index < 0 || index >= _filteredItems.Count)
                return null;

            return _filteredItems[index];
        }

        #endregion

        #region Item Management

        private void SelectItem(DaggerfallUnityItem item)
        {
            if (item == null)
                return;

            SelectedItem = item;
            _bottomSheetVisible = true;
            Debug.Log($"{LogPrefix} Selected item: {item.ItemName}");
        }

        private void DropSelectedItem()
        {
            if (SelectedItem == null || _playerEntity == null)
                return;

            Debug.Log($"{LogPrefix} Dropping item: {SelectedItem.ItemName}");

            // Remove from player inventory and create world object
            _playerEntity.Items.RemoveItem(SelectedItem);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.PlayerEntity.Items.RemoveItem(SelectedItem);
            }

            SelectedItem = null;
            _bottomSheetVisible = false;
            RefreshFilteredItems();
        }

        private void RefreshFilteredItems()
        {
            _filteredItems.Clear();

            if (_playerEntity == null)
                return;

            ItemCollection items = _playerEntity.Items;
            if (items == null)
                return;

            for (int i = 0; i < items.Count; i++)
            {
                DaggerfallUnityItem item = items.GetItem(i);
                if (item == null)
                    continue;

                if (ActiveCategoryIndex == 0)
                {
                    // "All" category — show everything
                    _filteredItems.Add(item);
                }
                else if (MatchesCategory(item, ActiveCategoryIndex))
                {
                    _filteredItems.Add(item);
                }
            }
        }

        private bool MatchesCategory(DaggerfallUnityItem item, int categoryIndex)
        {
            if (item == null || categoryIndex < 0 || categoryIndex >= CategoryFilters.Length)
                return false;

            switch (categoryIndex)
            {
                case 1: // Weapons
                    return item.ItemGroup == ItemGroups.Weapons;
                case 2: // Armor
                    return item.ItemGroup == ItemGroups.Armor;
                case 3: // Clothing
                    return item.ItemGroup == ItemGroups.MensClothing ||
                           item.ItemGroup == ItemGroups.WomensClothing;
                case 4: // Potions
                    return item.IsPotion;
                case 5: // Scrolls / Books
                    return item.ItemGroup == ItemGroups.Books;
                case 6: // Misc
                    return item.ItemGroup == ItemGroups.MiscItems ||
                           item.ItemGroup == ItemGroups.UselessItems1 ||
                           item.ItemGroup == ItemGroups.UselessItems2;
                default:
                    return false;
            }
        }

        #endregion

        #region Utility

        private void AnimateBottomSheet()
        {
            float target = _bottomSheetVisible ? 1f : 0f;
            _bottomSheetT = Mathf.MoveTowards(_bottomSheetT, target, Time.deltaTime * BottomSheetAnimSpeed);
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
            if (GameManager.Instance == null)
                return;

            InputManager.Instance.IsPaused = suppress;
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
            _cellHeight = _cellHeightDp * dpScale;
            _cellGap = _cellGapDp * dpScale;
            _tabBarHeight = _tabBarHeightDp * dpScale;
            _statusBarHeight = _statusBarHeightDp * dpScale;
            _bottomSheetHeight = _bottomSheetHeightDp * dpScale;
        }

        private void EnsureStyles()
        {
            if (_stylesInitialized)
                return;

            _itemNameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = Color.white }
            };

            _itemDetailStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f, 1f) }
            };

            _tabStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Normal,
                normal = { textColor = _inactiveTabColor }
            };

            _tabActiveStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = _activeTabColor }
            };

            _statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f, 1f) }
            };

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };

            _descriptionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = new Color(0.75f, 0.75f, 0.75f, 1f) }
            };

            _stylesInitialized = true;
        }

        #endregion
    }
}
