using UnityEngine;
using System.Collections.Generic;

namespace Blob3D.Data
{
    /// <summary>
    /// スキンの所持・選択・アンロック状態を管理。
    /// </summary>
    public class SkinManager : MonoBehaviour
    {
        public static SkinManager Instance { get; private set; }

        [Header("All Skins")]
        [SerializeField] private SkinData[] allSkins;

        [Header("References")]
        [SerializeField] private Renderer playerRenderer;

        public event System.Action<SkinData> OnSkinUnlocked;

        public SkinData CurrentSkin { get; private set; }
        public IReadOnlyList<SkinData> AllSkins => allSkins;

        private HashSet<string> unlockedSkins = new HashSet<string>();
        private const string KEY_SELECTED_SKIN = "Blob3D_SelectedSkin";
        private const string KEY_UNLOCKED_PREFIX = "Blob3D_Unlocked_";

        private void Awake()
        {
            Instance = this;
            LoadUnlockState();
        }

        // ---------- スキン選択 ----------

        /// <summary>スキンを選択して適用</summary>
        public void SelectSkin(SkinData skin)
        {
            if (!IsSkinUnlocked(skin)) return;

            CurrentSkin = skin;
            ApplySkin(skin);
            PlayerPrefs.SetString(KEY_SELECTED_SKIN, skin.skinName);
            PlayerPrefs.Save();
        }

        /// <summary>プレイヤーのRendererにスキンを適用</summary>
        public void ApplySkin(SkinData skin)
        {
            if (playerRenderer == null) return;

            if (skin.material != null)
            {
                playerRenderer.material = skin.material;
            }
            else
            {
                playerRenderer.material.color = skin.primaryColor;
            }

            if (skin.pattern != null)
            {
                playerRenderer.material.mainTexture = skin.pattern;
            }
        }

        // ---------- アンロック ----------

        public bool IsSkinUnlocked(SkinData skin)
        {
            if (skin.unlockType == SkinData.UnlockType.Default) return true;
            return unlockedSkins.Contains(skin.skinName);
        }

        /// <summary>ゲーム状況に応じてアンロック判定を行う</summary>
        public void CheckUnlocks(int totalScore, int gamesPlayed, float maxSize, int blobsAbsorbed)
        {
            foreach (var skin in allSkins)
            {
                if (IsSkinUnlocked(skin)) continue;

                bool shouldUnlock = skin.unlockType switch
                {
                    SkinData.UnlockType.ScoreReached   => totalScore >= skin.unlockThreshold,
                    SkinData.UnlockType.GamesPlayed    => gamesPlayed >= skin.unlockThreshold,
                    SkinData.UnlockType.MaxSizeReached  => maxSize >= skin.unlockThreshold,
                    SkinData.UnlockType.BlobsAbsorbed   => blobsAbsorbed >= skin.unlockThreshold,
                    _ => false
                };

                if (shouldUnlock)
                {
                    UnlockSkin(skin);
                }
            }
        }

        public void UnlockSkin(SkinData skin)
        {
            unlockedSkins.Add(skin.skinName);
            PlayerPrefs.SetInt(KEY_UNLOCKED_PREFIX + skin.skinName, 1);
            PlayerPrefs.Save();

            OnSkinUnlocked?.Invoke(skin);
            Debug.Log($"スキンアンロック: {skin.skinName}");
        }

        // ---------- 永続化 ----------

        private void LoadUnlockState()
        {
            if (allSkins == null) return;

            foreach (var skin in allSkins)
            {
                if (skin.unlockType == SkinData.UnlockType.Default ||
                    PlayerPrefs.GetInt(KEY_UNLOCKED_PREFIX + skin.skinName, 0) == 1)
                {
                    unlockedSkins.Add(skin.skinName);
                }
            }

            // 前回選択したスキンを復元
            string selected = PlayerPrefs.GetString(KEY_SELECTED_SKIN, "");
            if (!string.IsNullOrEmpty(selected))
            {
                foreach (var skin in allSkins)
                {
                    if (skin.skinName == selected)
                    {
                        CurrentSkin = skin;
                        return;
                    }
                }
            }

            // デフォルトスキン
            if (allSkins.Length > 0)
            {
                CurrentSkin = allSkins[0];
            }
        }
    }
}
