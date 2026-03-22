using UnityEngine;
using System;
using Blob3D.Gameplay;

namespace Blob3D.Data
{
    public class DailyRewardManager : MonoBehaviour
    {
        public static DailyRewardManager Instance { get; private set; }

        public event Action<int, int> OnRewardClaimed; // day, coins

        private const string KeyLastClaim = "Blob3D_DailyLastClaim";
        private const string KeyStreak = "Blob3D_DailyStreak";

        public int CurrentStreak => PlayerPrefs.GetInt(KeyStreak, 0);
        public bool CanClaimToday => !HasClaimedToday();

        // Rewards per day (coins): day1=10, day2=20, ... day7=100, then repeat
        private static readonly int[] rewards = { 10, 20, 30, 40, 50, 75, 100 };

        public int TodayReward
        {
            get
            {
                int streak = CurrentStreak % rewards.Length;
                return rewards[streak];
            }
        }

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            CheckStreakReset();
        }

        private bool HasClaimedToday()
        {
            string lastClaim = PlayerPrefs.GetString(KeyLastClaim, "");
            return lastClaim == DateTime.Now.ToString("yyyyMMdd");
        }

        private void CheckStreakReset()
        {
            string lastClaim = PlayerPrefs.GetString(KeyLastClaim, "");
            if (string.IsNullOrEmpty(lastClaim)) return;

            if (DateTime.TryParseExact(lastClaim, "yyyyMMdd", null,
                System.Globalization.DateTimeStyles.None, out DateTime lastDate))
            {
                int daysSince = (DateTime.Now.Date - lastDate.Date).Days;
                if (daysSince > 1)
                {
                    // Streak broken — reset
                    PlayerPrefs.SetInt(KeyStreak, 0);
                    PlayerPrefs.Save();
                }
            }
        }

        public bool ClaimReward()
        {
            if (!CanClaimToday) return false;

            int reward = TodayReward;
            int newStreak = CurrentStreak + 1;

            PlayerPrefs.SetString(KeyLastClaim, DateTime.Now.ToString("yyyyMMdd"));
            PlayerPrefs.SetInt(KeyStreak, newStreak);
            PlayerPrefs.Save();

            // Add coins
            ScoreManager.Instance?.AddCoins(reward);

            OnRewardClaimed?.Invoke(newStreak, reward);
            return true;
        }
    }
}
