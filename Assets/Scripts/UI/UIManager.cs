using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Blob3D.Core;
using Blob3D.Data;
using Blob3D.Gameplay;
using Blob3D.Player;
using Blob3D.Utils;

namespace Blob3D.UI
{
    /// <summary>
    /// 全画面のUI遷移（タイトル / ゲーム中 / リザルト / ポーズ）を管理。
    /// Smooth panel transitions, animated score counter, and screen shake support.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("Screen Panels")]
        [SerializeField] private GameObject titlePanel;
        [SerializeField] private GameObject gamePanel;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private GameObject pausePanel;

        [Header("Loading Screen")]
        [SerializeField] private GameObject loadingPanel;

        [Header("Title Screen")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button skinButton;
        [SerializeField] private TextMeshProUGUI highScoreLabel;
        [SerializeField] private Button statsButton;
        [SerializeField] private Button modeButton;
        [SerializeField] private TextMeshProUGUI modeLabelText;

        [Header("Stats Screen")]
        [SerializeField] private GameObject statsPanel;
        [SerializeField] private Button statsBackButton;
        [SerializeField] private TextMeshProUGUI statsTotalGamesText;
        [SerializeField] private TextMeshProUGUI statsHighScoreText;
        [SerializeField] private TextMeshProUGUI statsTotalFeedText;
        [SerializeField] private TextMeshProUGUI statsTotalBlobsText;
        [SerializeField] private TextMeshProUGUI statsMaxSizeText;
        [SerializeField] private TextMeshProUGUI statsBestTATimeText;
        [SerializeField] private TextMeshProUGUI statsLeaderboardText;

        [Header("Result Screen")]
        [SerializeField] private TextMeshProUGUI resultScoreText;
        [SerializeField] private TextMeshProUGUI resultRankText;
        [SerializeField] private TextMeshProUGUI resultFeedText;
        [SerializeField] private TextMeshProUGUI resultBlobsText;
        [SerializeField] private TextMeshProUGUI resultMaxSizeText;
        [SerializeField] private GameObject newHighScoreBadge;
        [SerializeField] private TextMeshProUGUI resultLeaderboardRankText;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button homeButton;

        [Header("Game Screen")]
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button dashButton;

        [Header("Pause Screen")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button pauseHomeButton;

        [Header("Shop Screen")]
        [SerializeField] private GameObject shopPanel;
        [SerializeField] private Button shopButton;
        [SerializeField] private Button shopBackButton;
        [SerializeField] private TextMeshProUGUI titleCoinDisplay;
        [SerializeField] private TextMeshProUGUI resultCoinsEarnedText;
        [SerializeField] private Transform shopContentParent;

        [Header("Settings Screen")]
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button settingsBackButton;
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider seSlider;
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private Button languageToggleButton;
        [SerializeField] private TextMeshProUGUI languageToggleLabel;

        [Header("Animation Settings")]
        [SerializeField] private float panelFadeDuration = 0.3f;
        [SerializeField] private float titleFadeDuration = 0.6f;
        [SerializeField] private float resultFadeDelay = 0.2f;
        [SerializeField] private float resultFadeDuration = 0.4f;
        [SerializeField] private float scoreCountUpDuration = 1.2f;
        [SerializeField] private float playButtonSlideDistance = 200f;
        [SerializeField] private float playButtonSlideDuration = 0.5f;

        [Header("Skin Unlock Notification")]
        [SerializeField] private TextMeshProUGUI skinUnlockText;
        [SerializeField] private float skinNotifyFadeDuration = 0.5f;
        [SerializeField] private float skinNotifyDisplayDuration = 2f;

        [Header("Achievement Notification")]
        [SerializeField] private TextMeshProUGUI achievementUnlockText;
        [SerializeField] private float achievementNotifyFadeDuration = 0.5f;
        [SerializeField] private float achievementNotifyDisplayDuration = 2.5f;

        [Header("Daily Reward")]
        [SerializeField] private GameObject dailyRewardPanel;
        [SerializeField] private TextMeshProUGUI dailyRewardText;
        [SerializeField] private Button dailyRewardClaimButton;
        [SerializeField] private Button dailyRewardCloseButton;

        // Cached CanvasGroups (ensured at runtime)
        private CanvasGroup titleCanvasGroup;
        private CanvasGroup gameCanvasGroup;
        private CanvasGroup resultCanvasGroup;
        private CanvasGroup pauseCanvasGroup;
        private CanvasGroup settingsCanvasGroup;
        private CanvasGroup statsCanvasGroup;
        private CanvasGroup shopCanvasGroup;
        private CanvasGroup loadingCanvasGroup;

        // Play button slide animation state
        private RectTransform playButtonRect;
        private Vector2 playButtonTargetPos;

        // Screen shake state
        private RectTransform canvasRect;
        private Coroutine shakeCoroutine;

        // Cached controller references
        private ShopController shopController;

        // Active transition coroutines (for cancellation)
        private Coroutine titleTransition;
        private Coroutine gameTransition;
        private Coroutine resultTransition;
        private Coroutine pauseTransition;
        private Coroutine scoreCountCoroutine;
        private Coroutine skinNotifyCoroutine;
        private Coroutine achievementNotifyCoroutine;
        private CanvasGroup dailyRewardCanvasGroup;

        public static UIManager Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            // Ensure CanvasGroups exist on each panel
            titleCanvasGroup = EnsureCanvasGroup(titlePanel);
            gameCanvasGroup = EnsureCanvasGroup(gamePanel);
            resultCanvasGroup = EnsureCanvasGroup(resultPanel);
            pauseCanvasGroup = EnsureCanvasGroup(pausePanel);
            settingsCanvasGroup = EnsureCanvasGroup(settingsPanel);
            statsCanvasGroup = EnsureCanvasGroup(statsPanel);
            shopCanvasGroup = EnsureCanvasGroup(shopPanel);
            loadingCanvasGroup = EnsureCanvasGroup(loadingPanel);

            // Hide loading panel initially
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(false);
            }

            // Cache canvas RectTransform for screen shake
            Canvas rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas != null)
            {
                canvasRect = rootCanvas.GetComponent<RectTransform>();
            }

