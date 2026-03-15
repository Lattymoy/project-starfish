using System;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using ProjectLegacy.Core;

namespace ProjectLegacy.UI.Panels
{
    /// <summary>
    /// Full-screen travel map panel for mobile devices. Displays the overworld map with
    /// tappable location markers. When a location is selected, a confirmation bottom sheet
    /// slides up showing estimated travel time and travel options (cautious/reckless travel
    /// mode, inn/camp rest preference). Integrates with DFU's <see cref="TravelTimeCalculator"/>
    /// and <see cref="DaggerfallTravelPopUp"/> systems for actual travel execution.
    /// </summary>
    public class MobileTravelMap : MonoBehaviour
    {
        private const string LogPrefix = "[ProjectLegacy]";
        private const float BottomSheetAnimSpeed = 7f;
        private const float MinZoom = 0.5f;
        private const float MaxZoom = 5f;
        private const float ZoomSmoothSpeed = 8f;
        private const float PanSmoothSpeed = 10f;

        #region Serialized Fields

        [Header("Map Display")]
        [SerializeField]
        [Tooltip("Initial zoom level when the map opens.")]
        private float _initialZoom = 1f;

        [SerializeField]
        [Range(0.01f, 0.1f)]
        [Tooltip("Pinch zoom sensitivity.")]
        private float _pinchZoomSensitivity = 0.04f;

        [SerializeField]
        [Tooltip("Tap radius for selecting locations in pixels.")]
        private float _locationTapRadius = 28f;

        [Header("Bottom Sheet")]
        [SerializeField]
        [Tooltip("Height of the travel confirmation bottom sheet in dp.")]
        private float _sheetHeightDp = 280f;

        [Header("Colors")]
        [SerializeField]
        [Tooltip("Map background color.")]
        private Color _backgroundColor = new Color(0.02f, 0.02f, 0.04f, 1f);

        [SerializeField]
        [Tooltip("Location marker color.")]
        private Color _markerColor = new Color(0.9f, 0.8f, 0.3f, 1f);

        [SerializeField]
        [Tooltip("Selected location marker color.")]
        private Color _selectedMarkerColor = new Color(1f, 0.5f, 0.2f, 1f);

        [SerializeField]
        [Tooltip("Player position marker color.")]
        private Color _playerColor = new Color(0.2f, 0.6f, 0.95f, 1f);

        [SerializeField]
        [Tooltip("Bottom sheet background color.")]
        private Color _sheetColor = new Color(0.1f, 0.1f, 0.13f, 0.97f);

        [SerializeField]
        [Tooltip("Option button normal color.")]
        private Color _optionBtnColor = new Color(0.18f, 0.18f, 0.22f, 0.95f);

        [SerializeField]
        [Tooltip("Option button active/selected color.")]
        private Color _optionActiveColor = new Color(0.85f, 0.72f, 0.35f, 1f);

        [SerializeField]
        [Tooltip("Travel/confirm button color.")]
        private Color _travelBtnColor = new Color(0.2f, 0.45f, 0.2f, 0.95f);

        [SerializeField]
        [Tooltip("Cancel button color.")]
        private Color _cancelBtnColor = new Color(0.35f, 0.15f, 0.15f, 0.95f);

        [SerializeField]
        [Tooltip("Close button color.")]
        private Color _closeBtnColor = new Color(0.8f, 0.3f, 0.3f, 1f);

        #endregion

        #region Travel Options

        /// <summary>
        /// Travel speed mode selection.
        /// </summary>
        public enum TravelMode
        {
            /// <summary>Slower but safer; lower encounter chance.</summary>
            Cautious,
            /// <summary>Faster but riskier; higher encounter chance.</summary>
            Reckless
        }

        /// <summary>
        /// Rest preference during travel.
        /// </summary>
        public enum RestPreference
        {
            /// <summary>Rest at inns in towns (costs gold, fully restores).</summary>
            Inn,
            /// <summary>Camp in the wilderness (free, partial rest).</summary>
            Camp
        }

        #endregion

        #region Public Properties

        /// <summary>Whether the travel map panel is currently visible.</summary>
        public bool IsVisible { get; private set; }

        /// <summary>The currently selected destination location name, or null.</summary>
        public string SelectedLocationName { get; private set; }

