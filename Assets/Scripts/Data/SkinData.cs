using UnityEngine;

namespace Blob3D.Data
{
    /// <summary>
    /// ScriptableObjectでスキンデータを管理。
    /// Unityエディタ上で Create > Blob3D > SkinData で作成可能。
    /// </summary>
    [CreateAssetMenu(fileName = "NewSkin", menuName = "Blob3D/SkinData")]
    public class SkinData : ScriptableObject
    {
        [Header("Basic Info")]
        public string skinName;
        public string description;
        public Sprite icon;

        [Header("Visual")]
        public Color primaryColor = Color.white;
        public Color secondaryColor = Color.gray;
        public Material material;        // 専用マテリアル（null なら色だけ変更）
        public Texture2D pattern;        // 模様テクスチャ

        [Header("Face Parts")]
        public Sprite eyeSprite;
        public Sprite mouthSprite;
        public Vector2 eyeOffset = new Vector2(0.2f, 0.15f);
        public float eyeScale = 0.3f;
        public float mouthScale = 0.2f;

        [Header("Shop")]
        public int coinCost = 100;

        [Header("Unlock Conditions")]
        public UnlockType unlockType;
        public int unlockThreshold;     // スコア、プレイ回数などの必要値
        public string unlockDescription;

        public enum UnlockType
        {
            Default,             // 最初から使用可能
            ScoreReached,        // 累計スコアで解放
            GamesPlayed,         // プレイ回数で解放
            MaxSizeReached,      // 最大サイズで解放
            BlobsAbsorbed,       // 吸収数で解放
            SpecialChallenge     // 特殊条件
        }
    }
}
