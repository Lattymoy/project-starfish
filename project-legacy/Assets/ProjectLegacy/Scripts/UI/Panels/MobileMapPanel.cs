using System;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Utility;
using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using ProjectLegacy.Core;

namespace ProjectLegacy.UI.Panels
{
    /// <summary>
    /// Full-screen map panel for mobile devices supporting pinch-to-zoom, pan-to-scroll,
    /// and toggleable map modes (local automap vs. travel map). Location markers are rendered
    /// as tappable targets. The HUD is hidden while this panel is active. Delegates to
    /// <see cref="MobileTravelMap"/> when the user taps a location on the travel map.
    /// </summary>
    public class MobileMapPanel : MonoBehaviour
    {
        private const string LogPrefix = "[ProjectLegacy]";
        private const float MinZoom = 0.5f;
        private const float MaxZoom = 6f;
        private const float ZoomSmoothSpeed = 8f;
        private const float PanSmoothSpeed = 10f;
        private const float MarkerTapRadius = 24f;

        #region Serialized Fields

        [Header("Map Mode")]
        [SerializeField]
        [Tooltip("Initial map mode when the panel opens.")]
        private MapMode _defaultMode = MapMode.LocalAutomap;

        [Header("Zoom")]
        [SerializeField]
        [Range(0.01f, 0.1f)]
        [Tooltip("Pinch zoom sensitivity multiplier.")]
        private float _pinchZoomSensitivity = 0.04f;

        [SerializeField]
        [Tooltip("Zoom level when the panel first opens.")]
        private float _initialZoom = 1f;

        [Header("Colors")]
        [SerializeField]
        [Tooltip("Background color behind the map.")]
        private Color _backgroundColor = new Color(0.02f, 0.02f, 0.04f, 1f);

        [SerializeField]
        [Tooltip("Map mode toggle button color.")]
        private Color _toggleBtnColor = new Color(0.15f, 0.15f, 0.2f, 0.9f);

        [SerializeField]
        [Tooltip("Active toggle button color.")]
        private Color _toggleActiveColor = new Color(0.85f, 0.72f, 0.35f, 1f);

        [SerializeField]
        [Tooltip("Location marker default color.")]
        private Color _markerColor = new Color(0.9f, 0.8f, 0.3f, 1f);

        [SerializeField]
        [Tooltip("Discovered location marker color.")]
        private Color _discoveredMarkerColor = new Color(0.9f, 0.8f, 0.3f, 1f);

        [SerializeField]
        [Tooltip("Undiscovered location marker color.")]
        private Color _undiscoveredMarkerColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);

        [SerializeField]
        [Tooltip("Player position marker color.")]
        private Color _playerMarkerColor = new Color(0.2f, 0.6f, 0.9f, 1f);

        [SerializeField]
        [Tooltip("Close button color.")]
        private Color _closeBtnColor = new Color(0.8f, 0.3f, 0.3f, 1f);

        [Header("References")]
        [SerializeField]
        [Tooltip("Reference to the travel map panel for location taps.")]
        private MobileTravelMap _travelMapPanel;

        #endregion

        #region Enums

        /// <summary>
        /// Available map display modes.
        /// </summary>
        public enum MapMode
        {
            /// <summary>Local dungeon/building automap.</summary>
            LocalAutomap,
            /// <summary>Overworld travel map showing regions and locations.</summary>
            TravelMap
        }

        #endregion

        #region Public Properties

        /// <summary>Whether the map panel is currently visible.</summary>
        public bool IsVisible { get; private set; }

        /// <summary>The current map display mode.</summary>
        public MapMode CurrentMode { get; private set; }

        /// <summary>The current zoom level.</summary>
        public float ZoomLevel => _currentZoom;

        /// <summary>Fired when the panel is shown.</summary>
        public event Action OnPanelShown;

        /// <summary>Fired when the panel is hidden.</summary>
        public event Action OnPanelHidden;

        /// <summary>Fired when the map mode changes. Parameter: new mode.</summary>
        public event Action<MapMode> OnModeChanged;

        /// <summary>Fired when a location marker is tapped. Parameter: location name.</summary>
        public event Action<string> OnLocationTapped;

        #endregion

        #region Private State

        // Zoom and pan
        private float _currentZoom;
        private float _targetZoom;
        private Vector2 _panOffset;
        private Vector2 _targetPanOffset;

