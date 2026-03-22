using UnityEngine;
using System.Collections.Generic;

namespace Blob3D.Utils
{
    /// <summary>
    /// Simple JP/EN localization. Strings stored in code (no external files needed).
    /// </summary>
    public static class Localization
    {
        public enum Language { Japanese, English }

        private static Language currentLanguage = Language.English;
        public static Language CurrentLanguage => currentLanguage;

        private const string KeyLanguage = "Blob3D_Language";

        public static event System.Action OnLanguageChanged;

        private static readonly Dictionary<string, string[]> strings = new Dictionary<string, string[]>
        {
            // Key -> [Japanese, English]
            // Title
            { "title_subtitle", new[] { "\u98df\u3079\u3066 \u6210\u9577\u3057\u3066 \u652f\u914d\u3057\u308d", "EAT  GROW  DOMINATE" } },
            { "btn_play", new[] { "\u30d7\u30ec\u30a4", "PLAY" } },
            { "btn_skins", new[] { "\u30b9\u30ad\u30f3", "SKINS" } },
            { "btn_stats", new[] { "\u7d71\u8a08", "STATS" } },
            { "btn_shop", new[] { "\u30b7\u30e7\u30c3\u30d7", "SHOP" } },
            { "btn_settings", new[] { "\u8a2d\u5b9a", "SETTINGS" } },
            { "btn_back", new[] { "\u623b\u308b", "BACK" } },
            { "best_score", new[] { "\u30d9\u30b9\u30c8: {0}", "BEST: {0}" } },

            // Game Modes
            { "mode_classic", new[] { "\u30af\u30e9\u30b7\u30c3\u30af", "CLASSIC" } },
            { "mode_survival", new[] { "\u30b5\u30d0\u30a4\u30d0\u30eb", "SURVIVAL" } },
            { "mode_timeattack", new[] { "\u30bf\u30a4\u30e0\u30a2\u30bf\u30c3\u30af", "TIME ATTACK" } },

            // HUD
            { "rank_tiny", new[] { "\u6975\u5c0f", "Tiny" } },
            { "rank_small", new[] { "\u5c0f", "Small" } },
            { "rank_medium", new[] { "\u4e2d", "Medium" } },
            { "rank_large", new[] { "\u5927", "Large" } },
            { "rank_mega", new[] { "\u30e1\u30ac", "Mega" } },

            // Result
            { "result_title", new[] { "\u30b2\u30fc\u30e0\u30aa\u30fc\u30d0\u30fc", "GAME OVER" } },
            { "result_feed", new[] { "\u30a8\u30b5", "FEED" } },
            { "result_blobs", new[] { "\u30d6\u30ed\u30d6", "BLOBS" } },
            { "result_maxsize", new[] { "\u6700\u5927\u30b5\u30a4\u30ba", "MAX SIZE" } },
            { "result_newhigh", new[] { "\u65b0\u8a18\u9332\uff01", "NEW HIGH SCORE!" } },
            { "result_coins", new[] { "+{0} \u30b3\u30a4\u30f3", "+{0} COINS" } },
            { "btn_retry", new[] { "\u30ea\u30c8\u30e9\u30a4", "RETRY" } },
            { "btn_home", new[] { "\u30db\u30fc\u30e0", "HOME" } },

            // Pause
            { "pause_title", new[] { "\u30dd\u30fc\u30ba", "PAUSED" } },
            { "btn_resume", new[] { "\u518d\u958b", "RESUME" } },

            // Settings
            { "settings_bgm", new[] { "BGM \u97f3\u91cf", "BGM Volume" } },
            { "settings_se", new[] { "SE \u97f3\u91cf", "SE Volume" } },
            { "settings_sensitivity", new[] { "\u611f\u5ea6", "Sensitivity" } },
            { "settings_language", new[] { "\u8a00\u8a9e", "Language" } },

            // Tutorial
            { "tutorial_1", new[] { "\u30b8\u30e7\u30a4\u30b9\u30c6\u30a3\u30c3\u30af\u3092\u30c9\u30e9\u30c3\u30b0\u3057\u3066\n\u79fb\u52d5\u3057\u3088\u3046", "Drag the joystick\nto move" } },
            { "tutorial_2", new[] { "\u30a8\u30b5\u3092\u98df\u3079\u3066\n\u5927\u304d\u304f\u306a\u308d\u3046\uff01", "Eat feed\nto grow!" } },
            { "tutorial_3", new[] { "\u81ea\u5206\u3088\u308a\u5c0f\u3055\u3044Blob\u3092\n\u5438\u53ce\u3057\u3088\u3046\uff01", "Absorb blobs\nsmaller than you!" } },
            { "tutorial_tap", new[] { "\u30bf\u30c3\u30d7\u3057\u3066\u6b21\u3078", "Tap to continue" } },

            // Daily Reward
            { "daily_title", new[] { "\u30c7\u30a4\u30ea\u30fc\u30dc\u30fc\u30ca\u30b9", "DAILY BONUS" } },
            { "daily_claim", new[] { "\u53d7\u3051\u53d6\u308b", "CLAIM" } },
            { "daily_day", new[] { "{0}\u65e5\u76ee", "Day {0}" } },
            { "daily_coins", new[] { "{0} \u30b3\u30a4\u30f3", "{0} Coins" } },

            // Shop
            { "shop_coins", new[] { "\u30b3\u30a4\u30f3: {0}", "COINS: {0}" } },
            { "shop_equipped", new[] { "\u88c5\u5099\u4e2d", "EQUIPPED" } },
            { "shop_select", new[] { "\u9078\u629e", "SELECT" } },

            // Achievements
            { "ach_unlocked", new[] { "\u5b9f\u7e3e\u89e3\u9664\uff01", "ACHIEVEMENT UNLOCKED!" } },

            // Loading
            { "loading", new[] { "\u8aad\u307f\u8fbc\u307f\u4e2d...", "LOADING..." } },

            // Leaderboard (in-game)
            { "lb_you", new[] { "\u3042\u306a\u305f", "YOU" } },

            // Stats
            { "stats_title", new[] { "\u7d71\u8a08", "STATISTICS" } },
            { "stats_games", new[] { "\u30d7\u30ec\u30a4\u56de\u6570", "Games Played" } },
            { "stats_highscore", new[] { "\u30cf\u30a4\u30b9\u30b3\u30a2", "High Score" } },
            { "stats_totalfeed", new[] { "\u901a\u7b97\u30a8\u30b5", "Total Feed" } },
            { "stats_totalblobs", new[] { "\u901a\u7b97\u30d6\u30ed\u30d6", "Total Blobs" } },
            { "stats_maxsize", new[] { "\u6700\u5927\u30b5\u30a4\u30ba", "Max Size" } },
            { "stats_bestta", new[] { "\u30d9\u30b9\u30c8TA", "Best TA" } },
            { "stats_topscores", new[] { "\u30c8\u30c3\u30d7\u30b9\u30b3\u30a2", "TOP SCORES" } },
            { "stats_noscores", new[] { "\u307e\u3060\u30b9\u30b3\u30a2\u304c\u3042\u308a\u307e\u305b\u3093", "No scores yet" } },

            // Result extras
            { "result_rank", new[] { "#{0}\u4f4d\uff01", "RANK #{0}!" } },
            { "result_leaderboard", new[] { "\u30ea\u30fc\u30c0\u30fc\u30dc\u30fc\u30c9: #{0}", "LEADERBOARD: #{0}" } },
            { "coins_earned", new[] { "+{0} \u30b3\u30a4\u30f3\u7372\u5f97", "+{0} COINS EARNED" } },

            // Notifications
            { "skin_unlocked", new[] { "\u65b0\u30b9\u30ad\u30f3: {0}\uff01", "NEW SKIN: {0}!" } },
            { "ach_notification", new[] { "\u5b9f\u7e3e: {0}\uff01", "ACHIEVEMENT: {0}!" } },

            // Daily Reward
            { "daily_desc", new[] { "{0}\u65e5\u76ee\n+{1} \u30b3\u30a4\u30f3", "Day {0}\n+{1} COINS" } },

            // HUD
            { "hud_dash", new[] { "\u30c0\u30c3\u30b7\u30e5", "DASH" } },
        };

        public static void Initialize()
        {
            int saved = PlayerPrefs.GetInt(KeyLanguage, -1);
            if (saved >= 0)
            {
                currentLanguage = (Language)saved;
            }
            else
            {
                // Auto-detect: Japanese if system language is Japanese
                currentLanguage = Application.systemLanguage == SystemLanguage.Japanese
                    ? Language.Japanese : Language.English;
            }
        }

        public static void SetLanguage(Language lang)
        {
            currentLanguage = lang;
            PlayerPrefs.SetInt(KeyLanguage, (int)lang);
            PlayerPrefs.Save();
            OnLanguageChanged?.Invoke();
        }

        public static void ToggleLanguage()
        {
            SetLanguage(currentLanguage == Language.Japanese ? Language.English : Language.Japanese);
        }

        /// <summary>Get localized string by key</summary>
        public static string Get(string key)
        {
            if (strings.TryGetValue(key, out string[] vals))
            {
                int idx = (int)currentLanguage;
                return idx < vals.Length ? vals[idx] : vals[0];
            }
            return $"[{key}]"; // Fallback shows key name
        }

        /// <summary>Get localized string with format args</summary>
        public static string Get(string key, params object[] args)
        {
            return string.Format(Get(key), args);
        }
    }
}
