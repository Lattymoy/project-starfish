using System;
using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Items;

namespace ProjectLegacy.UI.Portrait
{
    /// <summary>
    /// Four quick-access item/spell slots in the bottom HUD bar.
    /// Tap to use, long-press to unequip/view info. Supports drag-to-assign
    /// from inventory.
    /// </summary>
    public class QuickSlotStrip : MonoBehaviour
    {
        private const string LogPrefix = "[ProjectLegacy]";
        private const int SlotCount = 4;

        [Header("Layout")]
        [SerializeField]
        [Tooltip("Size of each slot in pixels.")]
        private float _slotSize = 56f;

        [SerializeField]
        [Tooltip("Gap between slots in pixels.")]
        private float _slotGap = 8f;

        [SerializeField]
        [Tooltip("Background color for empty slots.")]
        private Color _emptySlotColor = new Color(0.2f, 0.2f, 0.2f, 0.6f);

        [SerializeField]
        [Tooltip("Border color for slots.")]
        private Color _slotBorderColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);

        [SerializeField]
        [Tooltip("Highlight color when a slot is pressed.")]
        private Color _pressedColor = new Color(0.4f, 0.4f, 0.4f, 0.8f);

        /// <summary>Fired when a slot is tapped. Parameter: slot index (0-3).</summary>
        public event Action<int> OnSlotActivated;

        /// <summary>Fired when a slot is long-pressed. Parameter: slot index (0-3).</summary>
        public event Action<int> OnSlotLongPressed;

        // Slot tracking
        private readonly DaggerfallUnityItem[] _assignedItems = new DaggerfallUnityItem[SlotCount];
        private readonly float[] _slotPressStartTimes = new float[SlotCount];
        private readonly bool[] _slotPressed = new bool[SlotCount];

        private Texture2D _slotTexture;
        private GUIStyle _countStyle;
        private bool _stylesInitialized;

        private void Start()
        {
            _slotTexture = new Texture2D(1, 1);
            _slotTexture.SetPixel(0, 0, Color.white);
            _slotTexture.Apply();
        }

        private void OnDestroy()
        {
            if (_slotTexture != null) Destroy(_slotTexture);
        }