        // Pinch tracking
        private float _lastPinchDistance;
        private bool _isPinching;

        // Single-finger pan tracking
        private int _panTouchId = -1;
        private Vector2 _panTouchPrev;

        // Map textures (fetched from DFU systems)
        private Texture2D _automapTexture;
        private Texture2D _travelMapTexture;
        private Texture2D _pixelTex;

        // Location markers for travel map
        private LocationMarker[] _locationMarkers;
        private bool _markersLoaded;

        // HUD reference for hiding/showing
        private UI.Portrait.PortraitHUD _portraitHUD;
        private bool _hudWasVisible;

        // Styles and layout
        private GUIStyle _buttonStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _markerLabelStyle;
        private bool _stylesInitialized;
        private Rect _safeArea;
        private int _lastScreenWidth;
        private int _lastScreenHeight;

        #endregion

        #region Nested Types

        private struct LocationMarker
        {
            public string Name;
            public Vector2 NormalizedPosition; // 0-1 range on the map
            public bool Discovered;
            public DFRegion.LocationTypes LocationType;
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

            HandleTouchInput();
            SmoothZoomAndPan();
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
            DrawMapContent();
            DrawPlayerMarker();
            DrawLocationMarkers();
            DrawModeToggle();
            DrawZoomIndicator();
            DrawCloseButton();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Shows the map panel, hiding the HUD and pausing game input.
        /// </summary>
        public void Show()
        {
            if (IsVisible)
                return;

            IsVisible = true;
            CurrentMode = _defaultMode;
            _currentZoom = _initialZoom;
            _targetZoom = _initialZoom;
            _panOffset = Vector2.zero;
            _targetPanOffset = Vector2.zero;
            _markersLoaded = false;

            HideHud();
            SuppressGameInput(true);
            LoadMapData();

            OnPanelShown?.Invoke();
            Debug.Log($"{LogPrefix} MobileMapPanel shown. Mode: {CurrentMode}");
        }

        /// <summary>
        /// Hides the map panel, restoring HUD visibility and game input.
        /// </summary>
        public void Hide()
        {
            if (!IsVisible)
                return;

            IsVisible = false;

            RestoreHud();
            SuppressGameInput(false);

            OnPanelHidden?.Invoke();
            Debug.Log($"{LogPrefix} MobileMapPanel hidden.");
        }

        /// <summary>
        /// Switches between local automap and travel map modes.
        /// </summary>
        /// <param name="mode">The desired map mode.</param>
        public void SetMode(MapMode mode)
        {
            if (CurrentMode == mode)
                return;

            CurrentMode = mode;
            _currentZoom = _initialZoom;
            _targetZoom = _initialZoom;
            _panOffset = Vector2.zero;
            _targetPanOffset = Vector2.zero;
            _markersLoaded = false;

            LoadMapData();

            OnModeChanged?.Invoke(mode);
            Debug.Log($"{LogPrefix} Map mode changed to: {mode}");
        }

        /// <summary>
        /// Toggles between the two available map modes.
        /// </summary>
        public void ToggleMode()
        {
            SetMode(CurrentMode == MapMode.LocalAutomap ? MapMode.TravelMap : MapMode.LocalAutomap);
        }

        #endregion

        #region Drawing

        private void DrawBackground()
        {
            GUI.color = _backgroundColor;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _pixelTex);
            GUI.color = Color.white;
        }

