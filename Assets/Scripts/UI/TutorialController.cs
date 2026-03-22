using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Blob3D.Core;
using Blob3D.Utils;

namespace Blob3D.UI
{
    public class TutorialController : MonoBehaviour
    {
        private const string TutorialKey = "Blob3D_TutorialDone";

        private GameObject overlayPanel;
        private TextMeshProUGUI instructionText;
        private TextMeshProUGUI tapText;
        private CanvasGroup canvasGroup;
        private int currentStep;

        // Localization keys for each tutorial step
        private static readonly string[] stepKeys = {
            "tutorial_1",
            "tutorial_2",
            "tutorial_3"
        };

        private void Start()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnGameStart += CheckTutorial;
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnGameStart -= CheckTutorial;
        }

        private void CheckTutorial()
        {
            // Skip tutorial if already completed, or just mark as done and proceed
            if (PlayerPrefs.GetInt(TutorialKey, 0) == 1) return;

            // Auto-mark as done so it doesn't block gameplay on restart
            PlayerPrefs.SetInt(TutorialKey, 1);
            PlayerPrefs.Save();

            StartCoroutine(RunTutorial());
        }

        private IEnumerator RunTutorial()
        {
            // Tutorial started
            Time.timeScale = 0f;

            CreateOverlayUI();

            // If overlay creation failed (no Canvas), abort tutorial
            if (overlayPanel == null)
            {
                Time.timeScale = 1f;
                yield break;
            }

            for (currentStep = 0; currentStep < stepKeys.Length; currentStep++)
            {
                instructionText.text = Localization.Get(stepKeys[currentStep]);

                // Fade in
                yield return FadeOverlay(0f, 1f, 0.3f);

                // Wait for tap (using unscaled time)
                yield return WaitForTapUnscaled();

                // Fade out
                yield return FadeOverlay(1f, 0f, 0.2f);
            }

            // Tutorial done
            PlayerPrefs.SetInt(TutorialKey, 1);
            PlayerPrefs.Save();

            if (overlayPanel != null) Destroy(overlayPanel);
            Time.timeScale = 1f;
            // Tutorial finished
        }

        private void CreateOverlayUI()
        {
            // Find parent canvas
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("TutorialController: No Canvas found. Skipping tutorial overlay.");
                return;
            }

            overlayPanel = new GameObject("TutorialOverlay");
            overlayPanel.transform.SetParent(canvas.transform, false);

            RectTransform rt = overlayPanel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            canvasGroup = overlayPanel.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;

            // Dark background
            Image bg = overlayPanel.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.8f);
            bg.raycastTarget = true;

            // Instruction text
            GameObject textObj = new GameObject("InstructionText");
            textObj.transform.SetParent(overlayPanel.transform, false);
            RectTransform textRT = textObj.AddComponent<RectTransform>();
            textRT.anchoredPosition = new Vector2(0, 50);
            textRT.sizeDelta = new Vector2(800, 200);
            instructionText = textObj.AddComponent<TextMeshProUGUI>();
            instructionText.fontSize = 48;
            instructionText.alignment = TextAlignmentOptions.Center;
            instructionText.color = Color.white;
            instructionText.fontStyle = FontStyles.Bold;
            instructionText.raycastTarget = false;

            // Tap to continue text
            GameObject tapObj = new GameObject("TapText");
            tapObj.transform.SetParent(overlayPanel.transform, false);
            RectTransform tapRT = tapObj.AddComponent<RectTransform>();
            tapRT.anchoredPosition = new Vector2(0, -150);
            tapRT.sizeDelta = new Vector2(600, 60);
            tapText = tapObj.AddComponent<TextMeshProUGUI>();
            tapText.text = Localization.Get("tutorial_tap");
            tapText.fontSize = 28;
            tapText.alignment = TextAlignmentOptions.Center;
            tapText.color = new Color(0.7f, 0.75f, 0.82f);
            tapText.raycastTarget = false;
        }

        private IEnumerator FadeOverlay(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            canvasGroup.alpha = to;
        }

        private IEnumerator WaitForTapUnscaled()
        {
            // Wait a brief moment to prevent accidental skip
            float cooldown = 0.5f;
            while (cooldown > 0f)
            {
                cooldown -= Time.unscaledDeltaTime;
                yield return null;
            }

            while (true)
            {
                if (Input.GetMouseButtonDown(0) || Input.touchCount > 0)
                {
                    bool newTouch = false;
                    for (int i = 0; i < Input.touchCount; i++)
                    {
                        if (Input.GetTouch(i).phase == TouchPhase.Began)
                            newTouch = true;
                    }
                    if (Input.GetMouseButtonDown(0) || newTouch)
                        yield break;
                }
                yield return null;
            }
        }
    }
}
