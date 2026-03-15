using System.Collections.Generic;
using UnityEngine;
using ProjectLegacy.Core;

namespace ProjectLegacy.Input
{
    /// <summary>
    /// Divides the screen into Look and Move zones and routes each touch
    /// to the appropriate input handler. Supports simultaneous two-finger
    /// input across zones.
    /// </summary>
    public class TouchZoneManager : MonoBehaviour
    {
        private const string LogPrefix = "[ProjectLegacy]";

        [Header("Zone Handlers")]
        [SerializeField]
        [Tooltip("Handler for touch input in the look zone. Auto-created if not assigned.")]
        private LookHandler _lookHandler;

        [SerializeField]
        [Tooltip("Handler for touch input in the move zone. Auto-created if not assigned.")]
        private FloatingJoystick _floatingJoystick;

        [SerializeField]
        [Tooltip("Gesture recognizer for the look zone. Auto-created if not assigned.")]
        private GestureRecognizer _gestureRecognizer;

        /// <summary>The look handler instance.</summary>
        public LookHandler LookHandler => _lookHandler;

        /// <summary>The floating joystick instance.</summary>
        public FloatingJoystick Joystick => _floatingJoystick;

        /// <summary>The gesture recognizer instance.</summary>
        public GestureRecognizer GestureRecognizer => _gestureRecognizer;

        // Maps finger ID to assigned zone
        private readonly Dictionary<int, TouchZone> _fingerZoneMap = new Dictionary<int, TouchZone>();

        private void Awake()
        {
            if (_lookHandler == null)
                _lookHandler = gameObject.AddComponent<LookHandler>();

            if (_floatingJoystick == null)
                _floatingJoystick = gameObject.AddComponent<FloatingJoystick>();

            if (_gestureRecognizer == null)
                _gestureRecognizer = gameObject.AddComponent<GestureRecognizer>();
        }

        private void Update()
        {
            if (UnityEngine.Input.touchCount == 0)
            {
                _fingerZoneMap.Clear();
                return;
            }

            for (int i = 0; i < UnityEngine.Input.touchCount; i++)
            {
                Touch touch = UnityEngine.Input.GetTouch(i);
                ProcessTouch(touch);
            }
        }

        /// <summary>
        /// Determines which zone a screen position falls in.
        /// </summary>
        /// <param name="screenPosition">Position in screen coordinates.</param>
        /// <returns>The zone at the given position.</returns>
        public TouchZone GetZoneAtPosition(Vector2 screenPosition)
        {
            var layout = LegacyBootstrapper.Instance != null
                ? LegacyBootstrapper.Instance.ScreenLayout
                : null;

            if (layout == null)
            {
                // Fallback: simple 60/40 split
                float splitY = Screen.height * 0.4f;
                return screenPosition.y > splitY ? TouchZone.Look : TouchZone.Move;
            }

            if (layout.LookZoneRect.Contains(screenPosition))
                return TouchZone.Look;

            if (layout.MoveZoneRect.Contains(screenPosition))
                return TouchZone.Move;

            if (layout.TopBarRect.Contains(screenPosition))
                return TouchZone.HUD;

            if (layout.BottomBarRect.Contains(screenPosition))
                return TouchZone.HUD;

            // Default to look zone for any unassigned area
            return TouchZone.Look;
        }

        private void ProcessTouch(Touch touch)
        {
            int fingerId = touch.fingerId;

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    // Assign zone on first touch
                    TouchZone zone = GetZoneAtPosition(touch.position);
                    _fingerZoneMap[fingerId] = zone;
                    RouteTouch(touch, zone);
                    break;

                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    // Route to previously assigned zone
                    if (_fingerZoneMap.TryGetValue(fingerId, out TouchZone assignedZone))
                    {
                        RouteTouch(touch, assignedZone);
                    }
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    // Route end event and clean up
                    if (_fingerZoneMap.TryGetValue(fingerId, out TouchZone endZone))
                    {
                        RouteTouch(touch, endZone);
                        _fingerZoneMap.Remove(fingerId);
                    }
                    break;
            }
        }

        private void RouteTouch(Touch touch, TouchZone zone)
        {
            switch (zone)
            {
                case TouchZone.Look:
                    _lookHandler.ProcessTouch(touch);
                    _gestureRecognizer.ProcessTouch(touch);
                    break;

                case TouchZone.Move:
                    _floatingJoystick.ProcessTouch(touch);
                    break;

                case TouchZone.HUD:
                    // HUD touches are handled by the UI event system
                    break;
            }
        }
    }

    /// <summary>
    /// Screen zones for touch input routing.
    /// </summary>
    public enum TouchZone
    {
        /// <summary>Upper viewport area — camera look and combat gestures.</summary>
        Look,
        /// <summary>Lower viewport area — movement joystick.</summary>
        Move,
        /// <summary>HUD bars — UI interaction.</summary>
        HUD
    }
}