        private void DrawMapContent()
        {
            Texture2D mapTex = CurrentMode == MapMode.LocalAutomap ? _automapTexture : _travelMapTexture;
            if (mapTex == null)
            {
                // Placeholder when no map texture available
                GUI.color = new Color(0.15f, 0.15f, 0.18f, 1f);
                Rect placeholder = new Rect(
                    _safeArea.x + 20f, _safeArea.y + 60f,
                    _safeArea.width - 40f, _safeArea.height - 120f
                );
                GUI.DrawTexture(placeholder, _pixelTex);

                GUI.color = new Color(0.5f, 0.5f, 0.5f, 1f);
                string modeLabel = CurrentMode == MapMode.LocalAutomap ? "Local Automap" : "Travel Map";
                GUI.Label(
                    new Rect(placeholder.x, placeholder.center.y - 12f, placeholder.width, 24f),
                    modeLabel + " — No data available",
                    _labelStyle
                );
                GUI.color = Color.white;
                return;
            }

            // Calculate zoomed and panned map rect
            float baseWidth = _safeArea.width;
            float baseHeight = _safeArea.height - 100f; // Reserve space for controls
            float aspect = (float)mapTex.width / mapTex.height;

            float drawWidth = baseWidth * _currentZoom;
            float drawHeight = drawWidth / aspect;
            if (drawHeight < baseHeight * _currentZoom)
            {
                drawHeight = baseHeight * _currentZoom;
                drawWidth = drawHeight * aspect;
            }

            float drawX = _safeArea.center.x - drawWidth * 0.5f + _panOffset.x;
            float drawY = _safeArea.center.y - drawHeight * 0.5f + _panOffset.y;

            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(drawX, drawY, drawWidth, drawHeight), mapTex);
        }

        private void DrawPlayerMarker()
        {
            if (CurrentMode != MapMode.TravelMap)
                return;

            // Get player world position and map to screen
            Vector2 playerNorm = GetPlayerNormalizedMapPosition();
            Vector2 screenPos = MapNormalizedToScreen(playerNorm);

            float markerSize = 12f;
            Rect markerRect = new Rect(
                screenPos.x - markerSize * 0.5f,
                screenPos.y - markerSize * 0.5f,
                markerSize, markerSize
            );

            // Pulsing effect
            float pulse = 0.7f + Mathf.PingPong(Time.time * 2f, 0.3f);
            GUI.color = _playerMarkerColor * pulse;
            GUI.DrawTexture(markerRect, _pixelTex);

            // Direction indicator (small line)
            GUI.color = _playerMarkerColor;
            GUI.DrawTexture(new Rect(screenPos.x - 1f, screenPos.y - markerSize, 2f, markerSize * 0.5f), _pixelTex);

            GUI.color = Color.white;
        }

        private void DrawLocationMarkers()
        {
            if (CurrentMode != MapMode.TravelMap || _locationMarkers == null)
                return;

            for (int i = 0; i < _locationMarkers.Length; i++)
            {
                LocationMarker marker = _locationMarkers[i];
                Vector2 screenPos = MapNormalizedToScreen(marker.NormalizedPosition);

                // Cull markers outside viewport
                if (screenPos.x < _safeArea.x - MarkerTapRadius ||
                    screenPos.x > _safeArea.xMax + MarkerTapRadius ||
                    screenPos.y < _safeArea.y - MarkerTapRadius ||
                    screenPos.y > _safeArea.yMax + MarkerTapRadius)
                    continue;

                float markerSize = 6f * Mathf.Clamp(_currentZoom * 0.5f, 0.8f, 2f);
                Rect markerRect = new Rect(
                    screenPos.x - markerSize * 0.5f,
                    screenPos.y - markerSize * 0.5f,
                    markerSize, markerSize
                );

                GUI.color = marker.Discovered ? _discoveredMarkerColor : _undiscoveredMarkerColor;
                GUI.DrawTexture(markerRect, _pixelTex);

                // Show label at higher zoom levels for discovered locations
                if (marker.Discovered && _currentZoom >= 2f)
                {
                    GUI.color = Color.white;
                    Rect labelRect = new Rect(screenPos.x + markerSize, screenPos.y - 8f, 120f, 16f);
                    GUI.Label(labelRect, marker.Name, _markerLabelStyle);
                }

                // Tap detection
                Rect tapRect = new Rect(
                    screenPos.x - MarkerTapRadius,
                    screenPos.y - MarkerTapRadius,
                    MarkerTapRadius * 2f,
                    MarkerTapRadius * 2f
                );

                if (Event.current.type == EventType.MouseDown && tapRect.Contains(Event.current.mousePosition))
                {
                    HandleLocationTap(marker);
                    Event.current.Use();
                }
            }

            GUI.color = Color.white;
        }

        private void DrawModeToggle()
        {
            float btnWidth = 120f;
            float btnHeight = 36f;
            float btnY = _safeArea.y + 8f;

            // Local map button
            Rect localBtn = new Rect(_safeArea.center.x - btnWidth - 2f, btnY, btnWidth, btnHeight);
            bool isLocal = CurrentMode == MapMode.LocalAutomap;

            GUI.color = isLocal ? _toggleActiveColor : _toggleBtnColor;
            GUI.DrawTexture(localBtn, _pixelTex);
            GUI.color = isLocal ? Color.black : Color.white;
            GUI.Label(localBtn, "Local Map", _buttonStyle);

            if (Event.current.type == EventType.MouseDown && localBtn.Contains(Event.current.mousePosition))
            {
                SetMode(MapMode.LocalAutomap);
                Event.current.Use();
            }

            // Travel map button
            Rect travelBtn = new Rect(_safeArea.center.x + 2f, btnY, btnWidth, btnHeight);
            bool isTravel = CurrentMode == MapMode.TravelMap;

            GUI.color = isTravel ? _toggleActiveColor : _toggleBtnColor;
            GUI.DrawTexture(travelBtn, _pixelTex);
            GUI.color = isTravel ? Color.black : Color.white;
            GUI.Label(travelBtn, "Travel Map", _buttonStyle);

            if (Event.current.type == EventType.MouseDown && travelBtn.Contains(Event.current.mousePosition))
            {
                SetMode(MapMode.TravelMap);
                Event.current.Use();
            }

            GUI.color = Color.white;
        }

        private void DrawZoomIndicator()
        {
            float indicatorWidth = 60f;
            float indicatorHeight = 20f;
            Rect indicatorRect = new Rect(
                _safeArea.xMax - indicatorWidth - 8f,
                _safeArea.y + 12f,
                indicatorWidth, indicatorHeight
            );

            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(indicatorRect, _pixelTex);

            GUI.color = Color.white;
            GUI.Label(indicatorRect, $"{_currentZoom:F1}x", _buttonStyle);
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

        #endregion

        #region Input Handling

        private void HandleTouchInput()
        {
            int touchCount = Input.touchCount;

            if (touchCount >= 2)
            {
                HandlePinchZoom();
                return;
            }

            _isPinching = false;

            if (touchCount == 1)
            {
                HandleSingleFingerPan(Input.GetTouch(0));
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

            float currentDistance = Vector2.Distance(t0.position, t1.position);

            if (!_isPinching)
            {
                _isPinching = true;
                _lastPinchDistance = currentDistance;
                _panTouchId = -1; // Cancel pan when pinching starts
                return;
            }

            float delta = currentDistance - _lastPinchDistance;
            _targetZoom = Mathf.Clamp(_targetZoom + delta * _pinchZoomSensitivity, MinZoom, MaxZoom);
            _lastPinchDistance = currentDistance;
        }

        private void HandleSingleFingerPan(Touch touch)
        {
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    _panTouchId = touch.fingerId;
                    _panTouchPrev = touch.position;
                    break;

                case TouchPhase.Moved:
                    if (touch.fingerId == _panTouchId)
                    {
                        Vector2 delta = touch.position - _panTouchPrev;
                        // In GUI coordinates, Y is flipped
                        _targetPanOffset += new Vector2(delta.x, -delta.y);
                        _panTouchPrev = touch.position;
                    }
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if (touch.fingerId == _panTouchId)
                    {
                        _panTouchId = -1;
                    }
                    break;
            }
        }

        private void SmoothZoomAndPan()
        {
            _currentZoom = Mathf.Lerp(_currentZoom, _targetZoom, Time.deltaTime * ZoomSmoothSpeed);
            _panOffset = Vector2.Lerp(_panOffset, _targetPanOffset, Time.deltaTime * PanSmoothSpeed);
        }

        private void HandleLocationTap(LocationMarker marker)
        {
            if (string.IsNullOrEmpty(marker.Name))
                return;

            Debug.Log($"{LogPrefix} Location tapped: {marker.Name} (Discovered: {marker.Discovered})");
            OnLocationTapped?.Invoke(marker.Name);

            // Open travel map panel if available
            if (_travelMapPanel != null)
            {
                _travelMapPanel.ShowForLocation(marker.Name);
            }
        }

        #endregion

        #region Map Data

        private void LoadMapData()
        {
            switch (CurrentMode)
            {
                case MapMode.LocalAutomap:
                    LoadAutomapData();
                    break;
                case MapMode.TravelMap:
                    LoadTravelMapData();
                    break;
            }
        }

        private void LoadAutomapData()
        {
            // Attempt to get automap texture from DFU's automap system
            if (GameManager.Instance == null)
            {
                Debug.LogWarning($"{LogPrefix} Cannot load automap — GameManager not available.");
                return;
            }

            // The automap is rendered by DFU's Automap component
            Automap automap = GameManager.Instance.InteriorAutomap;
            if (automap != null)
            {
                _automapTexture = automap.ExteriorLayoutTexture;
                Debug.Log($"{LogPrefix} Automap texture loaded.");
            }
            else
            {
                Debug.Log($"{LogPrefix} No automap available for current location.");
                _automapTexture = null;
            }
        }

        private void LoadTravelMapData()
        {
            if (DaggerfallUnity.Instance == null)
            {
                Debug.LogWarning($"{LogPrefix} Cannot load travel map — DaggerfallUnity not available.");
                return;
            }

            // Load travel map background texture
            _travelMapTexture = DaggerfallUI.GetTextureFromResources("TRAV0I00.IMG") as Texture2D;

            if (_travelMapTexture == null)
            {
                Debug.LogWarning($"{LogPrefix} Travel map texture not found. Using placeholder.");
            }

            LoadLocationMarkers();
            Debug.Log($"{LogPrefix} Travel map loaded. Markers: {(_locationMarkers != null ? _locationMarkers.Length : 0)}");
        }

        private void LoadLocationMarkers()
        {
            if (_markersLoaded)
                return;

            _markersLoaded = true;

            if (DaggerfallUnity.Instance == null || DaggerfallUnity.Instance.ContentReader == null)
            {
                _locationMarkers = Array.Empty<LocationMarker>();
                return;
            }

            // Load location data from the current region
            PlayerGPS playerGPS = GameManager.Instance != null ? GameManager.Instance.PlayerGPS : null;
            if (playerGPS == null)
            {
                _locationMarkers = Array.Empty<LocationMarker>();
                return;
            }

            int regionIndex = playerGPS.CurrentRegionIndex;
            DFRegion region = DaggerfallUnity.Instance.ContentReader.MapFileReader.GetRegion(regionIndex);

            if (region.LocationCount == 0)
            {
                _locationMarkers = Array.Empty<LocationMarker>();
                return;
            }

            var markers = new System.Collections.Generic.List<LocationMarker>();

            for (int i = 0; i < region.LocationCount; i++)
            {
                DFLocation location;
                if (!DaggerfallUnity.Instance.ContentReader.GetLocation(regionIndex, i, out location))
                    continue;

                // Normalize position within region map (approximate)
                float nx = Mathf.InverseLerp(region.MapTable[i].Longitude >> 8, 0, 1000);
                float ny = Mathf.InverseLerp(region.MapTable[i].Latitude >> 8, 0, 500);

                bool discovered = playerGPS.HasDiscoveredLocation(location.MapTableData.MapId);

                markers.Add(new LocationMarker
                {
                    Name = location.Name,
                    NormalizedPosition = new Vector2(nx, ny),
                    Discovered = discovered,
                    LocationType = location.MapTableData.LocationType
                });
            }

            _locationMarkers = markers.ToArray();
        }

        #endregion

        #region Coordinate Helpers

        private Vector2 MapNormalizedToScreen(Vector2 normalizedPos)
        {
            float mapWidth = _safeArea.width * _currentZoom;
            float mapHeight = _safeArea.height * _currentZoom;

            float baseX = _safeArea.center.x - mapWidth * 0.5f + _panOffset.x;
            float baseY = _safeArea.center.y - mapHeight * 0.5f + _panOffset.y;

            return new Vector2(
                baseX + normalizedPos.x * mapWidth,
                baseY + normalizedPos.y * mapHeight
            );
        }

        private Vector2 GetPlayerNormalizedMapPosition()
        {
            if (GameManager.Instance == null || GameManager.Instance.PlayerGPS == null)
                return new Vector2(0.5f, 0.5f);

            PlayerGPS gps = GameManager.Instance.PlayerGPS;

            // Map world coordinates to a 0-1 range within the current region
            // This is a simplified mapping; actual DFU uses pixel coordinates
            float nx = Mathf.InverseLerp(0, 1000, gps.CurrentMapPixel.X % 1000);
            float ny = Mathf.InverseLerp(0, 500, gps.CurrentMapPixel.Y % 500);

            return new Vector2(nx, ny);
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
        }

        private void EnsureStyles()
        {
            if (_stylesInitialized)
                return;

            _buttonStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f, 1f) }
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
