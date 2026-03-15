using UnityEngine;
using DaggerfallWorkshop.Game;

namespace ProjectLegacy.Core
{
    /// <summary>
    /// Manages portrait orientation lock and camera configuration.
    /// Adjusts FOV for the narrow portrait viewport and handles
    /// weapon rendering offset to prevent viewport obstruction.
    /// </summary>
    public class PortraitModeManager : MonoBehaviour
    {
        private const string LogPrefix = "[ProjectLegacy]";

        [Header("Field of View")]
        [SerializeField]
        [Range(45f, 65f)]
        [Tooltip("Camera field of view in portrait mode. Lower values feel more zoomed in.")]
        private float _portraitFOV = 55f;

        [Header("Weapon Offset")]
        [SerializeField]
        [Tooltip("Vertical offset for the FPS weapon to prevent it from obscuring the narrow viewport.")]
        private Vector3 _weaponPositionOffset = new Vector3(0f, -0.05f, 0f);

        [SerializeField]
        [Tooltip("Scale adjustment for the FPS weapon in portrait mode.")]
        private float _weaponScaleMultiplier = 0.85f;

        /// <summary>Whether portrait mode is currently active.</summary>
        public bool IsActive { get; private set; }

        /// <summary>The configured portrait FOV.</summary>
        public float PortraitFOV => _portraitFOV;

        private float _originalFOV;
        private ScreenOrientation _originalOrientation;
        private bool _originalAutoRotation;
        private Rect _lastSafeArea;

        /// <summary>
        /// Activates portrait mode: locks orientation, adjusts FOV, and
        /// modifies weapon rendering for the portrait viewport.
        /// </summary>
        public void ActivatePortraitMode()
        {
            if (IsActive)
                return;

            CacheOriginalSettings();
            ApplyPortraitOrientation();
            ApplyPortraitFOV();
            ApplyWeaponAdjustments();

            _lastSafeArea = Screen.safeArea;
            IsActive = true;

            Debug.Log($"{LogPrefix} Portrait mode activated — FOV: {_portraitFOV}°");
        }

        /// <summary>
        /// Deactivates portrait mode and restores original settings.
        /// </summary>
        public void DeactivatePortraitMode()
        {
            if (!IsActive)
                return;

            RestoreOriginalSettings();
            IsActive = false;

            Debug.Log($"{LogPrefix} Portrait mode deactivated — original settings restored.");
        }

        /// <summary>
        /// Updates the portrait FOV at runtime.
        /// </summary>
        /// <param name="fov">New FOV value (clamped to 45–65 range).</param>
        public void SetFOV(float fov)
        {
            _portraitFOV = Mathf.Clamp(fov, 45f, 65f);
            if (IsActive)
            {
                ApplyPortraitFOV();
            }
        }

        private void LateUpdate()
        {
            if (!IsActive)
                return;

            // Prevent DFU from resetting FOV
            var mainCamera = GameManager.Instance != null ? GameManager.Instance.MainCamera : Camera.main;
            if (mainCamera != null && !Mathf.Approximately(mainCamera.fieldOfView, _portraitFOV))
            {
                mainCamera.fieldOfView = _portraitFOV;
            }

            // Detect safe area changes (shouldn't happen in portrait lock, but defensive)
            if (_lastSafeArea != Screen.safeArea)
            {
                _lastSafeArea = Screen.safeArea;
                OnSafeAreaChanged();
            }
        }

        private void OnDisable()
        {
            if (IsActive)
            {
                DeactivatePortraitMode();
            }
        }

        private void CacheOriginalSettings()
        {
            _originalOrientation = Screen.orientation;
            _originalAutoRotation = Screen.autorotateToPortrait;

            var mainCamera = GameManager.Instance != null ? GameManager.Instance.MainCamera : Camera.main;
            if (mainCamera != null)
            {
                _originalFOV = mainCamera.fieldOfView;
            }
        }

        private void RestoreOriginalSettings()
        {
            Screen.orientation = _originalOrientation;
            Screen.autorotateToPortrait = _originalAutoRotation;

            var mainCamera = GameManager.Instance != null ? GameManager.Instance.MainCamera : Camera.main;
            if (mainCamera != null)
            {
                mainCamera.fieldOfView = _originalFOV;
            }
        }

        private void ApplyPortraitOrientation()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;

            Debug.Log($"{LogPrefix} Orientation locked to Portrait");
        }

        private void ApplyPortraitFOV()
        {
            var mainCamera = GameManager.Instance != null ? GameManager.Instance.MainCamera : Camera.main;
            if (mainCamera != null)
            {
                mainCamera.fieldOfView = _portraitFOV;
                Debug.Log($"{LogPrefix} Camera FOV set to {_portraitFOV}°");
            }
            else
            {
                Debug.LogWarning($"{LogPrefix} Main camera not found — FOV will be applied when available.");
            }
        }

        private void ApplyWeaponAdjustments()
        {
            if (GameManager.Instance == null || GameManager.Instance.WeaponManager == null)
                return;

            // REQUIRES DFU PATCH: WeaponManager may need a public accessor for the
            // FPS weapon transform to apply position/scale offsets cleanly. For now,
            // we attempt to find it via the known hierarchy.
            var weaponManager = GameManager.Instance.WeaponManager;
            var screenWeapon = weaponManager.ScreenWeapon;
            if (screenWeapon != null)
            {
                var weaponTransform = screenWeapon.transform;
                weaponTransform.localPosition += _weaponPositionOffset;
                weaponTransform.localScale *= _weaponScaleMultiplier;
            }
        }

        private void OnSafeAreaChanged()
        {
            Debug.Log($"{LogPrefix} Safe area changed: {Screen.safeArea}");
            // Notify layout resolver to recalculate
            if (LegacyBootstrapper.Instance != null && LegacyBootstrapper.Instance.ScreenLayout != null)
            {
                LegacyBootstrapper.Instance.ScreenLayout.Recalculate();
            }
        }
    }
}
