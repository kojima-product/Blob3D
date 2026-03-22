using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Blob3D.Core;
using Blob3D.Gameplay;
using Blob3D.Player;

namespace Blob3D.UI
{
    /// <summary>
    /// ゲーム中のHUD（スコア、タイマー、ランク表示）を管理。
    /// Adds scale pulse on score change, timer urgency effects,
    /// size glow pulse, and smooth powerup icon transitions.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private TextMeshProUGUI rankText;
        [SerializeField] private TextMeshProUGUI sizeText;

        [Header("PowerUp Indicator")]
        [SerializeField] private GameObject speedBoostIcon;
        [SerializeField] private GameObject shieldIcon;
        [SerializeField] private GameObject magnetIcon;
        [SerializeField] private GameObject ghostIcon;

        [Header("Score Animation")]
        [SerializeField] private float scorePulseScale = 1.3f;
        [SerializeField] private float scorePulseDuration = 0.15f;

        [Header("Timer Urgency")]
        [SerializeField] private float timerUrgencyThreshold = 30f;
        [SerializeField] private float timerPulseSpeed = 3f;
        [SerializeField] private Color timerNormalColor = Color.white;
        [SerializeField] private Color timerUrgentColor = Color.red;

        [Header("Size Pulse")]
        [SerializeField] private float sizePulseScale = 1.2f;
        [SerializeField] private float sizePulseDuration = 0.2f;

        [Header("PowerUp Icon Fade")]
        [SerializeField] private float iconFadeDuration = 0.25f;

        [Header("PowerUp Expiry Blink")]
        [SerializeField] private float estimatedPowerUpDuration = 5f;
        [SerializeField] private float blinkThreshold = 2f;
        [SerializeField] private float blinkSpeed = 6f;

        [Header("Dash Cooldown")]
        [SerializeField] private Image dashCooldownOverlay;

        [Header("Boundary Warning")]
        [SerializeField] private Image boundaryVignetteImage;
        [SerializeField] private float vignetteProximityThreshold = 0.7f;

        [Header("Combo Display")]
        [SerializeField] private TextMeshProUGUI comboText;

        // Cached state
        private float previousSize;
        private Coroutine scorePulseCoroutine;
        private Coroutine sizePulseCoroutine;
        private Coroutine comboPulseCoroutine;
        private float currentTimerValue = float.MaxValue;
        private float timerWarningCooldown;

        // Boundary warning
        private FieldBoundary fieldBoundary;

        // Track powerup icon visibility and fade coroutines
        private readonly Dictionary<GameObject, bool> iconStates = new Dictionary<GameObject, bool>();
        private readonly Dictionary<GameObject, Coroutine> iconCoroutines = new Dictionary<GameObject, Coroutine>();

        // Track when each powerup icon became active for expiry blinking
        private readonly Dictionary<GameObject, float> iconActivationTimes = new Dictionary<GameObject, float>();

