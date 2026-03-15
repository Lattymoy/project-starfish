using UnityEngine;
using ProjectLegacy.Core;

namespace ProjectLegacy.Util
{
    /// <summary>
    /// Detects device capabilities at startup and adjusts defaults accordingly.
    /// Categorizes the device by screen size, performance tier, and available
    /// resources, then applies appropriate settings for button sizes, render
    /// resolution, LOD distances, and effect quality.
    /// </summary>
    public class DeviceProfile : MonoBehaviour
    {
        private const string LogPrefix = "[ProjectLegacy]";

        /// <summary>Singleton instance.</summary>
        public static DeviceProfile Instance { get; private set; }

        /// <summary>Detected screen size category.</summary>
        public ScreenSizeCategory ScreenSize { get; private set; }

        /// <summary>Detected performance tier.</summary>
        public PerformanceTier Performance { get; private set; }

        /// <summary>Available system RAM in megabytes.</summary>
        public int SystemRamMB { get; private set; }

        /// <summary>GPU name.</summary>
        public string GpuName { get; private set; }

        /// <summary>Screen diagonal size in inches (estimated).</summary>
        public float ScreenDiagonalInches { get; private set; }

        /// <summary>Screen DPI.</summary>
        public float ScreenDpi { get; private set; }

        /// <summary>Recommended render resolution scale (0.5–1.0).</summary>
        public float RecommendedRenderScale { get; private set; }

        /// <summary>Recommended button size multiplier.</summary>
        public float RecommendedButtonScale { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DetectDeviceCapabilities();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Re-detects device capabilities. Useful if screen configuration changes.
        /// </summary>
        public void Refresh()
        {
            DetectDeviceCapabilities();
        }

        /// <summary>
        /// Applies recommended settings based on detected device profile.
        /// Only applies if settings haven't been manually configured.
        /// </summary>
        public void ApplyRecommendedSettings()
        {
            if (LegacyBootstrapper.Instance == null || LegacyBootstrapper.Instance.Settings == null)
                return;

            var settings = LegacyBootstrapper.Instance.Settings;

            // Only apply defaults if this is a fresh install
            if (!PlayerPrefs.HasKey("ProjectLegacy_ProfileApplied"))
            {
                settings.RenderScale = RecommendedRenderScale;

                // Adjust joystick size for screen size
                settings.JoystickMaxRadius = ScreenSize switch
                {
                    ScreenSizeCategory.Small => 60f,
                    ScreenSizeCategory.Medium => 80f,
                    ScreenSizeCategory.Large => 90f,
                    ScreenSizeCategory.Tablet => 100f,
                    _ => 80f
                };

                settings.Save();
                PlayerPrefs.SetInt("ProjectLegacy_ProfileApplied", 1);
                PlayerPrefs.Save();

                Debug.Log($"{LogPrefix} Applied recommended settings for {ScreenSize}/{Performance} device");
            }
        }

        private void DetectDeviceCapabilities()
        {
            DetectScreenProperties();
            DetectPerformanceTier();
            CalculateRecommendations();

            Debug.Log($"{LogPrefix} Device profile detected:");
            Debug.Log($"{LogPrefix}   Screen: {ScreenSize} ({ScreenDiagonalInches:F1}\", {Screen.width}x{Screen.height} @ {ScreenDpi}dpi)");
            Debug.Log($"{LogPrefix}   Performance: {Performance} (RAM: {SystemRamMB}MB, GPU: {GpuName})");
            Debug.Log($"{LogPrefix}   Recommended: renderScale={RecommendedRenderScale:F1}, buttonScale={RecommendedButtonScale:F1}");
        }

        private void DetectScreenProperties()
        {
            ScreenDpi = Screen.dpi > 0 ? Screen.dpi : 160f;
            SystemRamMB = SystemInfo.systemMemorySize;
            GpuName = SystemInfo.graphicsDeviceName;

            // Calculate physical diagonal
            float widthInches = Screen.width / ScreenDpi;
            float heightInches = Screen.height / ScreenDpi;
            ScreenDiagonalInches = Mathf.Sqrt(widthInches * widthInches + heightInches * heightInches);

            // Categorize screen size
            if (ScreenDiagonalInches < 5.0f)
                ScreenSize = ScreenSizeCategory.Small;
            else if (ScreenDiagonalInches < 6.5f)
                ScreenSize = ScreenSizeCategory.Medium;
            else if (ScreenDiagonalInches < 8.0f)
                ScreenSize = ScreenSizeCategory.Large;
            else
                ScreenSize = ScreenSizeCategory.Tablet;
        }

        private void DetectPerformanceTier()
        {
            int score = 0;

            // RAM scoring
            if (SystemRamMB >= 8192) score += 3;
            else if (SystemRamMB >= 6144) score += 2;
            else if (SystemRamMB >= 4096) score += 1;

            // GPU scoring based on shader level
            int shaderLevel = SystemInfo.graphicsShaderLevel;
            if (shaderLevel >= 50) score += 3;
            else if (shaderLevel >= 45) score += 2;
            else if (shaderLevel >= 35) score += 1;

            // CPU scoring
            int processorCount = SystemInfo.processorCount;
            if (processorCount >= 8) score += 2;
            else if (processorCount >= 4) score += 1;

            // Processor frequency
            int processorFreq = SystemInfo.processorFrequency;
            if (processorFreq >= 2500) score += 2;
            else if (processorFreq >= 1800) score += 1;

            // Max texture size
            int maxTextureSize = SystemInfo.maxTextureSize;
            if (maxTextureSize >= 8192) score += 1;

            // Categorize
            if (score >= 8)
                Performance = PerformanceTier.High;
            else if (score >= 4)
                Performance = PerformanceTier.Mid;
            else
                Performance = PerformanceTier.Low;
        }

        private void CalculateRecommendations()
        {
            // Render scale based on performance tier
            RecommendedRenderScale = Performance switch
            {
                PerformanceTier.High => 1.0f,
                PerformanceTier.Mid => 0.8f,
                PerformanceTier.Low => 0.6f,
                _ => 0.8f
            };

            // Button scale based on screen size
            RecommendedButtonScale = ScreenSize switch
            {
                ScreenSizeCategory.Small => 1.2f,
                ScreenSizeCategory.Medium => 1.0f,
                ScreenSizeCategory.Large => 0.95f,
                ScreenSizeCategory.Tablet => 0.85f,
                _ => 1.0f
            };
        }
    }

    /// <summary>
    /// Screen size categories based on physical diagonal measurement.
    /// </summary>
    public enum ScreenSizeCategory
    {
        /// <summary>Small phones (under 5").</summary>
        Small,
        /// <summary>Standard phones (5"–6.5").</summary>
        Medium,
        /// <summary>Large phones / phablets (6.5"–8").</summary>
        Large,
        /// <summary>Tablets (8"+).</summary>
        Tablet
    }

    /// <summary>
    /// Performance tiers based on device hardware capabilities.
    /// </summary>
    public enum PerformanceTier
    {
        /// <summary>Low-end devices — reduce quality for stable framerate.</summary>
        Low,
        /// <summary>Mid-range devices — balanced quality and performance.</summary>
        Mid,
        /// <summary>High-end devices — full quality enabled.</summary>
        High
    }
}
