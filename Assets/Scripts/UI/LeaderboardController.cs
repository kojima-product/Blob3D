using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using Blob3D.Core;
using Blob3D.AI;
using Blob3D.Player;
using Blob3D.Utils;

namespace Blob3D.UI
{
    /// <summary>
    /// Real-time leaderboard showing top 5 blobs by size.
    /// Updates every 0.5 seconds for performance.
    /// </summary>
    public class LeaderboardController : MonoBehaviour
    {
        [SerializeField] private float updateInterval = 0.5f;
        [SerializeField] private float rankFlashDuration = 0.5f;

        private TextMeshProUGUI[] rankTexts;
        private const int MaxEntries = 5;
        private float updateTimer;
        private int previousPlayerRank = -1;
        private Coroutine rankFlashCoroutine;

        // Random AI names
        private static readonly string[] aiNames = {
            "Slimo", "Gooey", "Blobby", "Squish", "Jello",
            "Puddi", "Wobble", "Glorp", "Slimy", "Bouncy",
            "Mushy", "Drippy", "Goopy", "Sticky", "Stretchy",
            "Chunky", "Flubber", "Gummy", "Marshy", "Oozy",
            "Plop", "Bloop", "Splat", "Gulp", "Chomp",
            "Mochi", "Neru", "Puni", "Toro", "Puyo"
        };

        private Dictionary<AIBlobController, string> aiNameMap = new Dictionary<AIBlobController, string>();

        private void Start()
        {
            // Create UI elements dynamically
            CreateLeaderboardUI();
        }

        private void Update()
        {
            if (GameManager.Instance == null ||
                GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;

            updateTimer -= Time.deltaTime;
            if (updateTimer <= 0f)
            {
                updateTimer = updateInterval;
                CleanStaleEntries();
                UpdateLeaderboard();
            }
        }

        private void CreateLeaderboardUI()
        {
            // This creates a simple vertical list in the top-right area
            rankTexts = new TextMeshProUGUI[MaxEntries];

            for (int i = 0; i < MaxEntries; i++)
            {
                GameObject textObj = new GameObject($"Rank_{i}");
                textObj.transform.SetParent(transform, false);
                RectTransform rt = textObj.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(1, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(1, 1);
                rt.anchoredPosition = new Vector2(-20, -170 - i * 32);
                rt.sizeDelta = new Vector2(220, 28);

                var tmp = textObj.AddComponent<TextMeshProUGUI>();
                tmp.fontSize = 20;
                tmp.alignment = TextAlignmentOptions.Right;
                tmp.color = new Color(0.7f, 0.75f, 0.82f);
                tmp.raycastTarget = false;
                rankTexts[i] = tmp;
            }
        }

        private void UpdateLeaderboard()
        {
            var entries = new List<(string name, float size, bool isPlayer)>();

            // Add player
            if (BlobController.Instance != null && BlobController.Instance.IsAlive)
            {
                entries.Add((Localization.Get("lb_you"), BlobController.Instance.CurrentSize, true));
            }

            // Add AI blobs
            if (AISpawner.Instance?.ActiveAIs != null)
            {
                foreach (var ai in AISpawner.Instance.ActiveAIs)
                {
                    if (ai != null && ai.IsAlive)
                    {
                        string name = GetAIName(ai);
                        entries.Add((name, ai.CurrentSize, false));
                    }
                }
            }

            // Sort by size descending
            entries.Sort((a, b) => b.size.CompareTo(a.size));

            // Update UI
            for (int i = 0; i < MaxEntries; i++)
            {
                if (i < entries.Count)
                {
                    var entry = entries[i];
                    string sizeStr = entry.size.ToString("F1");
                    rankTexts[i].text = $"{i + 1}. {entry.name}  {sizeStr}";

                    if (entry.isPlayer)
                    {
                        rankTexts[i].color = new Color(0.2f, 0.8f, 1f); // Accent color for player
                        rankTexts[i].fontStyle = FontStyles.Bold;

                        int currentRank = i + 1;
                        if (previousPlayerRank != -1 && currentRank != previousPlayerRank)
                        {
                            // Rank UP: flash green; Rank DOWN: flash red
                            Color flashColor = currentRank < previousPlayerRank
                                ? new Color(0.2f, 1f, 0.3f) // Green for rank up
                                : new Color(1f, 0.3f, 0.2f); // Red for rank down

                            if (rankFlashCoroutine != null)
                            {
                                StopCoroutine(rankFlashCoroutine);
                            }
                            rankFlashCoroutine = StartCoroutine(FlashRankColor(rankTexts[i], flashColor));
                        }
                        previousPlayerRank = currentRank;
                    }
                    else
                    {
                        rankTexts[i].color = new Color(0.7f, 0.75f, 0.82f);
                        rankTexts[i].fontStyle = FontStyles.Normal;
                    }

                    rankTexts[i].gameObject.SetActive(true);
                }
                else
                {
                    rankTexts[i].gameObject.SetActive(false);
                }
            }
        }

        private string GetAIName(AIBlobController ai)
        {
            if (!aiNameMap.ContainsKey(ai))
            {
                aiNameMap[ai] = aiNames[Random.Range(0, aiNames.Length)];
            }
            return aiNameMap[ai];
        }

        /// <summary>Remove entries for destroyed or inactive AI references</summary>
        private void CleanStaleEntries()
        {
            var staleKeys = new List<AIBlobController>();
            foreach (var kvp in aiNameMap)
            {
                if (kvp.Key == null || !kvp.Key.IsAlive)
                {
                    staleKeys.Add(kvp.Key);
                }
            }
            foreach (var key in staleKeys)
            {
                aiNameMap.Remove(key);
            }
        }

        /// <summary>
        /// Flash the player's rank text to a highlight color then fade back to accent blue.
        /// Green for rank up, red for rank down.
        /// </summary>
        private System.Collections.IEnumerator FlashRankColor(TextMeshProUGUI text, Color flashColor)
        {
            Color accentColor = new Color(0.2f, 0.8f, 1f);
            float elapsed = 0f;

            // Set flash color immediately
            text.color = flashColor;

            // Also pulse scale for emphasis
            while (elapsed < rankFlashDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / rankFlashDuration);
                text.color = Color.Lerp(flashColor, accentColor, t);
                float scale = 1f + 0.2f * Mathf.Sin(t * Mathf.PI);
                text.transform.localScale = Vector3.one * scale;
                yield return null;
            }

            text.color = accentColor;
            text.transform.localScale = Vector3.one;
            rankFlashCoroutine = null;
        }

        private System.Collections.IEnumerator PulseText(TextMeshProUGUI text)
        {
            float dur = 0.3f;
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                float scale = 1f + 0.3f * Mathf.Sin(t * Mathf.PI);
                text.transform.localScale = Vector3.one * scale;
                yield return null;
            }
            text.transform.localScale = Vector3.one;
        }
    }
}
