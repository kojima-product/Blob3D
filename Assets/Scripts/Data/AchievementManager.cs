using UnityEngine;
using System;
using System.Collections.Generic;

namespace Blob3D.Data
{
    public class AchievementManager : MonoBehaviour
    {
        public static AchievementManager Instance { get; private set; }

        public event Action<Achievement> OnAchievementUnlocked;

        [System.Serializable]
        public class Achievement
        {
            public string id;
            public string title;
            public string description;
            public bool unlocked;
        }

        private List<Achievement> achievements = new List<Achievement>();

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            InitializeAchievements();
            LoadAchievements();
        }

        public IReadOnlyList<Achievement> AllAchievements => achievements;
        public int UnlockedCount { get { int c = 0; foreach (var a in achievements) if (a.unlocked) c++; return c; } }

        private void InitializeAchievements()
        {
            achievements.Add(new Achievement { id = "first_feed", title = "First Bite", description = "Eat your first feed" });
            achievements.Add(new Achievement { id = "feed_100", title = "Big Eater", description = "Eat 100 feed" });
            achievements.Add(new Achievement { id = "feed_500", title = "Glutton", description = "Eat 500 feed" });
            achievements.Add(new Achievement { id = "first_absorb", title = "First Prey", description = "Absorb your first blob" });
            achievements.Add(new Achievement { id = "absorb_10", title = "Hunter", description = "Absorb 10 blobs" });
            achievements.Add(new Achievement { id = "absorb_50", title = "Apex Predator", description = "Absorb 50 blobs" });
            achievements.Add(new Achievement { id = "size_10", title = "Growing Up", description = "Reach size 10" });
            achievements.Add(new Achievement { id = "size_30", title = "Giant", description = "Reach size 30" });
            achievements.Add(new Achievement { id = "size_50", title = "Mega Blob", description = "Reach size 50" });
            achievements.Add(new Achievement { id = "score_1000", title = "Scorer", description = "Reach a score of 1,000" });
            achievements.Add(new Achievement { id = "score_10000", title = "High Scorer", description = "Reach a score of 10,000" });
            achievements.Add(new Achievement { id = "games_10", title = "Regular", description = "Play 10 games" });
            achievements.Add(new Achievement { id = "games_50", title = "Addicted", description = "Play 50 games" });
            achievements.Add(new Achievement { id = "combo_5", title = "Combo Master", description = "Achieve a 5x combo" });
            achievements.Add(new Achievement { id = "combo_10", title = "Chain Expert", description = "Achieve a 10x combo" });
            achievements.Add(new Achievement { id = "survival_60", title = "Survivor", description = "Survive 60 seconds in Survival mode" });
            achievements.Add(new Achievement { id = "timeattack_60", title = "Speed Star", description = "Complete Time Attack in under 60 seconds" });
            achievements.Add(new Achievement { id = "first_powerup", title = "Power Up!", description = "Collect your first power-up" });
        }

        private void LoadAchievements()
        {
            foreach (var a in achievements)
                a.unlocked = PlayerPrefs.GetInt($"Blob3D_Ach_{a.id}", 0) == 1;
        }

        public void CheckAchievements(int totalFeed, int totalBlobs, float maxSize,
            int totalScore, int gamesPlayed, int maxCombo, float survivalTime, float taTime)
        {
            TryUnlock("first_feed", totalFeed >= 1);
            TryUnlock("feed_100", totalFeed >= 100);
            TryUnlock("feed_500", totalFeed >= 500);
            TryUnlock("first_absorb", totalBlobs >= 1);
            TryUnlock("absorb_10", totalBlobs >= 10);
            TryUnlock("absorb_50", totalBlobs >= 50);
            TryUnlock("size_10", maxSize >= 10);
            TryUnlock("size_30", maxSize >= 30);
            TryUnlock("size_50", maxSize >= 50);
            TryUnlock("score_1000", totalScore >= 1000);
            TryUnlock("score_10000", totalScore >= 10000);
            TryUnlock("games_10", gamesPlayed >= 10);
            TryUnlock("games_50", gamesPlayed >= 50);
            TryUnlock("combo_5", maxCombo >= 5);
            TryUnlock("combo_10", maxCombo >= 10);
            TryUnlock("survival_60", survivalTime >= 60f);
            TryUnlock("timeattack_60", taTime > 0 && taTime <= 60f);
        }

        // Call this for one-off events during gameplay
        public void CheckInstant(string id)
        {
            TryUnlock(id, true);
        }

        private void TryUnlock(string id, bool condition)
        {
            if (!condition) return;
            var ach = achievements.Find(a => a.id == id);
            if (ach == null || ach.unlocked) return;
            ach.unlocked = true;
            PlayerPrefs.SetInt($"Blob3D_Ach_{id}", 1);
            PlayerPrefs.Save();
            OnAchievementUnlocked?.Invoke(ach);
        }
    }
}