        /// <summary>Current travel mode selection.</summary>
        public TravelMode SelectedTravelMode { get; private set; } = TravelMode.Cautious;

        /// <summary>Current rest preference selection.</summary>
        public RestPreference SelectedRestPreference { get; private set; } = RestPreference.Inn;

        /// <summary>Estimated travel time in days for the current selection.</summary>
        public int EstimatedTravelDays { get; private set; }

        /// <summary>Fired when travel is confirmed. Parameter: destination name.</summary>
        public event Action<string> OnTravelConfirmed;

        /// <summary>Fired when the panel is dismissed.</summary>
        public event Action OnPanelDismissed;

        #endregion

        #region Private State

        // Map display
        private Texture2D _mapTexture;
        private Texture2D _pixelTex;
        private float _currentZoom;
        private float _targetZoom;
        private Vector2 _panOffset;
        private Vector2 _targetPanOffset;

        // Pinch
        private float _lastPinchDistance;
        private bool _isPinching;

        // Pan
        private int _panTouchId = -1;
        private Vector2 _panPrevPos;

        // Bottom sheet
        private float _sheetT; // 0 = hidden, 1 = shown
        private bool _sheetTargetVisible;
        private float _sheetHeight;

        // Selected location data
        private DFLocation _selectedLocation;
        private bool _hasSelectedLocation;
        private Vector2 _selectedLocationScreenPos;

        // Location data cache
        private LocationEntry[] _regionLocations;
        private bool _locationsLoaded;
        private int _loadedRegionIndex = -1;

        // HUD
        private UI.Portrait.PortraitHUD _portraitHUD;
        private bool _hudWasVisible;

        // Styles
        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _valueStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _markerLabelStyle;
        private bool _stylesInitialized;

        private Rect _safeArea;
        private int _lastScreenWidth;
        private int _lastScreenHeight;

        #endregion

        #region Nested Types

        private struct LocationEntry
        {
            public string Name;
            public Vector2 NormalizedPosition;
            public bool Discovered;
            public DFRegion.LocationTypes LocationType;
            public int RegionIndex;
            public int LocationIndex;
        }

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

            HandleMapInput();
            SmoothZoomAndPan();
            AnimateBottomSheet();
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

            DrawMapBackground();
            DrawMapTexture();
            DrawLocationMarkers();
            DrawPlayerPosition();
            DrawSelectedLocationHighlight();
            DrawCloseButton();
            DrawBottomSheet();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Shows the travel map panel, centering on the player's current position.
        /// </summary>
        public void Show()
        {
            if (IsVisible)
                return;

            IsVisible = true;
            _currentZoom = _initialZoom;
            _targetZoom = _initialZoom;
            _panOffset = Vector2.zero;
            _targetPanOffset = Vector2.zero;
            _sheetT = 0f;
            _sheetTargetVisible = false;
            _hasSelectedLocation = false;
            SelectedLocationName = null;

            HideHud();
            SuppressGameInput(true);
            LoadMapTexture();
            LoadRegionLocations();

            Debug.Log($"{LogPrefix} MobileTravelMap shown.");
        }

        /// <summary>
        /// Shows the travel map panel pre-selecting the specified location.
        /// Called from <see cref="MobileMapPanel"/> when a location is tapped.
        /// </summary>
        /// <param name="locationName">The location name to pre-select.</param>
        public void ShowForLocation(string locationName)
        {
            Show();

            if (!string.IsNullOrEmpty(locationName))
            {
                SelectLocationByName(locationName);
            }
        }

        /// <summary>
        /// Hides the travel map panel and restores game state.
        /// </summary>
        public void Hide()
        {
            if (!IsVisible)
                return;

            IsVisible = false;
            _hasSelectedLocation = false;
            _sheetTargetVisible = false;

            RestoreHud();
            SuppressGameInput(false);

            OnPanelDismissed?.Invoke();
            Debug.Log($"{LogPrefix} MobileTravelMap hidden.");
        }

        /// <summary>
        /// Initiates travel to the currently selected location using the chosen options.
        /// Delegates to DFU's travel system for actual implementation.
        /// </summary>
        public void ConfirmTravel()
        {
            if (!_hasSelectedLocation || string.IsNullOrEmpty(SelectedLocationName))
            {
                Debug.LogWarning($"{LogPrefix} Cannot confirm travel — no location selected.");
                return;
            }

            Debug.Log($"{LogPrefix} Travel confirmed to {SelectedLocationName}. " +
                       $"Mode: {SelectedTravelMode}, Rest: {SelectedRestPreference}, " +
                       $"Est. days: {EstimatedTravelDays}");

            // Initiate travel through DFU's travel system
            BeginDfuTravel();

            OnTravelConfirmed?.Invoke(SelectedLocationName);
            Hide();
        }

