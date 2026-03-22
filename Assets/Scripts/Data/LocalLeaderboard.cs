using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Blob3D.Data
{
    /// <summary>
    /// Stores top 10 scores locally using PlayerPrefs.
    /// Each entry: score, size, date, game mode.
    /// </summary>
    public static class LocalLeaderboard
    {
        private const string KeyPrefix = "Blob3D_LB_";
        private const int MaxEntries = 10;

        [System.Serializable]
        public class Entry
        {
            public int score;
            public float maxSize;
            public string date;
            public string mode;
        }

        public static List<Entry> GetTopScores()
        {
            var entries = new List<Entry>();
            for (int i = 0; i < MaxEntries; i++)
            {
                string json = PlayerPrefs.GetString($"{KeyPrefix}{i}", "");
                if (!string.IsNullOrEmpty(json))
                {
                    entries.Add(JsonUtility.FromJson<Entry>(json));
                }
            }
            return entries.OrderByDescending(e => e.score).ToList();
        }

        /// <summary>Try to add a score. Returns rank (1-10) if it made the board, 0 otherwise.</summary>
        public static int TryAddScore(int score, float maxSize, string mode)
        {
            var entries = GetTopScores();
            var newEntry = new Entry
            {
                score = score,
                maxSize = maxSize,
                date = System.DateTime.Now.ToString("MM/dd"),
                mode = mode
            };

            entries.Add(newEntry);
            entries = entries.OrderByDescending(e => e.score).Take(MaxEntries).ToList();

            // Save
            for (int i = 0; i < entries.Count; i++)
            {
                PlayerPrefs.SetString($"{KeyPrefix}{i}", JsonUtility.ToJson(entries[i]));
            }
            // Clear any slots beyond count
            for (int i = entries.Count; i < MaxEntries; i++)
            {
                PlayerPrefs.DeleteKey($"{KeyPrefix}{i}");
            }
            PlayerPrefs.Save();

            // Fix: use score+date match instead of reference equality (entries list was recreated via Sort/Take)
            int rank = entries.FindIndex(e => e.score == newEntry.score && e.date == newEntry.date && e.mode == newEntry.mode) + 1;
            return rank > 0 && rank <= MaxEntries ? rank : 0;
        }
    }
}
