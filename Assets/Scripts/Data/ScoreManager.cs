using UnityEngine;
using System;
using Blob3D.Data;
using Blob3D.Utils;

namespace Blob3D.Gameplay
{
    /// <summary>
    /// スコアの計算・記録・ハイスコア管理を担当。
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        // ---------- 設定 ----------
        [Header("Score Multipliers")]
        [SerializeField] private float feedScoreMultiplier = 10f;
        [SerializeField] private float blobScoreMultiplier = 50f;

        // ---------- 状態 ----------
        public int CurrentScore { get; private set; }
        public int HighScore { get; private set; }
        public int FeedEaten { get; private set; }
        public int BlobsAbsorbed { get; private set; }
        public float MaxSizeReached { get; private set; }
        public int TotalGamesPlayed { get; private set; }

        /// <summary>Leaderboard rank achieved in the last round (1-10), or 0 if not ranked.</summary>
        public int LastLeaderboardRank { get; private set; }

        // ---------- コンボ ----------
        private int comboCount;
        private int maxComboThisRound;
        private float comboTimer;
        private const float ComboWindow = 2f;

        public int ComboCount => comboCount;
        public int MaxComboThisRound => maxComboThisRound;
        public float ComboMultiplier => 1f + comboCount * 0.25f;

        // ---------- コイン ----------
        private int coins;
        public int Coins => coins;
        public event Action<int> OnCoinsChanged;

        // ---------- イベント ----------
        public event Action<int> OnScoreChanged;
        public event Action OnNewHighScore;
        public event Action<int> OnComboChanged;

        // ---------- Cumulative Statistics ----------
        public int TotalFeedEaten { get; private set; }
        public int TotalBlobsAbsorbed { get; private set; }
        public float BestTimeAttackTime { get; private set; }

        // ---------- PlayerPrefs キー ----------
        private const string KEY_HIGH_SCORE = "Blob3D_HighScore";
        private const string KEY_TOTAL_GAMES = "Blob3D_TotalGames";
        private const string KEY_MAX_SIZE = "Blob3D_MaxSize";
        private const string KEY_TOTAL_FEED = "Blob3D_TotalFeedEaten";
        private const string KEY_TOTAL_BLOBS = "Blob3D_TotalBlobsAbsorbed";
        private const string KEY_BEST_TA_TIME = "Blob3D_BestTimeAttackTime";
        private const string KEY_COINS = "Blob3D_Coins";

        private void Awake()
        {
            Instance = this;
            LoadStats();
        }

        private void Start()
        {
            Core.GameManager.Instance.OnGameStart += ResetRoundScore;
        }

        private void Update()
        {
            if (comboTimer > 0f)
            {
                comboTimer -= Time.deltaTime;
                if (comboTimer <= 0f)
                {
                    comboCount = 0;
                    comboTimer = 0f;
                    OnComboChanged?.Invoke(comboCount);
                }
            }
        }

        // ---------- スコア加算 ----------

        /// <summary>エサ吸収時のスコア加算</summary>
        public void AddFeedScore(float nutrition)
        {
            int points = Mathf.RoundToInt(nutrition * feedScoreMultiplier * ComboMultiplier);
            CurrentScore += points;
            FeedEaten++;
            comboCount++;
            if (comboCount > maxComboThisRound) maxComboThisRound = comboCount;
            comboTimer = ComboWindow;
            OnScoreChanged?.Invoke(CurrentScore);
            OnComboChanged?.Invoke(comboCount);
        }

        /// <summary>blob吸収時のスコア加算</summary>
        public void AddBlobScore(float blobSize)
        {
            int points = Mathf.RoundToInt(blobSize * blobScoreMultiplier * ComboMultiplier);
            CurrentScore += points;
            BlobsAbsorbed++;
            comboCount += 2;
            if (comboCount > maxComboThisRound) maxComboThisRound = comboCount;
            comboTimer = ComboWindow;
            OnScoreChanged?.Invoke(CurrentScore);
            OnComboChanged?.Invoke(comboCount);
        }

        /// <summary>プレイヤーの最大サイズを更新</summary>
        public void UpdateMaxSize(float size)
        {
            if (size > MaxSizeReached)
            {
                MaxSizeReached = size;
            }
        }

        // ---------- ラウンド管理 ----------

        private void ResetRoundScore()
        {
            CurrentScore = 0;
            FeedEaten = 0;
            BlobsAbsorbed = 0;
            MaxSizeReached = 1f;
            comboCount = 0;
            maxComboThisRound = 0;
            comboTimer = 0f;
            OnScoreChanged?.Invoke(0);
            OnComboChanged?.Invoke(0);
        }