        /// <summary>
        /// Cancels the current location selection and hides the bottom sheet.
        /// </summary>
        public void CancelSelection()
        {
            _hasSelectedLocation = false;
            _sheetTargetVisible = false;
            SelectedLocationName = null;
            Debug.Log($"{LogPrefix} Location selection cancelled.");
        }

        #endregion

        #region Drawing

        private void DrawMapBackground()
        {
            GUI.color = _backgroundColor;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _pixelTex);
            GUI.color = Color.white;
        }

        private void DrawMapTexture()
        {
            if (_mapTexture == null)
            {
                GUI.color = new Color(0.12f, 0.12f, 0.15f, 1f);
                Rect placeholder = new Rect(
                    _safeArea.x + 20f, _safeArea.y + 50f,
                    _safeArea.width - 40f, _safeArea.height - 100f
                );
                GUI.DrawTexture(placeholder, _pixelTex);
                GUI.color = new Color(0.4f, 0.4f, 0.4f, 1f);
                GUI.Label(placeholder, "Travel Map", _labelStyle);
                GUI.color = Color.white;
                return;
            }

            Rect mapRect = CalculateMapRect();
            GUI.color = Color.white;
            GUI.DrawTexture(mapRect, _mapTexture);
        }

        private void DrawLocationMarkers()
        {
            if (_regionLocations == null)
                return;

            for (int i = 0; i < _regionLocations.Length; i++)
            {
                LocationEntry loc = _regionLocations[i];
                Vector2 screenPos = NormalizedToScreen(loc.NormalizedPosition);

                // Viewport culling
                if (screenPos.x < _safeArea.x - 20f || screenPos.x > _safeArea.xMax + 20f ||
                    screenPos.y < _safeArea.y - 20f || screenPos.y > _safeArea.yMax + 20f)
                    continue;

                bool isSelected = _hasSelectedLocation && loc.Name == SelectedLocationName;
                float size = GetMarkerSize(loc.LocationType);

                if (isSelected)
                {
                    // Pulsing selected marker
                    float pulse = 1f + Mathf.PingPong(Time.time * 3f, 0.4f);
                    size *= pulse;
                }

                Rect markerRect = new Rect(
                    screenPos.x - size * 0.5f,
                    screenPos.y - size * 0.5f,
                    size, size
                );

                Color markerCol = isSelected ? _selectedMarkerColor
                    : loc.Discovered ? _markerColor
                    : new Color(0.35f, 0.35f, 0.35f, 0.4f);
                GUI.color = markerCol;
                GUI.DrawTexture(markerRect, _pixelTex);

                // Label at sufficient zoom
                if (_currentZoom >= 1.8f && loc.Discovered)
                {
                    GUI.color = Color.white;
                    Rect labelRect = new Rect(screenPos.x + size, screenPos.y - 7f, 130f, 14f);
                    GUI.Label(labelRect, loc.Name, _markerLabelStyle);
                }

                // Tap detection
                Rect tapTarget = new Rect(
                    screenPos.x - _locationTapRadius,
                    screenPos.y - _locationTapRadius,
                    _locationTapRadius * 2f,
                    _locationTapRadius * 2f
                );

                if (Event.current.type == EventType.MouseDown && tapTarget.Contains(Event.current.mousePosition))
                {
                    SelectLocation(i);
                    Event.current.Use();
                }
            }

            GUI.color = Color.white;
        }

        private void DrawPlayerPosition()
        {
            Vector2 playerNorm = GetPlayerNormalizedPosition();
            Vector2 screenPos = NormalizedToScreen(playerNorm);

            float size = 10f;
            float pulse = 0.75f + Mathf.PingPong(Time.time * 2f, 0.25f);

            GUI.color = _playerColor * pulse;
            Rect markerRect = new Rect(screenPos.x - size * 0.5f, screenPos.y - size * 0.5f, size, size);
            GUI.DrawTexture(markerRect, _pixelTex);

            // Small direction arrow
            GUI.color = _playerColor;
            GUI.DrawTexture(new Rect(screenPos.x - 1f, screenPos.y - size, 2f, size * 0.6f), _pixelTex);

            GUI.color = Color.white;
        }