        private void OnEnable()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnTimerUpdated += UpdateTimer;
            }
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnScoreChanged += UpdateScore;
                ScoreManager.Instance.OnComboChanged += UpdateCombo;
            }

            // Initialize previous size
            if (BlobController.Instance != null)
            {
                previousSize = BlobController.Instance.CurrentSize;
            }
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnTimerUpdated -= UpdateTimer;
            }
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnScoreChanged -= UpdateScore;
                ScoreManager.Instance.OnComboChanged -= UpdateCombo;
            }
        }

        private void Start()
        {
            fieldBoundary = FindObjectOfType<FieldBoundary>();
        }

        private void Update()
        {
            UpdateRank();
            UpdatePowerUpIndicators();
            UpdateTimerUrgency();
            UpdateBoundaryWarning();
            UpdateDashCooldown();
        }

        // ---------- Dash Cooldown Overlay ----------

        /// <summary>
        /// Update the semi-transparent overlay on the dash button to show cooldown progress.
        /// Uses Image.Type.Filled (fillAmount) for a radial/vertical wipe effect.
        /// </summary>
        private void UpdateDashCooldown()
        {
            if (dashCooldownOverlay == null || BlobController.Instance == null) return;

            dashCooldownOverlay.fillAmount = BlobController.Instance.DashCooldownRatio;
        }

        // ---------- Score with Pulse Animation ----------

        private void UpdateScore(int score)
        {
            if (scoreText == null) return;

            scoreText.text = score.ToString("N0");

            // Trigger scale pulse on score change
            if (scorePulseCoroutine != null)
            {
                StopCoroutine(scorePulseCoroutine);
            }
            scorePulseCoroutine = StartCoroutine(ScalePulse(scoreText.transform, scorePulseScale, scorePulseDuration));
        }

        // ---------- Timer with Urgency Effect ----------

        private void UpdateTimer(float timeValue)
        {
            if (timerText == null) return;

            // Survival mode: negative value signals no timer display
            if (timeValue < 0f)
            {
                currentTimerValue = float.MaxValue;
                timerText.text = "--:--";
                return;
            }

            // TimeAttack mode: elapsed time counts up
            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentMode == GameManager.GameMode.TimeAttack)
            {
                currentTimerValue = float.MaxValue; // No urgency for count-up
                int m = Mathf.FloorToInt(timeValue / 60f);
                int s = Mathf.FloorToInt(timeValue % 60f);
                timerText.text = $"{m}:{s:D2}";
                return;
            }

            // Classic mode: countdown
            currentTimerValue = timeValue;
            int minutes = Mathf.FloorToInt(timeValue / 60f);
            int seconds = Mathf.FloorToInt(timeValue % 60f);
            timerText.text = $"{minutes}:{seconds:D2}";
        }

        /// <summary>
        /// Apply pulsing red color to timer when below urgency threshold.
        /// Runs every frame to produce a smooth sine-wave pulse.
        /// </summary>
        private void UpdateTimerUrgency()
        {
            if (timerText == null) return;

            if (currentTimerValue <= timerUrgencyThreshold && currentTimerValue > 0f)
            {
                // Oscillate between normal and urgent color using sine wave
                float pulse = (Mathf.Sin(Time.unscaledTime * timerPulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
                timerText.color = Color.Lerp(timerNormalColor, timerUrgentColor, pulse);

                // Slight scale pulse synchronized with color
                float scaleAmount = 1f + pulse * 0.08f;
                timerText.transform.localScale = Vector3.one * scaleAmount;

                // Play warning beep periodically
                timerWarningCooldown -= Time.unscaledDeltaTime;
                if (timerWarningCooldown <= 0f)
                {
                    AudioManager.Instance?.PlayTimerWarning();
                    // Beep every 2 seconds normally, every 1 second under 10s
                    timerWarningCooldown = currentTimerValue <= 10f ? 1f : 2f;
                }
            }
            else
            {
                timerText.color = timerNormalColor;
                timerText.transform.localScale = Vector3.one;
                timerWarningCooldown = 0f;
            }
        }

        // ---------- Boundary Warning Vignette ----------

        /// <summary>
        /// Show red vignette overlay when player is near the field boundary.
        /// Alpha scales from 0 at threshold to 1 at the edge.
        /// </summary>
        private void UpdateBoundaryWarning()
        {
            if (boundaryVignetteImage == null || fieldBoundary == null) return;
            if (BlobController.Instance == null) return;

            float proximity = fieldBoundary.GetBoundaryProximity(BlobController.Instance.transform.position);

            if (proximity > vignetteProximityThreshold)
            {
                float alpha = (proximity - vignetteProximityThreshold) / (1f - vignetteProximityThreshold);
                Color c = boundaryVignetteImage.color;
                boundaryVignetteImage.color = new Color(c.r, c.g, c.b, alpha);
                boundaryVignetteImage.enabled = true;
            }
            else
            {
                boundaryVignetteImage.enabled = false;
            }
        }

        // ---------- Rank & Size with Glow Pulse ----------

        private void UpdateRank()
        {
            if (BlobController.Instance == null || ScoreManager.Instance == null) return;

            float size = BlobController.Instance.CurrentSize;
            string rank = ScoreManager.Instance.GetSizeRank(size);

            if (rankText != null)
                rankText.text = rank;

            if (sizeText != null)
                sizeText.text = $"x{size:F1}";

            // Detect size increase and trigger pulse
            if (size > previousSize + 0.05f)
            {
                TriggerSizePulse();
            }
            previousSize = size;

            // 最大サイズ記録を更新
            ScoreManager.Instance.UpdateMaxSize(size);
        }

        private void TriggerSizePulse()
        {
            if (sizeText == null) return;

            if (sizePulseCoroutine != null)
            {
                StopCoroutine(sizePulseCoroutine);
            }
            sizePulseCoroutine = StartCoroutine(ScalePulse(sizeText.transform, sizePulseScale, sizePulseDuration));
        }

        // ---------- PowerUp Icons with Smooth Fade ----------

        private void UpdatePowerUpIndicators()
        {
            if (BlobController.Instance == null) return;

            var player = BlobController.Instance;

            SetIconVisible(speedBoostIcon, player.SpeedMultiplier > 1f);
            SetIconVisible(shieldIcon, player.HasShield);
            SetIconVisible(magnetIcon, player.IsMagnetActive);
            SetIconVisible(ghostIcon, player.IsGhostActive);

            // Apply expiry blink to active icons nearing estimated expiration
            ApplyExpiryBlink(speedBoostIcon);
            ApplyExpiryBlink(shieldIcon);
            ApplyExpiryBlink(magnetIcon);
            ApplyExpiryBlink(ghostIcon);
        }

        /// <summary>
        /// When a power-up icon has been active long enough that it is likely about to expire,
        /// oscillate its alpha between 0.3 and 1.0 using a sine wave to warn the player.
        /// </summary>
        private void ApplyExpiryBlink(GameObject icon)
        {
            if (icon == null) return;
            if (!iconActivationTimes.ContainsKey(icon)) return;
            if (!iconStates.ContainsKey(icon) || !iconStates[icon]) return;

            float elapsed = Time.time - iconActivationTimes[icon];
            float remainingEstimate = estimatedPowerUpDuration - elapsed;

            if (remainingEstimate > 0f && remainingEstimate <= blinkThreshold)
            {
                CanvasGroup cg = icon.GetComponent<CanvasGroup>();
                if (cg == null) return;

                // Sine wave oscillation between 0.3 and 1.0
                float t = (Mathf.Sin(Time.time * blinkSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
                cg.alpha = Mathf.Lerp(0.3f, 1f, t);
            }
        }

        /// <summary>
        /// Show or hide a powerup icon with a smooth fade transition.
        /// Uses CanvasGroup alpha instead of instant SetActive toggling.
        /// </summary>
        private void SetIconVisible(GameObject icon, bool visible)
        {
            if (icon == null) return;

            // Initialize tracking on first access
            if (!iconStates.ContainsKey(icon))
            {
                iconStates[icon] = icon.activeSelf;
                iconCoroutines[icon] = null;
            }

            bool currentlyVisible = iconStates[icon];
            if (currentlyVisible == visible) return;

            iconStates[icon] = visible;

            // Track activation time for expiry blink estimation
            if (visible)
            {
                iconActivationTimes[icon] = Time.time;
            }
            else
            {
                iconActivationTimes.Remove(icon);
            }

            // Cancel ongoing fade for this icon
            if (iconCoroutines[icon] != null)
            {
                StopCoroutine(iconCoroutines[icon]);
            }

            iconCoroutines[icon] = StartCoroutine(FadeIcon(icon, visible));
        }

        private IEnumerator FadeIcon(GameObject icon, bool fadeIn)
        {
            // Ensure CanvasGroup exists
            CanvasGroup cg = icon.GetComponent<CanvasGroup>();
            if (cg == null)
            {
                cg = icon.AddComponent<CanvasGroup>();
            }

            // Show the object before fading in
            if (fadeIn)
            {
                icon.SetActive(true);
                cg.alpha = 0f;
            }

            float startAlpha = cg.alpha;
            float endAlpha = fadeIn ? 1f : 0f;
            float elapsed = 0f;

            while (elapsed < iconFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / iconFadeDuration);
                cg.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
                yield return null;
            }

            cg.alpha = endAlpha;

            // Deactivate object after fading out to save draw calls
            if (!fadeIn)
            {
                icon.SetActive(false);
            }
        }

        // ---------- Combo Display ----------

        private void EnsureComboText()
        {
            if (comboText != null) return;

            GameObject comboObj = new GameObject("ComboText");
            comboObj.transform.SetParent(transform, false);
            RectTransform rt = comboObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 80f);
            rt.sizeDelta = new Vector2(300f, 60f);

            comboText = comboObj.AddComponent<TextMeshProUGUI>();
            comboText.fontSize = 36;
            comboText.alignment = TextAlignmentOptions.Center;
            comboText.color = new Color(1f, 0.85f, 0.2f);
            comboText.fontStyle = FontStyles.Bold;
            comboText.raycastTarget = false;
            comboText.alpha = 0f;
        }

        private void UpdateCombo(int combo)
        {
            EnsureComboText();

            if (combo > 1)
            {
                comboText.text = $"x{combo}!";
                comboText.alpha = 1f;

                // Scale larger for higher combos
                float comboScale = GetComboScale(combo);
                comboText.color = GetComboColor(combo);

                if (comboPulseCoroutine != null)
                {
                    StopCoroutine(comboPulseCoroutine);
                }
                comboPulseCoroutine = StartCoroutine(ComboPulseAnimation(comboScale, GetComboDisplayDuration(combo)));
            }
            else
            {
                if (comboPulseCoroutine != null)
                {
                    StopCoroutine(comboPulseCoroutine);
                    comboPulseCoroutine = null;
                }
                StartCoroutine(FadeOutCombo());
            }
        }

        /// <summary>Return base scale multiplier based on combo count</summary>
        private float GetComboScale(int combo)
        {
            if (combo >= 5) return 2.5f;
            if (combo >= 4) return 2.2f;
            if (combo >= 3) return 1.8f;
            return 1.4f; // x2
        }

        /// <summary>Return color based on combo count</summary>
        private Color GetComboColor(int combo)
        {
            if (combo >= 5) return new Color(1f, 0.35f, 0.1f); // Red-orange
            if (combo >= 4) return new Color(1f, 0.55f, 0.15f); // Orange
            if (combo >= 3) return new Color(1f, 0.9f, 0.2f);  // Yellow
            return Color.white; // x2
        }

        /// <summary>Return display hold duration before fade based on combo count</summary>
        private float GetComboDisplayDuration(int combo)
        {
            if (combo >= 5) return 1.5f;
            if (combo >= 3) return 1.0f;
            return 0.6f; // x2
        }

        private IEnumerator ComboPulseAnimation(float targetScale, float holdDuration)
        {
            if (comboText == null) yield break;

            // Scale pop: overshoot -> settle at target scale over 0.2s
            float popDuration = 0.2f;
            float elapsed = 0f;
            float overshootScale = targetScale * 1.3f;

            // Pop up phase
            while (elapsed < popDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / popDuration);
                float scale = Mathf.Lerp(overshootScale, targetScale, t);
                comboText.transform.localScale = Vector3.one * scale;
                yield return null;
            }
            comboText.transform.localScale = Vector3.one * targetScale;

            // Hold at target scale
            yield return new WaitForSecondsRealtime(holdDuration);

            // Fade out after hold
            StartCoroutine(FadeOutCombo());
        }

        private IEnumerator FadeOutCombo()
        {
            if (comboText == null) yield break;

            float duration = 0.3f;
            float elapsed = 0f;
            float startAlpha = comboText.alpha;
            Vector3 startScale = comboText.transform.localScale;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                comboText.alpha = Mathf.Lerp(startAlpha, 0f, t);
                comboText.transform.localScale = Vector3.Lerp(startScale, Vector3.one, t);
                yield return null;
            }
            comboText.alpha = 0f;
            comboText.transform.localScale = Vector3.one;
        }

        // ---------- Shared Animation Coroutines ----------

        /// <summary>
        /// Quick scale pulse: scale up then back to 1.0 with ease-out.
        /// </summary>
        private IEnumerator ScalePulse(Transform target, float peakScale, float duration)
        {
            if (target == null) yield break;

            float halfDuration = duration * 0.5f;
            float elapsed = 0f;

            // Scale up phase
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                float scale = Mathf.Lerp(1f, peakScale, t);
                target.localScale = Vector3.one * scale;
                yield return null;
            }

            // Scale down phase (ease-out)
            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                float eased = t * t; // ease-in for quick snap back
                float scale = Mathf.Lerp(peakScale, 1f, eased);
                target.localScale = Vector3.one * scale;
                yield return null;
            }

            target.localScale = Vector3.one;
        }
    }
}