        /// <summary>ラウンド終了時にハイスコア判定＆保存</summary>
        public bool FinalizeRound()
        {
            TotalGamesPlayed++;
            bool isNewHighScore = false;

            if (CurrentScore > HighScore)
            {
                HighScore = CurrentScore;
                isNewHighScore = true;
                OnNewHighScore?.Invoke();
            }

            // Accumulate cumulative totals
            TotalFeedEaten += FeedEaten;
            TotalBlobsAbsorbed += BlobsAbsorbed;

            // Track best TimeAttack time
            if (Core.GameManager.Instance != null &&
                Core.GameManager.Instance.CurrentMode == Core.GameManager.GameMode.TimeAttack)
            {
                float elapsed = Core.GameManager.Instance.ElapsedTime;
                if (BestTimeAttackTime <= 0f || elapsed < BestTimeAttackTime)
                {
                    BestTimeAttackTime = elapsed;
                }
            }

            // Award coins based on score (10% of score)
            AddCoins(Mathf.RoundToInt(CurrentScore * 0.1f));

            SaveStats();

            // Record score to local leaderboard
            string modeName = Core.GameManager.Instance != null
                ? Core.GameManager.Instance.CurrentMode.ToString()
                : "Classic";
            LastLeaderboardRank = LocalLeaderboard.TryAddScore(CurrentScore, MaxSizeReached, modeName);

            // Check for skin unlocks based on cumulative stats
            SkinManager.Instance?.CheckUnlocks(
                (int)CurrentScore, TotalGamesPlayed,
                MaxSizeReached, BlobsAbsorbed);

            // Check achievements based on cumulative stats
            float survivalTime = 0f;
            float taTime = 0f;
            if (Core.GameManager.Instance != null)
            {
                if (Core.GameManager.Instance.CurrentMode == Core.GameManager.GameMode.Survival)
                    survivalTime = Core.GameManager.Instance.ElapsedTime;
                if (Core.GameManager.Instance.CurrentMode == Core.GameManager.GameMode.TimeAttack)
                    taTime = Core.GameManager.Instance.ElapsedTime;
            }
            Data.AchievementManager.Instance?.CheckAchievements(
                TotalFeedEaten, TotalBlobsAbsorbed, MaxSizeReached,
                CurrentScore, TotalGamesPlayed, maxComboThisRound,
                survivalTime, taTime);

            return isNewHighScore;
        }

        // ---------- サイズランク ----------

        public string GetSizeRank(float size)
        {
            if (size < 2f) return Localization.Get("rank_tiny");
            if (size < 5f) return Localization.Get("rank_small");
            if (size < 20f) return Localization.Get("rank_medium");
            if (size < 50f) return Localization.Get("rank_large");
            return Localization.Get("rank_mega");
        }

        // ---------- 永続化 ----------

        private void LoadStats()
        {
            HighScore = PlayerPrefs.GetInt(KEY_HIGH_SCORE, 0);
            TotalGamesPlayed = PlayerPrefs.GetInt(KEY_TOTAL_GAMES, 0);
            MaxSizeReached = PlayerPrefs.GetFloat(KEY_MAX_SIZE, 1f);
            TotalFeedEaten = PlayerPrefs.GetInt(KEY_TOTAL_FEED, 0);
            TotalBlobsAbsorbed = PlayerPrefs.GetInt(KEY_TOTAL_BLOBS, 0);
            BestTimeAttackTime = PlayerPrefs.GetFloat(KEY_BEST_TA_TIME, 0f);
            coins = PlayerPrefs.GetInt(KEY_COINS, 0);
        }

        private void SaveStats()
        {
            PlayerPrefs.SetInt(KEY_HIGH_SCORE, HighScore);
            PlayerPrefs.SetInt(KEY_TOTAL_GAMES, TotalGamesPlayed);
            PlayerPrefs.SetFloat(KEY_MAX_SIZE, MaxSizeReached);
            PlayerPrefs.SetInt(KEY_TOTAL_FEED, TotalFeedEaten);
            PlayerPrefs.SetInt(KEY_TOTAL_BLOBS, TotalBlobsAbsorbed);
            PlayerPrefs.SetFloat(KEY_BEST_TA_TIME, BestTimeAttackTime);
            PlayerPrefs.Save();
        }

        // ---------- コイン管理 ----------

        /// <summary>Add coins and persist</summary>
        public void AddCoins(int amount)
        {
            coins += amount;
            SaveCoins();
            OnCoinsChanged?.Invoke(coins);
        }

        /// <summary>Spend coins if sufficient balance. Returns false if not enough.</summary>
        public bool SpendCoins(int amount)
        {
            if (coins < amount) return false;
            coins -= amount;
            SaveCoins();
            OnCoinsChanged?.Invoke(coins);
            return true;
        }

        private void SaveCoins()
        {
            PlayerPrefs.SetInt(KEY_COINS, coins);
            PlayerPrefs.Save();
        }

        private void OnDestroy()
        {
            if (Core.GameManager.Instance != null)
                Core.GameManager.Instance.OnGameStart -= ResetRoundScore;
        }
    }
}