            // Cache play button RectTransform for slide animation
            if (playButton != null)
            {
                playButtonRect = playButton.GetComponent<RectTransform>();
                playButtonTargetPos = playButtonRect.anchoredPosition;
            }

            // ボタンバインド
            playButton?.onClick.AddListener(OnPlayClicked);
            retryButton?.onClick.AddListener(OnRetryClicked);
            homeButton?.onClick.AddListener(OnHomeClicked);
            pauseButton?.onClick.AddListener(OnPauseClicked);
            resumeButton?.onClick.AddListener(OnResumeClicked);
            pauseHomeButton?.onClick.AddListener(OnHomeClicked);
            dashButton?.onClick.AddListener(OnDashClicked);
            settingsButton?.onClick.AddListener(OnSettingsClicked);
            settingsBackButton?.onClick.AddListener(OnSettingsBackClicked);
            statsButton?.onClick.AddListener(OnStatsClicked);
            statsBackButton?.onClick.AddListener(OnStatsBackClicked);
            modeButton?.onClick.AddListener(OnModeChanged);
            skinButton?.onClick.AddListener(OnShopClicked);
            shopButton?.onClick.AddListener(OnShopClicked);
            shopBackButton?.onClick.AddListener(OnShopBackClicked);
            languageToggleButton?.onClick.AddListener(OnLanguageToggleClicked);

            // Subscribe to localization changes to refresh visible text
            Localization.OnLanguageChanged += RefreshLocalizedText;

            // Settings slider listeners
            bgmSlider?.onValueChanged.AddListener(OnBGMVolumeChanged);
            seSlider?.onValueChanged.AddListener(OnSEVolumeChanged);
            sensitivitySlider?.onValueChanged.AddListener(OnSensitivityChanged);

            // Load saved settings values
            LoadSettings();

            // Update language toggle label
            UpdateLanguageToggleLabel();

            // Initialize ShopController with references
            var shopCtrl = shopPanel?.GetComponent<ShopController>();
            if (shopCtrl != null) shopCtrl.Initialize(titleCoinDisplay, shopContentParent);

            // イベント購読
            GameManager.Instance.OnGameStart += ShowGameUI;
            GameManager.Instance.OnGameOver += ShowResultUI;

