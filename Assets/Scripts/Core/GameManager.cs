using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using Blob3D.Gameplay;

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

        // ---------- ゲーム状態 ----------
        public enum GameState { Title, Playing, Paused, GameOver, Result }

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

            Application.targetFrameRate = 60;
        }

        private void Update()
        {
            if (CurrentState != GameState.Playing) return;

            switch (currentMode)
            {
                case GameMode.Classic:
                    RemainingTime -= Time.deltaTime;
                    OnTimerUpdated?.Invoke(RemainingTime);

                    // Field shrinking when time is running low
                    if (RemainingTime < shrinkStartTime)
                    {
                        float t = 1f - (RemainingTime / shrinkStartTime);
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

            CurrentState = GameState.Playing;
            Time.timeScale = 1f;
            AudioManager.Instance?.PlayBGM();
            OnGameStart?.Invoke();

            // Start ambient floating particles around the player
            var playerBlob = Player.BlobController.Instance;
            if (playerBlob != null)
            {
                VFXManager.Instance?.StartAmbientParticles(playerBlob.transform);
            }
        }

        /// <summary>プレイヤーが食べられた場合のゲームオーバー（スローモーション演出付き）</summary>
        public void PlayerDied()
        {
            if (CurrentState != GameState.Playing) return;
            StartCoroutine(PlayerDiedSequence());
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

        /// <summary>時間切れによるラウンド終了</summary>
        public void EndRound()
        {
            if (CurrentState != GameState.Playing) return;
            CurrentState = GameState.Result;
            AudioManager.Instance?.StopBGM();
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
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>タイトルに戻る</summary>
        public void ReturnToTitle()
        {
            Time.timeScale = 1f;
            CurrentState = GameState.Title;
            SceneManager.LoadScene("Title");
        }

        /// <summary>フィールド内のランダム座標を返す</summary>
        public Vector3 GetRandomFieldPosition(float margin = 10f)
        {
            float r = fieldRadius - margin;
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