        /// <summary>
        /// Assigns an item to a quick slot.
        /// </summary>
        /// <param name="slotIndex">Slot index (0-3).</param>
        /// <param name="item">The item to assign, or null to clear.</param>
        public void AssignItem(int slotIndex, DaggerfallUnityItem item)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount)
                return;

            _assignedItems[slotIndex] = item;
            Debug.Log($"{LogPrefix} Quick slot {slotIndex}: {(item != null ? item.ItemName : "cleared")}");
        }

        /// <summary>
        /// Gets the item assigned to a quick slot.
        /// </summary>
        /// <param name="slotIndex">Slot index (0-3).</param>
        /// <returns>The assigned item, or null if empty.</returns>
        public DaggerfallUnityItem GetAssignedItem(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount)
                return null;

            return _assignedItems[slotIndex];
        }

        /// <summary>
        /// Clears all quick slot assignments.
        /// </summary>
        public void ClearAll()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                _assignedItems[i] = null;
            }
        }

        /// <summary>
        /// Activates the item in the specified slot.
        /// </summary>
        /// <param name="slotIndex">Slot index (0-3).</param>
        public void ActivateSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount)
                return;

            var item = _assignedItems[slotIndex];
            if (item == null)
                return;

            // Use the item through DFU's item system
            if (GameManager.Instance != null)
            {
                GameManager.Instance.PlayerEntity.ItemEquipTable.EquipItem(item);
            }

            OnSlotActivated?.Invoke(slotIndex);
            Util.HapticFeedback.LightTap();

            Debug.Log($"{LogPrefix} Quick slot {slotIndex} activated: {item.ItemName}");
        }

        private void OnGUI()
        {
            if (_slotTexture == null)
                return;

            InitStyles();

            var layout = Core.LegacyBootstrapper.Instance != null
                ? Core.LegacyBootstrapper.Instance.ScreenLayout
                : null;

            float totalWidth = SlotCount * _slotSize + (SlotCount - 1) * _slotGap;
            float startX, startY;

            if (layout != null)
            {
                startX = layout.BottomBarRect.x + layout.BottomBarRect.width - totalWidth - 10f;
                startY = Screen.height - layout.BottomBarRect.y - layout.BottomBarRect.height +
                         (layout.BottomBarRect.height - _slotSize) / 2f;
            }
            else
            {
                startX = Screen.width - totalWidth - 10f;
                startY = Screen.height - _slotSize - 20f;
            }

            for (int i = 0; i < SlotCount; i++)
            {
                float slotX = startX + i * (_slotSize + _slotGap);
                DrawSlot(i, slotX, startY);
            }
        }

        private void DrawSlot(int index, float x, float y)
        {
            Rect slotRect = new Rect(x, y, _slotSize, _slotSize);

            // Check for touch input on this slot
            HandleSlotInput(index, slotRect);

            // Background
            GUI.color = _slotPressed[index] ? _pressedColor : _emptySlotColor;
            GUI.DrawTexture(slotRect, _slotTexture);

            // Border
            GUI.color = _slotBorderColor;
            float borderWidth = 1f;
            GUI.DrawTexture(new Rect(x, y, _slotSize, borderWidth), _slotTexture); // top
            GUI.DrawTexture(new Rect(x, y + _slotSize - borderWidth, _slotSize, borderWidth), _slotTexture); // bottom
            GUI.DrawTexture(new Rect(x, y, borderWidth, _slotSize), _slotTexture); // left
            GUI.DrawTexture(new Rect(x + _slotSize - borderWidth, y, borderWidth, _slotSize), _slotTexture); // right

            // Item info
            var item = _assignedItems[index];
            if (item != null)
            {
                // Item name abbreviation
                GUI.color = Color.white;
                string abbrev = item.ItemName.Length > 3
                    ? item.ItemName.Substring(0, 3)
                    : item.ItemName;
                GUI.Label(
                    new Rect(x + 4, y + 4, _slotSize - 8, _slotSize / 2),
                    abbrev,
                    _countStyle
                );

                // Stack count (if applicable)
                if (item.stackCount > 1)
                {
                    GUI.Label(
                        new Rect(x + 4, y + _slotSize - 18, _slotSize - 8, 14),
                        item.stackCount.ToString(),
                        _countStyle
                    );
                }
            }
            else
            {
                // Slot number indicator
                GUI.color = new Color(1f, 1f, 1f, 0.3f);
                GUI.Label(
                    new Rect(x, y, _slotSize, _slotSize),
                    (index + 1).ToString(),
                    new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 16,
                        normal = { textColor = new Color(1f, 1f, 1f, 0.3f) }
                    }
                );
            }

            GUI.color = Color.white;
        }

        private void HandleSlotInput(int index, Rect slotRect)
        {
            for (int t = 0; t < UnityEngine.Input.touchCount; t++)
            {
                Touch touch = UnityEngine.Input.GetTouch(t);
                Vector2 guiPos = new Vector2(touch.position.x, Screen.height - touch.position.y);

                if (!slotRect.Contains(guiPos))
                    continue;

                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        _slotPressed[index] = true;
                        _slotPressStartTimes[index] = Time.time;
                        break;

                    case TouchPhase.Ended:
                        if (_slotPressed[index])
                        {
                            float pressDuration = Time.time - _slotPressStartTimes[index];
                            if (pressDuration > 0.5f)
                            {
                                OnSlotLongPressed?.Invoke(index);
                            }
                            else
                            {
                                ActivateSlot(index);
                            }
                        }
                        _slotPressed[index] = false;
                        break;

                    case TouchPhase.Canceled:
                        _slotPressed[index] = false;
                        break;
                }
            }
        }

        private void InitStyles()
        {
            if (_stylesInitialized)
                return;

            _countStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleRight,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            _stylesInitialized = true;
        }
    }
}