            // Subscribe to camera shake events from BlobController
            BlobController.OnScreenShakeRequested += HandleScreenShake;

            // Subscribe to skin unlock notifications
            if (SkinManager.Instance != null)
            {
                SkinManager.Instance.OnSkinUnlocked += HandleSkinUnlocked;
            }

            // Hide skin unlock text initially
            if (skinUnlockText != null)
            {
                skinUnlockText.alpha = 0f;
                skinUnlockText.gameObject.SetActive(false);
            }

            // Subscribe to achievement unlock notifications
            if (AchievementManager.Instance != null)
            {
                AchievementManager.Instance.OnAchievementUnlocked += HandleAchievementUnlocked;
            }

            // Hide achievement unlock text initially
            if (achievementUnlockText != null)
            {
                achievementUnlockText.alpha = 0f;
                achievementUnlockText.gameObject.SetActive(false);
            }

            // Daily reward panel setup
            dailyRewardCanvasGroup = EnsureCanvasGroup(dailyRewardPanel);
            if (dailyRewardPanel != null) dailyRewardPanel.SetActive(false);
            dailyRewardClaimButton?.onClick.AddListener(OnDailyRewardClaim);
            dailyRewardCloseButton?.onClick.AddListener(OnDailyRewardClose);

            // 初期状態
            ShowTitleUI();
        }

        // ---------- Panel Transition Helpers ----------

        /// <summary>
        /// Ensure a CanvasGroup component exists on the given panel.
        /// </summary>
        private CanvasGroup EnsureCanvasGroup(GameObject panel)
        {
            if (panel == null) return null;
            CanvasGroup cg = panel.GetComponent<CanvasGroup>();
            if (cg == null)
            {
                cg = panel.AddComponent<CanvasGroup>();
            }
            return cg;
        }

        /// <summary>
        /// Fade a CanvasGroup from startAlpha to endAlpha over duration seconds.
        /// Uses unscaled time so it works during pause.
        /// </summary>
        private IEnumerator FadeCanvasGroup(CanvasGroup cg, float startAlpha, float endAlpha, float duration, float delay = 0f)
        {
            if (cg == null) yield break;

            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            cg.alpha = startAlpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Smooth ease-out curve
                float eased = 1f - (1f - t) * (1f - t);
                cg.alpha = Mathf.Lerp(startAlpha, endAlpha, eased);
                yield return null;
            }

            cg.alpha = endAlpha;
            cg.interactable = endAlpha > 0.5f;
            cg.blocksRaycasts = endAlpha > 0.5f;
        }

        /// <summary>
        /// Slide a RectTransform from an offset position to its target position.
        /// </summary>
        private IEnumerator SlideIn(RectTransform rt, Vector2 targetPos, Vector2 offset, float duration, float delay = 0f)
        {
            if (rt == null) yield break;

            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            Vector2 startPos = targetPos + offset;
            rt.anchoredPosition = startPos;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Ease-out back curve for a slight overshoot
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                rt.anchoredPosition = Vector2.Lerp(startPos, targetPos, eased);
                yield return null;
            }

            rt.anchoredPosition = targetPos;
        }

        /// <summary>
        /// Animate a TMP text counting up from 0 to targetValue.
        /// </summary>
        private IEnumerator AnimateScoreCounter(TextMeshProUGUI text, int targetValue, float duration, float delay = 0f)
        {
            if (text == null) yield break;

            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            float elapsed = 0f;
            text.text = "0";

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Ease-out for satisfying deceleration at the end
                float eased = 1f - (1f - t) * (1f - t);
                int displayValue = Mathf.RoundToInt(Mathf.Lerp(0f, targetValue, eased));
                text.text = displayValue.ToString("N0");
                yield return null;
            }

            text.text = targetValue.ToString("N0");
        }

        // ---------- 画面表示切替 ----------

        private void ShowTitleUI()
        {
            // Deactivate other panels immediately
            SetPanelActive(gamePanel, gameCanvasGroup, false);
            SetPanelActive(resultPanel, resultCanvasGroup, false);
            SetPanelActive(pausePanel, pauseCanvasGroup, false);
            SetPanelActive(settingsPanel, settingsCanvasGroup, false);
            SetPanelActive(statsPanel, statsCanvasGroup, false);
            SetPanelActive(shopPanel, shopCanvasGroup, false);

            // Activate and fade in title panel
            SetPanelActive(titlePanel, titleCanvasGroup, true, startHidden: true);

            if (highScoreLabel != null && ScoreManager.Instance != null)
            {
                highScoreLabel.text = Localization.Get("best_score", ScoreManager.Instance.HighScore.ToString("N0"));
            }

            // Update coin display on title screen
            if (titleCoinDisplay != null && ScoreManager.Instance != null)
            {
                titleCoinDisplay.text = Localization.Get("shop_coins", ScoreManager.Instance.Coins);
            }

            // Update mode label on title screen
            UpdateModeLabelText();

            // Check if daily reward is available
            if (DailyRewardManager.Instance != null && DailyRewardManager.Instance.CanClaimToday)
            {
                ShowDailyRewardPopup();
            }

            // Cancel previous title transition
            StopCoroutineSafe(ref titleTransition);

            // Fade in title panel
            titleTransition = StartCoroutine(FadeCanvasGroup(titleCanvasGroup, 0f, 1f, titleFadeDuration));

            // Slide play button up from bottom
            if (playButtonRect != null)
            {
                StartCoroutine(SlideIn(
                    playButtonRect,
                    playButtonTargetPos,
                    new Vector2(0f, -playButtonSlideDistance),
                    playButtonSlideDuration,
                    delay: titleFadeDuration * 0.3f
                ));
            }
        }

        private void ShowGameUI()
        {
            // Deactivate other panels
            SetPanelActive(titlePanel, titleCanvasGroup, false);
            SetPanelActive(resultPanel, resultCanvasGroup, false);
            SetPanelActive(pausePanel, pauseCanvasGroup, false);

            // Activate and quick fade in game panel
            SetPanelActive(gamePanel, gameCanvasGroup, true, startHidden: true);

            StopCoroutineSafe(ref gameTransition);
            gameTransition = StartCoroutine(FadeCanvasGroup(gameCanvasGroup, 0f, 1f, panelFadeDuration));
        }

        private void ShowResultUI()
        {
            // Deactivate other panels
            SetPanelActive(titlePanel, titleCanvasGroup, false);
            SetPanelActive(gamePanel, gameCanvasGroup, false);
            SetPanelActive(pausePanel, pauseCanvasGroup, false);

            if (ScoreManager.Instance == null) return;

            bool isNewHigh = ScoreManager.Instance.FinalizeRound();

            // Pre-populate static result texts
            if (resultRankText != null)
                resultRankText.text = ScoreManager.Instance.GetSizeRank(ScoreManager.Instance.MaxSizeReached);

            if (resultFeedText != null)
                resultFeedText.text = ScoreManager.Instance.FeedEaten.ToString();

            if (resultBlobsText != null)
                resultBlobsText.text = ScoreManager.Instance.BlobsAbsorbed.ToString();

            if (resultMaxSizeText != null)
                resultMaxSizeText.text = $"x{ScoreManager.Instance.MaxSizeReached:F1}";

            if (newHighScoreBadge != null)
                newHighScoreBadge.SetActive(isNewHigh);

            // Show leaderboard rank
            if (resultLeaderboardRankText != null)
            {
                int rank = ScoreManager.Instance.LastLeaderboardRank;
                if (rank > 0)
                {
                    resultLeaderboardRankText.text = Localization.Get("result_leaderboard", rank);
                    resultLeaderboardRankText.gameObject.SetActive(true);
                }
                else
                {
                    resultLeaderboardRankText.gameObject.SetActive(false);
                }
            }

            // Show coins earned this round
            int coinsEarned = Mathf.RoundToInt(ScoreManager.Instance.CurrentScore * 0.1f);
            if (resultCoinsEarnedText != null)
                resultCoinsEarnedText.text = Localization.Get("coins_earned", coinsEarned);

            // Activate result panel hidden, then fade in with delay
            SetPanelActive(resultPanel, resultCanvasGroup, true, startHidden: true);

            StopCoroutineSafe(ref resultTransition);
            StopCoroutineSafe(ref scoreCountCoroutine);

            resultTransition = StartCoroutine(FadeCanvasGroup(
                resultCanvasGroup, 0f, 1f, resultFadeDuration, delay: resultFadeDelay
            ));

            // Animate score counter from 0 to final value
            int finalScore = ScoreManager.Instance.CurrentScore;
            float counterDelay = resultFadeDelay + resultFadeDuration * 0.5f;
            scoreCountCoroutine = StartCoroutine(AnimateScoreCounter(
                resultScoreText, finalScore, scoreCountUpDuration, delay: counterDelay
            ));
        }

        /// <summary>
        /// Activate or deactivate a panel, optionally starting with alpha = 0.
        /// </summary>
        private void SetPanelActive(GameObject panel, CanvasGroup cg, bool active, bool startHidden = false)
        {
            if (panel == null) return;
            panel.SetActive(active);

            if (cg != null)
            {
                if (active && startHidden)
                {
                    cg.alpha = 0f;
                    cg.interactable = false;
                    cg.blocksRaycasts = false;
                }
                else if (!active)
                {
                    cg.alpha = 0f;
                    cg.interactable = false;
                    cg.blocksRaycasts = false;
                }
            }
        }

        // ---------- Screen Shake ----------

        /// <summary>
        /// Public API: trigger a screen shake effect on the root canvas.
        /// </summary>
        public void TriggerScreenShake(float intensity, float duration)
        {
            if (canvasRect == null) return;

            if (shakeCoroutine != null)
            {
                StopCoroutine(shakeCoroutine);
            }
            shakeCoroutine = StartCoroutine(ScreenShakeCoroutine(intensity, duration));
        }

        private IEnumerator ScreenShakeCoroutine(float intensity, float duration)
        {
            Vector2 originalPos = canvasRect.anchoredPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                // Decay intensity over time
                float decay = 1f - (elapsed / duration);
                float offsetX = Random.Range(-1f, 1f) * intensity * decay;
                float offsetY = Random.Range(-1f, 1f) * intensity * decay;
                canvasRect.anchoredPosition = originalPos + new Vector2(offsetX, offsetY);
                yield return null;
            }

            canvasRect.anchoredPosition = originalPos;
            shakeCoroutine = null;
        }

        /// <summary>
        /// Handler for BlobController screen shake events.
        /// </summary>
        private void HandleScreenShake(float intensity, float duration)
        {
            TriggerScreenShake(intensity, duration);
        }

        // ---------- Pause Panel Transitions ----------

        private void ShowPausePanel()
        {
            if (pausePanel == null) return;
            pausePanel.SetActive(true);

            if (pauseCanvasGroup != null)
            {
                pauseCanvasGroup.alpha = 0f;
                pauseCanvasGroup.interactable = false;
                pauseCanvasGroup.blocksRaycasts = false;
            }

            StopCoroutineSafe(ref pauseTransition);
            pauseTransition = StartCoroutine(FadeCanvasGroup(pauseCanvasGroup, 0f, 1f, panelFadeDuration));
        }

        private void HidePausePanel()
        {
            StopCoroutineSafe(ref pauseTransition);
            pauseTransition = StartCoroutine(HidePausePanelCoroutine());
        }

        private IEnumerator HidePausePanelCoroutine()
        {
            yield return FadeCanvasGroup(pauseCanvasGroup, 1f, 0f, panelFadeDuration);
            pausePanel?.SetActive(false);
            pauseTransition = null;
        }

        // ---------- Skin Unlock Notification ----------

        private void HandleSkinUnlocked(SkinData skin)
        {
            StopCoroutineSafe(ref skinNotifyCoroutine);
            skinNotifyCoroutine = StartCoroutine(ShowSkinUnlockNotification(skin));
        }

        private IEnumerator ShowSkinUnlockNotification(SkinData skin)
        {
            if (skinUnlockText == null) yield break;

            skinUnlockText.text = Localization.Get("skin_unlocked", skin.skinName);
            skinUnlockText.gameObject.SetActive(true);

            // Fade in
            float elapsed = 0f;
            while (elapsed < skinNotifyFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                skinUnlockText.alpha = Mathf.Clamp01(elapsed / skinNotifyFadeDuration);
                yield return null;
            }
            skinUnlockText.alpha = 1f;

            // Display
            yield return new WaitForSecondsRealtime(skinNotifyDisplayDuration);

            // Fade out
            elapsed = 0f;
            while (elapsed < skinNotifyFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                skinUnlockText.alpha = 1f - Mathf.Clamp01(elapsed / skinNotifyFadeDuration);
                yield return null;
            }
            skinUnlockText.alpha = 0f;
            skinUnlockText.gameObject.SetActive(false);
            skinNotifyCoroutine = null;
        }

        // ---------- Achievement Unlock Notification ----------

        private void HandleAchievementUnlocked(AchievementManager.Achievement achievement)
        {
            StopCoroutineSafe(ref achievementNotifyCoroutine);
            achievementNotifyCoroutine = StartCoroutine(ShowAchievementNotification(achievement));
        }

        private IEnumerator ShowAchievementNotification(AchievementManager.Achievement achievement)
        {
            if (achievementUnlockText == null) yield break;

            achievementUnlockText.text = Localization.Get("ach_notification", achievement.title);
            achievementUnlockText.gameObject.SetActive(true);

            // Fade in
            float elapsed = 0f;
            while (elapsed < achievementNotifyFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                achievementUnlockText.alpha = Mathf.Clamp01(elapsed / achievementNotifyFadeDuration);
                yield return null;
            }
            achievementUnlockText.alpha = 1f;

            // Display
            yield return new WaitForSecondsRealtime(achievementNotifyDisplayDuration);

            // Fade out
            elapsed = 0f;
            while (elapsed < achievementNotifyFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                achievementUnlockText.alpha = 1f - Mathf.Clamp01(elapsed / achievementNotifyFadeDuration);
                yield return null;
            }
            achievementUnlockText.alpha = 0f;
            achievementUnlockText.gameObject.SetActive(false);
            achievementNotifyCoroutine = null;
        }

        // ---------- Daily Reward Popup ----------

        private void ShowDailyRewardPopup()
        {
            if (dailyRewardPanel == null || DailyRewardManager.Instance == null) return;

            int streak = DailyRewardManager.Instance.CurrentStreak + 1;
            int reward = DailyRewardManager.Instance.TodayReward;
            if (dailyRewardText != null)
                dailyRewardText.text = Localization.Get("daily_desc", streak, reward);

            dailyRewardPanel.SetActive(true);
            if (dailyRewardCanvasGroup != null)
            {
                dailyRewardCanvasGroup.alpha = 0f;
                StartCoroutine(FadeCanvasGroup(dailyRewardCanvasGroup, 0f, 1f, panelFadeDuration));
            }
        }

        private void HideDailyRewardPopup()
        {
            if (dailyRewardPanel != null)
                dailyRewardPanel.SetActive(false);
        }

        private void OnDailyRewardClaim()
        {
            if (DailyRewardManager.Instance == null) return;
            bool claimed = DailyRewardManager.Instance.ClaimReward();
            if (claimed && titleCoinDisplay != null && ScoreManager.Instance != null)
            {
                titleCoinDisplay.text = Localization.Get("shop_coins", ScoreManager.Instance.Coins);
            }
            HideDailyRewardPopup();
        }

        private void OnDailyRewardClose()
        {
            HideDailyRewardPopup();
        }

        // ---------- ボタンハンドラ ----------

        private void OnPlayClicked()
        {
            GameManager.Instance.StartRound();
        }

        private void OnRetryClicked()
        {
            ShowLoadingPanel();
            GameManager.Instance.Retry();
        }

        private void OnHomeClicked()
        {
            ShowLoadingPanel();
            GameManager.Instance.ReturnToTitle();
        }

        private void OnPauseClicked()
        {
            GameManager.Instance.PauseGame();
            ShowPausePanel();
        }

        private void OnResumeClicked()
        {
            GameManager.Instance.ResumeGame();
            HidePausePanel();
        }

        private void OnDashClicked()
        {
            BlobController.Instance?.TriggerDash();
        }

        // ---------- Stats & Mode ----------

        private void OnStatsClicked()
        {
            SetPanelActive(titlePanel, titleCanvasGroup, false);
            SetPanelActive(statsPanel, statsCanvasGroup, true, startHidden: true);
            PopulateStatsPanel();
            StartCoroutine(FadeCanvasGroup(statsCanvasGroup, 0f, 1f, panelFadeDuration));
        }

        private void OnStatsBackClicked()
        {
            SetPanelActive(statsPanel, statsCanvasGroup, false);
            ShowTitleUI();
        }

        private void OnModeChanged()
        {
            if (GameManager.Instance == null) return;

            // Cycle through modes: Classic -> Survival -> TimeAttack -> Classic
            GameManager.GameMode current = GameManager.Instance.CurrentMode;
            GameManager.GameMode next = current switch
            {
                GameManager.GameMode.Classic => GameManager.GameMode.Survival,
                GameManager.GameMode.Survival => GameManager.GameMode.TimeAttack,
                GameManager.GameMode.TimeAttack => GameManager.GameMode.Classic,
                _ => GameManager.GameMode.Classic
            };
            GameManager.Instance.SetGameMode(next);
            UpdateModeLabelText();
        }

        private void UpdateModeLabelText()
        {
            if (modeLabelText == null || GameManager.Instance == null) return;
            modeLabelText.text = GameManager.Instance.CurrentMode switch
            {
                GameManager.GameMode.Classic => Localization.Get("mode_classic"),
                GameManager.GameMode.Survival => Localization.Get("mode_survival"),
                GameManager.GameMode.TimeAttack => Localization.Get("mode_timeattack"),
                _ => Localization.Get("mode_classic")
            };
        }

        private void PopulateStatsPanel()
        {
            if (ScoreManager.Instance == null) return;

            if (statsTotalGamesText != null)
                statsTotalGamesText.text = ScoreManager.Instance.TotalGamesPlayed.ToString();
            if (statsHighScoreText != null)
                statsHighScoreText.text = ScoreManager.Instance.HighScore.ToString("N0");
            if (statsTotalFeedText != null)
                statsTotalFeedText.text = ScoreManager.Instance.TotalFeedEaten.ToString("N0");
            if (statsTotalBlobsText != null)
                statsTotalBlobsText.text = ScoreManager.Instance.TotalBlobsAbsorbed.ToString("N0");
            if (statsMaxSizeText != null)
                statsMaxSizeText.text = $"x{ScoreManager.Instance.MaxSizeReached:F1}";
            if (statsBestTATimeText != null)
            {
                float best = ScoreManager.Instance.BestTimeAttackTime;
                statsBestTATimeText.text = best > 0f
                    ? $"{Mathf.FloorToInt(best / 60f)}:{(best % 60f):00.0}"
                    : "--:--.0";
            }

            // Populate leaderboard top 10
            if (statsLeaderboardText != null)
            {
                var topScores = LocalLeaderboard.GetTopScores();
                if (topScores.Count == 0)
                {
                    statsLeaderboardText.text = Localization.Get("stats_noscores");
                }
                else
                {
                    var sb = new System.Text.StringBuilder();
                    for (int i = 0; i < topScores.Count; i++)
                    {
                        var e = topScores[i];
                        sb.AppendLine($"#{i + 1}  {e.score:N0}  x{e.maxSize:F1}  {e.mode}  {e.date}");
                    }
                    statsLeaderboardText.text = sb.ToString().TrimEnd();
                }
            }
        }

        // ---------- Shop ----------

        private void OnShopClicked()
        {
            SetPanelActive(titlePanel, titleCanvasGroup, false);
            SetPanelActive(shopPanel, shopCanvasGroup, true, startHidden: true);

            // Ensure ShopController is initialized and refreshed
            if (shopController == null && shopPanel != null)
            {
                shopController = shopPanel.GetComponentInChildren<ShopController>();
            }
            if (shopController != null)
            {
                shopController.RefreshShop();
            }

            StartCoroutine(FadeCanvasGroup(shopCanvasGroup, 0f, 1f, panelFadeDuration));
        }

        private void OnShopBackClicked()
        {
            SetPanelActive(shopPanel, shopCanvasGroup, false);
            ShowTitleUI();
        }

        // ---------- Settings ----------

        private void OnSettingsClicked()
        {
            SetPanelActive(titlePanel, titleCanvasGroup, false);
            SetPanelActive(settingsPanel, settingsCanvasGroup, true, startHidden: true);
            StartCoroutine(FadeCanvasGroup(settingsCanvasGroup, 0f, 1f, panelFadeDuration));
        }

        private void OnSettingsBackClicked()
        {
            SetPanelActive(settingsPanel, settingsCanvasGroup, false);
            ShowTitleUI();
        }

        private void OnBGMVolumeChanged(float val)
        {
            AudioManager.Instance?.SetBGMVolume(val);
            PlayerPrefs.SetFloat("BGMVolume", val);
        }

        private void OnSEVolumeChanged(float val)
        {
            AudioManager.Instance?.SetSEVolume(val);
            PlayerPrefs.SetFloat("SEVolume", val);
        }

        private void OnSensitivityChanged(float val)
        {
            BlobCameraController cam = Object.FindFirstObjectByType<BlobCameraController>();
            if (cam != null)
            {
                cam.SetSensitivity(val);
            }
            PlayerPrefs.SetFloat("Sensitivity", val);
        }

        private void LoadSettings()
        {
            float bgm = PlayerPrefs.GetFloat("BGMVolume", 0.3f);
            float se = PlayerPrefs.GetFloat("SEVolume", 0.6f);
            float sens = PlayerPrefs.GetFloat("Sensitivity", 0.25f);

            if (bgmSlider != null) bgmSlider.value = bgm;
            if (seSlider != null) seSlider.value = se;
            if (sensitivitySlider != null) sensitivitySlider.value = sens;

            AudioManager.Instance?.SetBGMVolume(bgm);
            AudioManager.Instance?.SetSEVolume(se);
        }

        // ---------- Language ----------

        private void OnLanguageToggleClicked()
        {
            Localization.ToggleLanguage();
        }

        private void UpdateLanguageToggleLabel()
        {
            if (languageToggleLabel != null)
            {
                languageToggleLabel.text = Localization.CurrentLanguage == Localization.Language.Japanese
                    ? "JP / en" : "jp / EN";
            }
        }

        /// <summary>
        /// Refresh all currently visible localized text when language changes.
        /// </summary>
        private void RefreshLocalizedText()
        {
            UpdateLanguageToggleLabel();
            UpdateModeLabelText();

            // Refresh title screen text if visible
            if (titlePanel != null && titlePanel.activeSelf)
            {
                if (highScoreLabel != null && ScoreManager.Instance != null)
                {
                    highScoreLabel.text = Localization.Get("best_score", ScoreManager.Instance.HighScore.ToString("N0"));
                }
                if (titleCoinDisplay != null && ScoreManager.Instance != null)
                {
                    titleCoinDisplay.text = Localization.Get("shop_coins", ScoreManager.Instance.Coins);
                }
            }
        }

        // ---------- Utility ----------

        /// <summary>
        /// Safely stop a coroutine and null the reference.
        /// </summary>
        private void StopCoroutineSafe(ref Coroutine coroutine)
        {
            if (coroutine != null)
            {
                StopCoroutine(coroutine);
                coroutine = null;
            }
        }

        /// <summary>
        /// Show the loading overlay panel before scene transitions.
        /// </summary>
        private void ShowLoadingPanel()
        {
            if (loadingPanel == null) return;
            loadingPanel.SetActive(true);
            if (loadingCanvasGroup != null)
            {
                loadingCanvasGroup.alpha = 1f;
                loadingCanvasGroup.interactable = false;
                loadingCanvasGroup.blocksRaycasts = true;
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStart -= ShowGameUI;
                GameManager.Instance.OnGameOver -= ShowResultUI;
            }

            BlobController.OnScreenShakeRequested -= HandleScreenShake;
            Localization.OnLanguageChanged -= RefreshLocalizedText;

            if (SkinManager.Instance != null)
            {
                SkinManager.Instance.OnSkinUnlocked -= HandleSkinUnlocked;
            }

            if (AchievementManager.Instance != null)
            {
                AchievementManager.Instance.OnAchievementUnlocked -= HandleAchievementUnlocked;
            }
        }
    }
}
