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

        // Tracks spawned face part GameObjects for cleanup on skin switch
        private readonly List<GameObject> activeFaceParts = new List<GameObject>();

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

        /// <summary>Apply skin visuals including material and face parts</summary>
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

            ApplyFaceParts(skin);
        }

        /// <summary>Create face part sprites (eyes, mouth) from skin data</summary>
        private void ApplyFaceParts(SkinData skin)
        {
            // Remove previous face parts
            foreach (var part in activeFaceParts)
            {
                if (part != null) Destroy(part);
            }
            activeFaceParts.Clear();

            Transform parent = playerRenderer.transform;

            // Eyes
            if (skin.eyeSprite != null)
            {
                // Left eye
                var leftEye = CreateFacePartSprite("Eye_L", skin.eyeSprite, parent,
                    new Vector3(-skin.eyeOffset.x, skin.eyeOffset.y, -0.51f), skin.eyeScale);
                activeFaceParts.Add(leftEye);

                // Right eye
                var rightEye = CreateFacePartSprite("Eye_R", skin.eyeSprite, parent,
                    new Vector3(skin.eyeOffset.x, skin.eyeOffset.y, -0.51f), skin.eyeScale);
                activeFaceParts.Add(rightEye);
            }

            // Mouth
            if (skin.mouthSprite != null)
            {
                var mouth = CreateFacePartSprite("Mouth", skin.mouthSprite, parent,
                    new Vector3(0f, -skin.eyeOffset.y * 0.5f, -0.51f), skin.mouthScale);
                activeFaceParts.Add(mouth);
            }
        }

        private GameObject CreateFacePartSprite(string partName, Sprite sprite, Transform parent,
            Vector3 localPos, float scale)
        {
            var go = new GameObject($"FacePart_{partName}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * scale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 1;

            return go;
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
            // Fix: null check on allSkins to prevent NullReferenceException
            if (allSkins == null) return;
            foreach (var skin in allSkins)
            {
                if (IsSkinUnlocked(skin)) continue;

                bool shouldUnlock = skin.unlockType switch
                {
                    SkinData.UnlockType.ScoreReached    => totalScore >= skin.unlockThreshold,
                    SkinData.UnlockType.GamesPlayed     => gamesPlayed >= skin.unlockThreshold,
                    SkinData.UnlockType.MaxSizeReached  => maxSize >= skin.unlockThreshold,
                    SkinData.UnlockType.BlobsAbsorbed   => blobsAbsorbed >= skin.unlockThreshold,
                    // SpecialChallenge skins are unlocked manually via UnlockSpecialSkin()
                    SkinData.UnlockType.SpecialChallenge => false,
                    _ => false
                };

                if (shouldUnlock)
                {
                    UnlockSkin(skin);
                }
            }
        }

        /// <summary>Manually unlock a special challenge skin by name</summary>
        public bool UnlockSpecialSkin(string skinName)
        {
            if (allSkins == null) return false;

            foreach (var skin in allSkins)
            {
                if (skin.skinName == skinName && !IsSkinUnlocked(skin))
                {
                    UnlockSkin(skin);
                    return true;
                }
            }
            return false;
        }

        public void UnlockSkin(SkinData skin)
        {
            unlockedSkins.Add(skin.skinName);
            PlayerPrefs.SetInt(KEY_UNLOCKED_PREFIX + skin.skinName, 1);
            PlayerPrefs.Save();

            OnSkinUnlocked?.Invoke(skin);
            Debug.Log($"Skin unlocked: {skin.skinName}");
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
