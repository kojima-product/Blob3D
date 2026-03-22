using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System;
using System.Collections;
using Blob3D.Gameplay;
using TMPro;

namespace Blob3D.Core
{
    /// <summary>
    /// ゲーム全体のフロー管理を行うシングルトン。
    /// シーンをまたいで永続化する。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        // ---------- ゲームモード ----------
        public enum GameMode { Classic, Survival, TimeAttack }

        [Header("Game Mode")]
        [SerializeField] private GameMode currentMode = GameMode.Classic;
        public GameMode CurrentMode => currentMode;

        public void SetGameMode(GameMode mode) { currentMode = mode; }

        // ---------- 設定 ----------
        [Header("Round Settings")]
        [SerializeField] private float roundDuration = 180f; // 3分
        [SerializeField] private int maxAIBlobs = 30;

        [Header("Field Settings")]
        [SerializeField] private float fieldRadius = 150f;
        [SerializeField] private float shrinkStartTime = 90f;  // Start shrinking with 90 seconds left
        [SerializeField] private float minFieldRadius = 80f;    // Minimum radius

        [Header("TimeAttack Settings")]
        [SerializeField] private float timeAttackGoalSize = 30f;

        private float originalFieldRadius;
        private float elapsedTime; // For TimeAttack mode (counts up)

        // Fix: track coroutines for cleanup on scene reload / retry
        private Coroutine countdownCoroutine;
        private Coroutine playerDiedCoroutine;

        // ---------- ゲーム状態 ----------
        public enum GameState { Title, Countdown, Playing, Paused, GameOver, Result }

        public GameState CurrentState { get; private set; } = GameState.Title;
        public float RemainingTime { get; private set; }
        public float ElapsedTime => elapsedTime;
        public float FieldRadius => fieldRadius;
        public int MaxAIBlobs => maxAIBlobs;

        // ---------- イベント ----------
        public event Action OnGameStart;
        public event Action OnGameOver;
        public event Action OnGamePause;
        public event Action OnGameResume;
        public event Action<float> OnTimerUpdated; // 残り時間
        public event Action<int> OnCountdownTick; // 3, 2, 1, 0 (0 = GO!)

        // ---------- ライフサイクル ----------
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Mobile polish settings
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.orientation = ScreenOrientation.Portrait;
            QualitySettings.vSyncCount = 0;

