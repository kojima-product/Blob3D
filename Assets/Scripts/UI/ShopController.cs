using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Blob3D.Data;
using Blob3D.Gameplay;

namespace Blob3D.UI
{
    /// <summary>
    /// Shop UI for purchasing skins with coins.
    /// Shows available skins with prices and purchase buttons.
    /// </summary>
    public class ShopController : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI coinDisplay;
        [SerializeField] private Transform contentParent;
        private readonly List<GameObject> shopItems = new List<GameObject>();

        private void OnEnable()
        {
            RefreshShop();
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.OnCoinsChanged += UpdateCoinDisplay;
        }

        private void OnDisable()
        {
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.OnCoinsChanged -= UpdateCoinDisplay;
        }

        public void Initialize(TextMeshProUGUI coinText, Transform content)
        {
            coinDisplay = coinText;
            contentParent = content;
        }

        private void UpdateCoinDisplay(int coins)
        {
            if (coinDisplay != null)
                coinDisplay.text = $"COINS: {coins}";
        }

        public void RefreshShop()
        {
            if (contentParent == null || SkinManager.Instance == null) return;

            // Clear existing items
            foreach (var item in shopItems)
                Destroy(item);
            shopItems.Clear();

            // Update coin display
            if (coinDisplay != null && ScoreManager.Instance != null)
                coinDisplay.text = $"COINS: {ScoreManager.Instance.Coins}";

            // Create shop items for each skin
            var skins = SkinManager.Instance.AllSkins;
            if (skins == null) return;

            for (int i = 0; i < skins.Count; i++)
            {
                var skin = skins[i];
                if (skin == null) continue;

                GameObject item = CreateShopItem(skin, i);
                shopItems.Add(item);
            }
        }

        private GameObject CreateShopItem(SkinData skin, int index)
        {
            GameObject itemObj = new GameObject($"ShopItem_{skin.skinName}");
            itemObj.transform.SetParent(contentParent, false);

            RectTransform rt = itemObj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(320, 100);
            rt.anchoredPosition = new Vector2(0, -index * 110);

            // Background
            Image bg = itemObj.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.14f, 0.18f, 0.9f);

            // Color preview
            GameObject previewObj = new GameObject("Preview");
            previewObj.transform.SetParent(itemObj.transform, false);
            RectTransform prevRT = previewObj.AddComponent<RectTransform>();
            prevRT.anchorMin = new Vector2(0, 0.5f);
            prevRT.anchorMax = new Vector2(0, 0.5f);
            prevRT.pivot = new Vector2(0, 0.5f);
            prevRT.anchoredPosition = new Vector2(15, 0);
            prevRT.sizeDelta = new Vector2(60, 60);
            Image prevImg = previewObj.AddComponent<Image>();
            prevImg.color = skin.primaryColor;
            prevImg.raycastTarget = false;

            // Name
            GameObject nameObj = new GameObject("Name");
            nameObj.transform.SetParent(itemObj.transform, false);
            RectTransform nameRT = nameObj.AddComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0, 0.5f);
            nameRT.anchorMax = new Vector2(0, 0.5f);
            nameRT.pivot = new Vector2(0, 0.5f);
            nameRT.anchoredPosition = new Vector2(90, 10);
            nameRT.sizeDelta = new Vector2(150, 30);
            TextMeshProUGUI nameTMP = nameObj.AddComponent<TextMeshProUGUI>();
            nameTMP.text = skin.skinName;
            nameTMP.fontSize = 22;
            nameTMP.color = Color.white;
            nameTMP.fontStyle = FontStyles.Bold;
            nameTMP.raycastTarget = false;

            // Status / Buy button
            bool isUnlocked = SkinManager.Instance.IsSkinUnlocked(skin);
            bool isSelected = SkinManager.Instance.CurrentSkin == skin;

            GameObject btnObj = new GameObject("ActionButton");
            btnObj.transform.SetParent(itemObj.transform, false);
            RectTransform btnRT = btnObj.AddComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(1, 0.5f);
            btnRT.anchorMax = new Vector2(1, 0.5f);
            btnRT.pivot = new Vector2(1, 0.5f);
            btnRT.anchoredPosition = new Vector2(-10, 0);
            btnRT.sizeDelta = new Vector2(100, 50);

            Image btnImg = btnObj.AddComponent<Image>();
            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnImg;

            GameObject btnTextObj = new GameObject("Text");
            btnTextObj.transform.SetParent(btnObj.transform, false);
            RectTransform btnTextRT = btnTextObj.AddComponent<RectTransform>();
            btnTextRT.anchorMin = Vector2.zero;
            btnTextRT.anchorMax = Vector2.one;
            btnTextRT.offsetMin = Vector2.zero;
            btnTextRT.offsetMax = Vector2.zero;
            TextMeshProUGUI btnTMP = btnTextObj.AddComponent<TextMeshProUGUI>();
            btnTMP.alignment = TextAlignmentOptions.Center;
            btnTMP.fontSize = 18;
            btnTMP.raycastTarget = false;

            if (isSelected)
            {
                btnImg.color = new Color(0.2f, 0.7f, 1f);
                btnTMP.text = "EQUIPPED";
                btnTMP.color = Color.white;
                btn.interactable = false;
            }
            else if (isUnlocked)
            {
                btnImg.color = new Color(0.2f, 0.8f, 0.4f);
                btnTMP.text = "SELECT";
                btnTMP.color = Color.white;
                SkinData capturedSkin = skin;
                btn.onClick.AddListener(() => {
                    SkinManager.Instance.SelectSkin(capturedSkin);
                    RefreshShop();
                });
            }
            else
            {
                btnImg.color = new Color(0.9f, 0.7f, 0.1f);
                btnTMP.text = $"{skin.coinCost}";
                btnTMP.color = Color.white;
                SkinData capturedSkin = skin;
                btn.onClick.AddListener(() => {
                    if (ScoreManager.Instance != null && ScoreManager.Instance.SpendCoins(capturedSkin.coinCost))
                    {
                        SkinManager.Instance.UnlockSkin(capturedSkin);
                        SkinManager.Instance.SelectSkin(capturedSkin);
                        RefreshShop();
                    }
                });
            }

            return itemObj;
        }
    }
}
