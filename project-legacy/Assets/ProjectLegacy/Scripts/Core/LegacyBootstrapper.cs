using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;

namespace ProjectLegacy.Core
{
    /// <summary>
    /// Single entry point for all Project Legacy systems.
    /// Detects Android platform, activates portrait mode, and initializes
    /// all Legacy subsystems in the correct order. If this component does
    /// not run, vanilla DFU behavior is preserved.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class LegacyBootstrapper : MonoBehaviour
    {
        private const string LogPrefix = "[ProjectLegacy]";

        /// <summary>Singleton instance accessible from any Legacy system.</summary>
        public static LegacyBootstrapper Instance { get; private set; }

        [Header("Initialization")]
        [SerializeField]
        [Tooltip("Force initialization even when not running on Android (useful for editor testing).")]
        private bool _forceInitInEditor = true;

        [SerializeField]
        [Tooltip("Enable verbose logging for debugging.")]
        private bool _debugLogging = false;

        [Header("System References")]
        [SerializeField]
        [Tooltip("Portrait mode manager. Auto-created if not assigned.")]
        private PortraitModeManager _portraitModeManager;

        [SerializeField]
        [Tooltip("Screen layout resolver. Auto-created if not assigned.")]
        private ScreenLayoutResolver _screenLayoutResolver;

        /// <summary>Whether all Legacy systems have been initialized.</summary>
        public bool IsInitialized { get; private set; }

        /// <summary>The portrait mode manager instance.</summary>
        public PortraitModeManager PortraitMode => _portraitModeManager;

        /// <summary>The screen layout resolver instance.</summary>
        public ScreenLayoutResolver ScreenLayout => _screenLayoutResolver;

        /// <summary>The legacy settings instance.</summary>
        public LegacySettings Settings { get; private set; }

        private bool _daggerfallReady;
        private GameObject _systemsRoot;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"{LogPrefix} Duplicate LegacyBootstrapper detected. Destroying this instance.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (!ShouldInitialize())
            {
                Debug.Log($"{LogPrefix} Not initializing — platform check failed.");
                enabled = false;
                return;
            }

            Debug.Log($"{LogPrefix} LegacyBootstrapper initializing...");
            LogPlatformInfo();
        }

        private void Start()
        {
            InitializeSystems();
        }

        private void Update()
        {
            if (IsInitialized || _daggerfallReady)
                return;

            // Wait for DFU singletons to be ready before completing initialization
            if (GameManager.Instance != null && DaggerfallUI.Instance != null)
            {
                _daggerfallReady = true;
                OnDaggerfallReady();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Shutdown();
                Instance = null;
            }
        }

        /// <summary>
        /// Determines whether Legacy systems should initialize on this platform.
        /// </summary>
        private bool ShouldInitialize()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Debug.Log($"{LogPrefix} Platform: Android detected");
            return true;
#else
            if (_forceInitInEditor && Application.isEditor)
            {
                Debug.Log($"{LogPrefix} Platform: Editor mode — forced initialization enabled");
                return true;
            }
            return Application.platform == RuntimePlatform.Android;
#endif
        }

        /// <summary>
        /// Creates the systems root and initializes core subsystems that
        /// do not depend on DFU singletons.
        /// </summary>
        private void InitializeSystems()
        {
            _systemsRoot = new GameObject("ProjectLegacy_Systems");
            _systemsRoot.transform.SetParent(transform);

            // Load or create settings
            Settings = LegacySettings.Load();
            LogDebug("Settings loaded");

            // Screen layout resolver — needed before any UI
            if (_screenLayoutResolver == null)
            {
                _screenLayoutResolver = _systemsRoot.AddComponent<ScreenLayoutResolver>();
            }
            LogDebug("ScreenLayoutResolver initialized");

            // Portrait mode manager
            if (_portraitModeManager == null)
            {
                _portraitModeManager = _systemsRoot.AddComponent<PortraitModeManager>();
            }
            LogDebug("PortraitModeManager initialized");

            Debug.Log($"{LogPrefix} Core systems initialized. Waiting for Daggerfall Unity...");
        }

        /// <summary>
        /// Called once DFU's GameManager and UI singletons are available.
        /// Hooks into DFU systems and activates input/HUD/combat.
        /// </summary>
        private void OnDaggerfallReady()
        {
            Debug.Log($"{LogPrefix} Daggerfall Unity ready — hooking into game systems.");

            // Initialize input systems
            var inputObj = new GameObject("LegacyInput");
            inputObj.transform.SetParent(_systemsRoot.transform);
            inputObj.AddComponent<Input.TouchZoneManager>();
            LogDebug("Input systems initialized");

            // Initialize combat systems
            var combatObj = new GameObject("LegacyCombat");
            combatObj.transform.SetParent(_systemsRoot.transform);
            combatObj.AddComponent<Combat.AutoAimController>();
            combatObj.AddComponent<Combat.LockOnSystem>();
            combatObj.AddComponent<Combat.SwipeCombatHandler>();
            LogDebug("Combat systems initialized");

            // Portrait mode activation
            _portraitModeManager.ActivatePortraitMode();
            Debug.Log($"{LogPrefix} Portrait mode activated");

            IsInitialized = true;
            Debug.Log($"{LogPrefix} All systems initialized successfully.");
        }

        /// <summary>
        /// Shuts down all Legacy systems and restores vanilla DFU state.
        /// </summary>
        private void Shutdown()
        {
            Debug.Log($"{LogPrefix} Shutting down Legacy systems...");

            if (_portraitModeManager != null)
            {
                _portraitModeManager.DeactivatePortraitMode();
            }

            if (_systemsRoot != null)
            {
                Destroy(_systemsRoot);
            }

            IsInitialized = false;
            Debug.Log($"{LogPrefix} Shutdown complete.");
        }

        private void LogPlatformInfo()
        {
            if (!_debugLogging) return;

            Debug.Log($"{LogPrefix} Device: {SystemInfo.deviceModel}");
            Debug.Log($"{LogPrefix} Screen: {Screen.width}x{Screen.height} @ {Screen.dpi}dpi");
            Debug.Log($"{LogPrefix} Safe area: {Screen.safeArea}");
            Debug.Log($"{LogPrefix} GPU: {SystemInfo.graphicsDeviceName}");
            Debug.Log($"{LogPrefix} RAM: {SystemInfo.systemMemorySize}MB");
        }

        private void LogDebug(string message)
        {
            if (_debugLogging)
            {
                Debug.Log($"{LogPrefix} {message}");
            }
        }
    }
}