            // Initialize localization system
            Blob3D.Utils.Localization.Initialize();
        }

        private void Update()
        {
            if (CurrentState != GameState.Playing) return;

            switch (currentMode)
            {
                case GameMode.Classic:
                    RemainingTime -= Time.deltaTime;
                    OnTimerUpdated?.Invoke(RemainingTime);

                    // Fix: clamp field shrinking — prevent negative RemainingTime from inverting lerp
                    if (RemainingTime < shrinkStartTime && shrinkStartTime > 0f)
                    {
                        float t = Mathf.Clamp01(1f - (RemainingTime / shrinkStartTime));
                        fieldRadius = Mathf.Lerp(originalFieldRadius, minFieldRadius, t);
                    }

                    if (RemainingTime <= 0f)
                    {
                        RemainingTime = 0f;
                        EndRound();
                    }
                    break;

                case GameMode.Survival:
                    // No timer countdown; game ends only when player dies
                    elapsedTime += Time.deltaTime;
                    // Send negative value as signal for "--:--" display
                    OnTimerUpdated?.Invoke(-1f);
                    break;

                case GameMode.TimeAttack:
                    elapsedTime += Time.deltaTime;
                    OnTimerUpdated?.Invoke(elapsedTime);

                    // Check if player reached goal size
                    var player = Player.BlobController.Instance;
                    if (player != null && player.CurrentSize >= timeAttackGoalSize)
                    {
                        EndRound();
                    }
                    break;
            }
        }

        // ---------- パブリックAPI ----------

        /// <summary>ラウンドを開始する</summary>
        public void StartRound()
        {
            originalFieldRadius = fieldRadius;
            elapsedTime = 0f;

            switch (currentMode)
            {
                case GameMode.Classic:
                    RemainingTime = roundDuration;
                    break;

                case GameMode.Survival:
                    RemainingTime = float.MaxValue;
                    break;

                case GameMode.TimeAttack:
                    RemainingTime = float.MaxValue;
                    break;
            }

            CurrentState = GameState.Countdown;
            Time.timeScale = 1f;
            // Fix: stop previous countdown if StartRound called twice
            if (countdownCoroutine != null) StopCoroutine(countdownCoroutine);
            countdownCoroutine = StartCoroutine(CountdownSequence());
        }

        private IEnumerator CountdownSequence()
        {
            // Create countdown UI
            GameObject canvasObj = new GameObject("CountdownCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            canvasObj.AddComponent<CanvasScaler>();

            GameObject textObj = new GameObject("CountdownText");
            textObj.transform.SetParent(canvasObj.transform, false);
            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 120;
            text.fontStyle = FontStyles.Bold;
            text.color = Color.white;
            text.enableWordWrapping = false;

            // Center the text
            RectTransform rt = text.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(400f, 200f);

            // Add outline for readability
            Outline outline = textObj.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(3f, -3f);

            // Countdown: 3, 2, 1
            for (int i = 3; i >= 1; i--)
            {
                text.text = i.ToString();
                OnCountdownTick?.Invoke(i);
                yield return new WaitForSeconds(1f);
            }

            // GO!
            text.text = "GO!";
            OnCountdownTick?.Invoke(0);

            // Start the actual game
            CurrentState = GameState.Playing;
            AudioManager.Instance?.PlayBGM();
            OnGameStart?.Invoke();

            // Start ambient floating particles around the player
            var playerBlob = Player.BlobController.Instance;
            if (playerBlob != null)
            {
                VFXManager.Instance?.StartAmbientParticles(playerBlob.transform);
            }

            yield return new WaitForSeconds(0.5f);

            // Clean up countdown UI
            Destroy(canvasObj);
        }

        /// <summary>プレイヤーが食べられた場合のゲームオーバー（スローモーション演出付き）</summary>
        public void PlayerDied()
        {
            if (CurrentState != GameState.Playing) return;

            // Cancel all active power-up effects before death sequence
            var player = Player.BlobController.Instance;
            if (player != null)
            {
                PowerUpEffectManager.Instance?.CancelAllEffects(player);
            }

            // Fix: track coroutine to prevent double-start
            if (playerDiedCoroutine != null) StopCoroutine(playerDiedCoroutine);
            playerDiedCoroutine = StartCoroutine(PlayerDiedSequence());
        }

        private IEnumerator PlayerDiedSequence()
        {
            // Prevent further gameplay state changes during the sequence
            CurrentState = GameState.Paused;

            // Dramatic slow-motion effect
            Time.timeScale = 0.3f;
            yield return new WaitForSecondsRealtime(0.8f);

            // Restore normal time and trigger game over
            Time.timeScale = 1f;
            CurrentState = GameState.GameOver;
            AudioManager.Instance?.StopBGM();
            OnGameOver?.Invoke();
        }

        /// <summary>Round ended (time's up in Classic, or goal reached in TimeAttack)</summary>
        public void EndRound()
        {
            if (CurrentState != GameState.Playing) return;
            CurrentState = GameState.Result;
            AudioManager.Instance?.StopBGM();
            AudioManager.Instance?.PlayVictory();
            StartCoroutine(EndRoundSlowMotion());
        }

        private IEnumerator EndRoundSlowMotion()
        {
            // Brief slow-motion celebration effect
            Time.timeScale = 0.5f;
            yield return new WaitForSecondsRealtime(0.5f);
            Time.timeScale = 1f;
            OnGameOver?.Invoke();
        }

        /// <summary>ポーズ</summary>
        public void PauseGame()
        {
            if (CurrentState != GameState.Playing) return;
            CurrentState = GameState.Paused;
            Time.timeScale = 0f;
            OnGamePause?.Invoke();
        }

        /// <summary>ポーズ解除</summary>
        public void ResumeGame()
        {
            if (CurrentState != GameState.Paused) return;
            CurrentState = GameState.Playing;
            Time.timeScale = 1f;
            OnGameResume?.Invoke();
        }

        /// <summary>リトライ（シーンリロード）</summary>
        public void Retry()
        {
            // Fix: stop active coroutines to prevent stale callbacks after scene reload
            StopAllCoroutines();
            Time.timeScale = 1f;
            fieldRadius = originalFieldRadius; // Fix: restore field radius for next round
            CurrentState = GameState.Title;
            StartCoroutine(LoadSceneWithOverlay());
        }

        /// <summary>タイトルに戻る</summary>
        public void ReturnToTitle()
        {
            // Fix: stop active coroutines to prevent stale callbacks after scene reload
            StopAllCoroutines();
            Time.timeScale = 1f;
            fieldRadius = originalFieldRadius; // Fix: restore field radius for next round
            CurrentState = GameState.Title;
            StartCoroutine(LoadSceneWithOverlay());
        }

        /// <summary>Async scene load with loading overlay shown by UIManager.</summary>
        private IEnumerator LoadSceneWithOverlay()
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex);
            while (op != null && !op.isDone)
            {
                yield return null;
            }
        }

        /// <summary>フィールド内のランダム座標を返す</summary>
        public Vector3 GetRandomFieldPosition(float margin = 10f)
        {
            // Fix: prevent negative radius when margin exceeds fieldRadius
            float r = Mathf.Max(fieldRadius - margin, 1f);
            Vector2 circle = UnityEngine.Random.insideUnitCircle * r;
            return new Vector3(circle.x, 0.5f, circle.y);
        }

        /// <summary>座標がフィールド内かチェックし、はみ出していればクランプ</summary>
        public Vector3 ClampToField(Vector3 position)
        {
            Vector3 flat = new Vector3(position.x, 0f, position.z);
            if (flat.magnitude > fieldRadius)
            {
                flat = flat.normalized * fieldRadius;
            }
            return new Vector3(flat.x, position.y, flat.z);
        }
    }
}
