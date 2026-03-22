using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using Blob3D.Core;
using Blob3D.AI;
using Blob3D.Player;

namespace Blob3D.UI
{
    /// <summary>
    /// Real-time leaderboard showing top 5 blobs by size.
    /// Updates every 0.5 seconds for performance.
    /// </summary>
    public class LeaderboardController : MonoBehaviour
    {
        [SerializeField] private float updateInterval = 0.5f;

        private TextMeshProUGUI[] rankTexts;
        private const int MaxEntries = 5;
        private float updateTimer;
        private int previousPlayerRank = -1;

        // Random AI names
        private static readonly string[] aiNames = {
            "Slimo", "Gooey", "Blobby", "Squish", "Jello",
            "Puddi", "Wobble", "Glorp", "Slimy", "Bouncy",
            "Mushy", "Drippy", "Goopy", "Sticky", "Stretchy",
            "Chunky", "Flubber", "Gummy", "Marshy", "Oozy",
            "Plop", "Bloop", "Splat", "Gulp", "Chomp",
            "Mochi", "Neru", "Puni", "Toro", "Puyo"
        };

        private Dictionary<int, string> aiNameMap = new Dictionary<int, string>();

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
                entries.Add(("YOU", BlobController.Instance.CurrentSize, true));
            }

            // Add AI blobs
            if (AISpawner.Instance?.ActiveAIs != null)
            {
                foreach (var ai in AISpawner.Instance.ActiveAIs)
                {
                    if (ai != null && ai.IsAlive)
                    {
                        string name = GetAIName(ai.GetInstanceID());
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

                        // Rank change highlight
                        if (previousPlayerRank != -1 && i + 1 < previousPlayerRank)
                        {
                            // Player moved up — brief pulse
                            StartCoroutine(PulseText(rankTexts[i]));
                        }
                        previousPlayerRank = i + 1;
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

        private string GetAIName(int instanceId)
        {
            if (!aiNameMap.ContainsKey(instanceId))
            {
                aiNameMap[instanceId] = aiNames[Random.Range(0, aiNames.Length)];
            }
            return aiNameMap[instanceId];
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
