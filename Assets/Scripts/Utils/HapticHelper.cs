using UnityEngine;

namespace Blob3D.Utils
{
    /// <summary>
    /// Simple haptic feedback helper. Uses Handheld.Vibrate on supported platforms.
    /// Falls back to no-op on unsupported platforms.
    /// </summary>
    public static class HapticHelper
    {
        private static float lastVibrateTime;
        private const float MinInterval = 0.05f; // Prevent vibration spam

        /// <summary>Light tap feedback (feed absorption, dash)</summary>
        public static void LightImpact()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (Time.unscaledTime - lastVibrateTime < MinInterval) return;
            lastVibrateTime = Time.unscaledTime;
            Handheld.Vibrate();
#endif
        }

        /// <summary>Medium impact (blob absorption)</summary>
        public static void MediumImpact()
        {
#if UNITY_ANDROID || UNITY_IOS
            lastVibrateTime = Time.unscaledTime;
            Handheld.Vibrate();
#endif
        }

        /// <summary>Heavy impact (being absorbed, game over)</summary>
        public static void HeavyImpact()
        {
#if UNITY_ANDROID || UNITY_IOS
            lastVibrateTime = Time.unscaledTime;
            Handheld.Vibrate();
#endif
        }
    }
}