        private void DrawSelectedLocationHighlight()
        {
            if (!_hasSelectedLocation || _regionLocations == null)
                return;

            // Draw a line from player to selected location
            Vector2 playerScreen = NormalizedToScreen(GetPlayerNormalizedPosition());
            Vector2 destScreen = _selectedLocationScreenPos;

            DrawDottedLine(playerScreen, destScreen, new Color(1f, 1f, 1f, 0.2f), 4f, 8f);
        }

        private void DrawCloseButton()
        {
            float btnSize = 44f;
            Rect closeRect = new Rect(_safeArea.x + 8f, _safeArea.y + 8f, btnSize, btnSize);

            GUI.color = new Color(0.1f, 0.1f, 0.12f, 0.85f);
            GUI.DrawTexture(closeRect, _pixelTex);

            GUI.color = _closeBtnColor;
            var closeBtnStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _closeBtnColor }
            };
            GUI.Label(closeRect, "X", closeBtnStyle);

            if (Event.current.type == EventType.MouseDown && closeRect.Contains(Event.current.mousePosition))
            {
                Hide();
                Event.current.Use();
            }

            GUI.color = Color.white;
        }

        private void DrawBottomSheet()
        {
            if (_sheetT <= 0.01f)
                return;

            float sheetY = Screen.height - _sheetHeight * _sheetT;
            Rect sheetRect = new Rect(0, sheetY, Screen.width, _sheetHeight);

            // Shadow
            GUI.color = new Color(0f, 0f, 0f, 0.4f * _sheetT);
            GUI.DrawTexture(new Rect(0, sheetY - 4f, Screen.width, 4f), _pixelTex);

            // Background
            GUI.color = _sheetColor;
            GUI.DrawTexture(sheetRect, _pixelTex);

            // Top edge
            GUI.color = new Color(0.3f, 0.3f, 0.33f, 0.5f);
            GUI.DrawTexture(new Rect(0, sheetY, Screen.width, 1f), _pixelTex);

            // Drag handle
            float handleW = 40f;
            GUI.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
            GUI.DrawTexture(new Rect(Screen.width * 0.5f - handleW * 0.5f, sheetY + 8f, handleW, 4f), _pixelTex);

            float contentX = _safeArea.x + 16f;
            float contentW = _safeArea.width - 32f;
            float yOff = sheetY + 22f;

            // Destination name
            GUI.color = Color.white;
            GUI.Label(new Rect(contentX, yOff, contentW, 24f),
                $"Travel to: {SelectedLocationName ?? "Unknown"}", _headerStyle);
            yOff += 30f;

            // Travel time estimate
            GUI.color = new Color(0.8f, 0.8f, 0.75f, 1f);
            string timeStr = EstimatedTravelDays == 1 ? "1 day" : $"{EstimatedTravelDays} days";
            GUI.Label(new Rect(contentX, yOff, contentW, 20f),
                $"Estimated travel time: {timeStr}", _labelStyle);
            yOff += 28f;

            // Travel mode toggle
            yOff = DrawTravelModeToggle(contentX, yOff, contentW);
            yOff += 10f;

            // Rest preference toggle
            yOff = DrawRestPreferenceToggle(contentX, yOff, contentW);
            yOff += 16f;

            // Action buttons
            DrawActionButtons(contentX, yOff, contentW);

            GUI.color = Color.white;
        }

        private float DrawTravelModeToggle(float x, float yOff, float width)
        {
            GUI.color = new Color(0.6f, 0.6f, 0.6f, 1f);
            GUI.Label(new Rect(x, yOff, width, 18f), "Travel Speed:", _labelStyle);
            yOff += 22f;

            float halfWidth = (width - 8f) * 0.5f;
            float btnHeight = 40f;

            // Cautious button
            bool isCautious = SelectedTravelMode == TravelMode.Cautious;
            Rect cautiousRect = new Rect(x, yOff, halfWidth, btnHeight);

            GUI.color = isCautious ? _optionActiveColor : _optionBtnColor;
            GUI.DrawTexture(cautiousRect, _pixelTex);

            GUI.color = isCautious ? Color.black : Color.white;
            GUI.Label(cautiousRect, "Cautious", _buttonStyle);

            if (Event.current.type == EventType.MouseDown && cautiousRect.Contains(Event.current.mousePosition))
            {
                SetTravelMode(TravelMode.Cautious);
                Event.current.Use();
            }

            // Reckless button
            bool isReckless = SelectedTravelMode == TravelMode.Reckless;
            Rect recklessRect = new Rect(x + halfWidth + 8f, yOff, halfWidth, btnHeight);

            GUI.color = isReckless ? _optionActiveColor : _optionBtnColor;
            GUI.DrawTexture(recklessRect, _pixelTex);

            GUI.color = isReckless ? Color.black : Color.white;
            GUI.Label(recklessRect, "Reckless", _buttonStyle);

            if (Event.current.type == EventType.MouseDown && recklessRect.Contains(Event.current.mousePosition))
            {
                SetTravelMode(TravelMode.Reckless);
                Event.current.Use();
            }

            return yOff + btnHeight;
        }

        private float DrawRestPreferenceToggle(float x, float yOff, float width)
        {
            GUI.color = new Color(0.6f, 0.6f, 0.6f, 1f);
            GUI.Label(new Rect(x, yOff, width, 18f), "Rest Preference:", _labelStyle);
            yOff += 22f;

            float halfWidth = (width - 8f) * 0.5f;
            float btnHeight = 40f;

            // Inn button
            bool isInn = SelectedRestPreference == RestPreference.Inn;
            Rect innRect = new Rect(x, yOff, halfWidth, btnHeight);

            GUI.color = isInn ? _optionActiveColor : _optionBtnColor;
            GUI.DrawTexture(innRect, _pixelTex);

            GUI.color = isInn ? Color.black : Color.white;
            GUI.Label(innRect, "Inns", _buttonStyle);

            if (Event.current.type == EventType.MouseDown && innRect.Contains(Event.current.mousePosition))
            {
                SetRestPreference(RestPreference.Inn);
                Event.current.Use();
            }

            // Camp button
            bool isCamp = SelectedRestPreference == RestPreference.Camp;
            Rect campRect = new Rect(x + halfWidth + 8f, yOff, halfWidth, btnHeight);

            GUI.color = isCamp ? _optionActiveColor : _optionBtnColor;
            GUI.DrawTexture(campRect, _pixelTex);

            GUI.color = isCamp ? Color.black : Color.white;
            GUI.Label(campRect, "Camping", _buttonStyle);

            if (Event.current.type == EventType.MouseDown && campRect.Contains(Event.current.mousePosition))
            {
                SetRestPreference(RestPreference.Camp);
                Event.current.Use();
            }

            return yOff + btnHeight;
        }

        private void DrawActionButtons(float x, float yOff, float width)
        {
            float halfWidth = (width - 8f) * 0.5f;
            float btnHeight = 44f;

            // Cancel button
            Rect cancelRect = new Rect(x, yOff, halfWidth, btnHeight);
            GUI.color = _cancelBtnColor;
            GUI.DrawTexture(cancelRect, _pixelTex);
            GUI.color = Color.white;
            GUI.Label(cancelRect, "Cancel", _buttonStyle);

            if (Event.current.type == EventType.MouseDown && cancelRect.Contains(Event.current.mousePosition))
            {
                CancelSelection();
                Event.current.Use();
            }

            // Travel button
            Rect travelRect = new Rect(x + halfWidth + 8f, yOff, halfWidth, btnHeight);
            GUI.color = _travelBtnColor;
            GUI.DrawTexture(travelRect, _pixelTex);
            GUI.color = Color.white;

            var travelBtnStyle = new GUIStyle(_buttonStyle)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 14
            };
            GUI.Label(travelRect, "Travel", travelBtnStyle);

            if (Event.current.type == EventType.MouseDown && travelRect.Contains(Event.current.mousePosition))
            {
                ConfirmTravel();
                Event.current.Use();
            }
        }

        private void DrawDottedLine(Vector2 from, Vector2 to, Color color, float dotSize, float gapSize)
        {
            GUI.color = color;
            Vector2 dir = (to - from).normalized;
            float totalDist = Vector2.Distance(from, to);
            float traveled = 0f;
            bool draw = true;

            while (traveled < totalDist)
            {
                float segLen = draw ? dotSize : gapSize;
                segLen = Mathf.Min(segLen, totalDist - traveled);

                if (draw)
                {
                    Vector2 start = from + dir * traveled;
                    GUI.DrawTexture(new Rect(start.x - 1f, start.y - 1f, 2f, 2f), _pixelTex);
                }

                traveled += segLen;
                draw = !draw;
            }

            GUI.color = Color.white;
        }

        #endregion

        #region Input

        private void HandleMapInput()
        {
            int touchCount = Input.touchCount;

            // Do not handle map panning/zooming when bottom sheet is fully visible
            // (touches in sheet area should not affect the map)

            if (touchCount >= 2)
            {
                HandlePinchZoom();
                return;
            }

            _isPinching = false;

            if (touchCount == 1)
            {
                HandleSingleTouch(Input.GetTouch(0));
            }
            else
            {
                _panTouchId = -1;
            }
        }

        private void HandlePinchZoom()
        {
            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);

            float dist = Vector2.Distance(t0.position, t1.position);

            if (!_isPinching)
            {
                _isPinching = true;
                _lastPinchDistance = dist;
                _panTouchId = -1;
                return;
            }

            float delta = dist - _lastPinchDistance;
            _targetZoom = Mathf.Clamp(_targetZoom + delta * _pinchZoomSensitivity, MinZoom, MaxZoom);
            _lastPinchDistance = dist;
        }

        private void HandleSingleTouch(Touch touch)
        {
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    _panTouchId = touch.fingerId;
                    _panPrevPos = touch.position;
                    break;

                case TouchPhase.Moved:
                    if (touch.fingerId == _panTouchId)
                    {
                        Vector2 delta = touch.position - _panPrevPos;
                        _targetPanOffset += new Vector2(delta.x, -delta.y);
                        _panPrevPos = touch.position;
                    }
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if (touch.fingerId == _panTouchId)
                        _panTouchId = -1;
                    break;
            }
        }

        private void SmoothZoomAndPan()
        {
            _currentZoom = Mathf.Lerp(_currentZoom, _targetZoom, Time.deltaTime * ZoomSmoothSpeed);
            _panOffset = Vector2.Lerp(_panOffset, _targetPanOffset, Time.deltaTime * PanSmoothSpeed);
        }

        #endregion

        #region Location Selection

        private void SelectLocation(int index)
        {
            if (_regionLocations == null || index < 0 || index >= _regionLocations.Length)
                return;

            LocationEntry loc = _regionLocations[index];

            if (!loc.Discovered)
            {
                Debug.Log($"{LogPrefix} Cannot travel to undiscovered location.");
                return;
            }

            SelectedLocationName = loc.Name;
            _hasSelectedLocation = true;
            _sheetTargetVisible = true;
            _selectedLocationScreenPos = NormalizedToScreen(loc.NormalizedPosition);

            // Load full location data
            LoadSelectedLocationData(loc.RegionIndex, loc.LocationIndex);
            RecalculateTravelTime();

            Debug.Log($"{LogPrefix} Location selected: {loc.Name}. Est. travel: {EstimatedTravelDays} days.");
        }

        private void SelectLocationByName(string name)
        {
            if (_regionLocations == null || string.IsNullOrEmpty(name))
                return;

            for (int i = 0; i < _regionLocations.Length; i++)
            {
                if (string.Equals(_regionLocations[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    SelectLocation(i);
                    return;
                }
            }

            Debug.LogWarning($"{LogPrefix} Location '{name}' not found in current region.");
        }

        private void SetTravelMode(TravelMode mode)
        {
            SelectedTravelMode = mode;
            RecalculateTravelTime();
            Debug.Log($"{LogPrefix} Travel mode set to: {mode}");
        }

        private void SetRestPreference(RestPreference pref)
        {
            SelectedRestPreference = pref;
            RecalculateTravelTime();
            Debug.Log($"{LogPrefix} Rest preference set to: {pref}");
        }

        #endregion

        #region DFU Integration

        private void LoadMapTexture()
        {
            _mapTexture = DaggerfallUI.GetTextureFromResources("TRAV0I00.IMG") as Texture2D;

            if (_mapTexture == null)
            {
                Debug.LogWarning($"{LogPrefix} Travel map texture not found.");
            }
        }

        private void LoadRegionLocations()
        {
            if (GameManager.Instance == null || GameManager.Instance.PlayerGPS == null)
            {
                _regionLocations = Array.Empty<LocationEntry>();
                return;
            }

            PlayerGPS gps = GameManager.Instance.PlayerGPS;
            int regionIndex = gps.CurrentRegionIndex;

            if (_locationsLoaded && _loadedRegionIndex == regionIndex)
                return;

            _locationsLoaded = true;
            _loadedRegionIndex = regionIndex;

            if (DaggerfallUnity.Instance == null || DaggerfallUnity.Instance.ContentReader == null)
            {
                _regionLocations = Array.Empty<LocationEntry>();
                return;
            }

            DFRegion region = DaggerfallUnity.Instance.ContentReader.MapFileReader.GetRegion(regionIndex);
            var locations = new System.Collections.Generic.List<LocationEntry>();

            for (int i = 0; i < region.LocationCount; i++)
            {
                DFLocation location;
                if (!DaggerfallUnity.Instance.ContentReader.GetLocation(regionIndex, i, out location))
                    continue;

                float nx = Mathf.InverseLerp(0, 1000, region.MapTable[i].Longitude >> 8);
                float ny = Mathf.InverseLerp(0, 500, region.MapTable[i].Latitude >> 8);

                bool discovered = gps.HasDiscoveredLocation(location.MapTableData.MapId);

                locations.Add(new LocationEntry
                {
                    Name = location.Name,
                    NormalizedPosition = new Vector2(nx, ny),
                    Discovered = discovered,
                    LocationType = location.MapTableData.LocationType,
                    RegionIndex = regionIndex,
                    LocationIndex = i
                });
            }

            _regionLocations = locations.ToArray();
            Debug.Log($"{LogPrefix} Loaded {_regionLocations.Length} locations for region {regionIndex}.");
        }

        private void LoadSelectedLocationData(int regionIndex, int locationIndex)
        {
            if (DaggerfallUnity.Instance == null || DaggerfallUnity.Instance.ContentReader == null)
                return;

            DFLocation location;
            if (DaggerfallUnity.Instance.ContentReader.GetLocation(regionIndex, locationIndex, out location))
            {
                _selectedLocation = location;
            }
        }

        private void RecalculateTravelTime()
        {
            if (!_hasSelectedLocation || GameManager.Instance == null)
            {
                EstimatedTravelDays = 0;
                return;
            }

            PlayerGPS gps = GameManager.Instance.PlayerGPS;
            if (gps == null)
            {
                EstimatedTravelDays = 0;
                return;
            }

            // Use DFU's travel time calculator
            TravelTimeCalculator calculator = new TravelTimeCalculator();
            bool hasShip = GameManager.Instance.PlayerEntity.Items.Contains(ItemGroups.Transportation, (int)Transportation.Small_cart);
            bool isCautious = SelectedTravelMode == TravelMode.Cautious;
            bool useInns = SelectedRestPreference == RestPreference.Inn;

            // Approximate endpoint from selected location data
            DFPosition endPos = MapsFile.LongitudeLatitudeToMapPixel(
                (int)_selectedLocation.MapTableData.Longitude,
                (int)_selectedLocation.MapTableData.Latitude
            );

            int travelTimeTotalMins = calculator.CalculateTravelTime(
                gps.CurrentMapPixel,
                endPos,
                isCautious,
                hasShip,
                false, // hasHorse — simplified
                false  // hasCart — simplified
            );

            // Convert minutes to days (DFU uses ~1440 minutes per day)
            EstimatedTravelDays = Mathf.Max(1, Mathf.CeilToInt(travelTimeTotalMins / 1440f));

            // Cautious mode adds ~50% more time
            if (isCautious)
            {
                EstimatedTravelDays = Mathf.CeilToInt(EstimatedTravelDays * 1.0f); // Already factored
            }
        }

        private void BeginDfuTravel()
        {
            if (GameManager.Instance == null)
                return;

            // Create travel parameters matching DFU's travel popup expectations
            bool isCautious = SelectedTravelMode == TravelMode.Cautious;
            bool useInns = SelectedRestPreference == RestPreference.Inn;

            // Hook into DFU's fast travel system
            DFPosition endPos = MapsFile.LongitudeLatitudeToMapPixel(
                (int)_selectedLocation.MapTableData.Longitude,
                (int)_selectedLocation.MapTableData.Latitude
            );

            TravelTimeCalculator calculator = new TravelTimeCalculator();
            int travelTimeTotalMins = calculator.CalculateTravelTime(
                GameManager.Instance.PlayerGPS.CurrentMapPixel,
                endPos,
                isCautious,
                false, false, false
            );

            // Begin the actual travel through DFU's travel manager
            DaggerfallUI.Instance.DfTravelMapWindow.BeginTravel(
                _selectedLocation,
                isCautious,
                useInns
            );

            Debug.Log($"{LogPrefix} DFU travel initiated to {SelectedLocationName}. " +
                       $"Time: {travelTimeTotalMins} mins, Cautious: {isCautious}, Inns: {useInns}");
        }

        #endregion

        #region Coordinate Helpers

        private Rect CalculateMapRect()
        {
            if (_mapTexture == null)
                return _safeArea;

            float aspect = (float)_mapTexture.width / _mapTexture.height;
            float drawWidth = _safeArea.width * _currentZoom;
            float drawHeight = drawWidth / aspect;

            if (drawHeight < (_safeArea.height - 60f) * _currentZoom)
            {
                drawHeight = (_safeArea.height - 60f) * _currentZoom;
                drawWidth = drawHeight * aspect;
            }

            float drawX = _safeArea.center.x - drawWidth * 0.5f + _panOffset.x;
            float drawY = _safeArea.center.y - drawHeight * 0.5f + _panOffset.y;

            return new Rect(drawX, drawY, drawWidth, drawHeight);
        }

        private Vector2 NormalizedToScreen(Vector2 norm)
        {
            Rect mapRect = CalculateMapRect();
            return new Vector2(
                mapRect.x + norm.x * mapRect.width,
                mapRect.y + norm.y * mapRect.height
            );
        }

        private Vector2 GetPlayerNormalizedPosition()
        {
            if (GameManager.Instance == null || GameManager.Instance.PlayerGPS == null)
                return new Vector2(0.5f, 0.5f);

            PlayerGPS gps = GameManager.Instance.PlayerGPS;
            float nx = Mathf.InverseLerp(0, 1000, gps.CurrentMapPixel.X % 1000);
            float ny = Mathf.InverseLerp(0, 500, gps.CurrentMapPixel.Y % 500);
            return new Vector2(nx, ny);
        }

        private float GetMarkerSize(DFRegion.LocationTypes locationType)
        {
            float baseSize = 5f * Mathf.Clamp(_currentZoom * 0.5f, 0.8f, 2f);

            switch (locationType)
            {
                case DFRegion.LocationTypes.TownCity:
                    return baseSize * 1.6f;
                case DFRegion.LocationTypes.TownHamlet:
                    return baseSize * 1.3f;
                case DFRegion.LocationTypes.TownVillage:
                    return baseSize * 1.1f;
                case DFRegion.LocationTypes.DungeonLabyrinth:
                case DFRegion.LocationTypes.DungeonKeep:
                case DFRegion.LocationTypes.DungeonRuin:
                    return baseSize * 1.0f;
                default:
                    return baseSize * 0.8f;
            }
        }

        #endregion

        #region HUD Management

        private void HideHud()
        {
            if (_portraitHUD == null)
            {
                _portraitHUD = FindObjectOfType<UI.Portrait.PortraitHUD>();
            }

            if (_portraitHUD != null)
            {
                _hudWasVisible = _portraitHUD.IsVisible;
                _portraitHUD.Hide();
            }
        }

        private void RestoreHud()
        {
            if (_portraitHUD != null && _hudWasVisible)
            {
                _portraitHUD.Show();
            }
        }

        #endregion

        #region Utility

        private void AnimateBottomSheet()
        {
            float target = _sheetTargetVisible ? 1f : 0f;
            _sheetT = Mathf.MoveTowards(_sheetT, target, Time.deltaTime * BottomSheetAnimSpeed);
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
            _sheetHeight = _sheetHeightDp * dpScale;
        }

        private void EnsureStyles()
        {
            if (_stylesInitialized)
                return;

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f, 1f) }
            };

            _valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = Color.white }
            };

            _buttonStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            _markerLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };

            _stylesInitialized = true;
        }

        #endregion
    }
}
