using UnityEngine;
using UnityEngine.UI;
using Blob3D.Core;
using Blob3D.Player;
using Blob3D.AI;

namespace Blob3D.UI
{
    /// <summary>
    /// Image-based radar minimap (no RenderTexture for performance).
    /// Shows player at center, AI blobs as colored dots, and field boundary outline.
    /// Uses a circular Mask for round appearance and includes a facing direction indicator.
    /// Updates at a fixed interval to reduce per-frame overhead.
    /// </summary>
    public class MinimapController : MonoBehaviour
    {
        [SerializeField] private float mapRadius = 80f; // UI radius in pixels
        [SerializeField] private float updateInterval = 0.3f;

        private RectTransform mapRect;
        private Image[] dots;
        private const int MaxDots = 50;
        private float timer;
        private Image playerDot;
        private Image boundaryCircle;
        private RectTransform directionIndicator;
        private BlobCameraController cachedCameraController;

        private void Start()
        {
            CreateMinimapUI();
        }

        private void CreateMinimapUI()
        {
            // Background with circular mask
            mapRect = GetComponent<RectTransform>();
            if (mapRect == null) mapRect = gameObject.AddComponent<RectTransform>();
            mapRect.anchorMin = new Vector2(0, 0);
            mapRect.anchorMax = new Vector2(0, 0);
            mapRect.pivot = new Vector2(0, 0);
            mapRect.anchoredPosition = new Vector2(20, 20);
            mapRect.sizeDelta = new Vector2(mapRadius * 2, mapRadius * 2);

            Image bg = gameObject.GetComponent<Image>();
            if (bg == null) bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.09f, 0.14f, 0.75f);
            bg.raycastTarget = false;

            // Add circular mask using Mask component with showMaskGraphic enabled
            Mask mask = gameObject.GetComponent<Mask>();
            if (mask == null) mask = gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            // Boundary circle (outline effect using slightly smaller circle)
            GameObject boundaryObj = new GameObject("Boundary");
            boundaryObj.transform.SetParent(transform, false);
            RectTransform brt = boundaryObj.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.5f, 0.5f);
            brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(mapRadius * 1.9f, mapRadius * 1.9f);
            boundaryCircle = boundaryObj.AddComponent<Image>();
            boundaryCircle.color = new Color(0.3f, 0.35f, 0.4f, 0.5f);
            boundaryCircle.raycastTarget = false;

            // Player dot (center, white, slightly larger)
            playerDot = CreateDot("PlayerDot", Color.white, 8f);
            playerDot.rectTransform.anchoredPosition = Vector2.zero;

            // Direction indicator: narrow line extending from center dot showing facing direction
            GameObject dirObj = new GameObject("DirectionIndicator");
            dirObj.transform.SetParent(transform, false);
            directionIndicator = dirObj.AddComponent<RectTransform>();
            directionIndicator.anchorMin = new Vector2(0.5f, 0.5f);
            directionIndicator.anchorMax = new Vector2(0.5f, 0.5f);
            directionIndicator.pivot = new Vector2(0.5f, 0f); // Pivot at bottom center so it extends upward from center
            directionIndicator.sizeDelta = new Vector2(2f, 12f);
            directionIndicator.anchoredPosition = Vector2.zero;
            Image dirImg = dirObj.AddComponent<Image>();
            dirImg.color = new Color(1f, 1f, 1f, 0.8f);
            dirImg.raycastTarget = false;

            // Pool of dots for AI and other entities
            dots = new Image[MaxDots];
            for (int i = 0; i < MaxDots; i++)
            {
                dots[i] = CreateDot($"Dot_{i}", Color.red, 5f);
                dots[i].gameObject.SetActive(false);
            }
        }

        private Image CreateDot(string name, Color color, float size)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(transform, false);
            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            Image img = obj.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private void Update()
        {
            if (GameManager.Instance == null ||
                GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;

            // Update direction indicator every frame for smooth rotation
            UpdateDirectionIndicator();

            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                timer = updateInterval;
                UpdateMinimap();
            }
        }

        /// <summary>
        /// Rotate the direction indicator line to match camera yaw.
        /// The line extends from the center dot showing which direction the player faces.
        /// </summary>
        private void UpdateDirectionIndicator()
        {
            if (directionIndicator == null) return;

            float cameraYaw = 0f;
            if (cachedCameraController == null)
            {
                cachedCameraController = FindObjectOfType<BlobCameraController>();
            }
            if (cachedCameraController != null)
            {
                cameraYaw = cachedCameraController.Yaw;
            }
            else if (Camera.main != null)
            {
                cameraYaw = Camera.main.transform.eulerAngles.y;
            }

            // Map camera yaw to UI rotation (negate because UI rotates clockwise)
            directionIndicator.localRotation = Quaternion.Euler(0f, 0f, -cameraYaw);
        }

        private void UpdateMinimap()
        {
            Vector3 playerPos = BlobController.Instance != null ?
                BlobController.Instance.transform.position : Vector3.zero;
            float fieldRadius = GameManager.Instance.FieldRadius;

            int dotIndex = 0;

            // Fix: copy list to avoid InvalidOperationException if list is modified during iteration
            if (AISpawner.Instance?.ActiveAIs != null)
            {
                var aiList = AISpawner.Instance.ActiveAIs;
                for (int i = 0, count = aiList.Count; i < count && dotIndex < MaxDots; i++)
                {
                    var ai = aiList[i];
                    if (ai == null || !ai.IsAlive) continue;

                    Vector2 mapPos = WorldToMinimap(ai.transform.position, playerPos, fieldRadius);
                    if (mapPos.magnitude > mapRadius * 0.9f) continue; // Off map

                    dots[dotIndex].gameObject.SetActive(true);
                    dots[dotIndex].rectTransform.anchoredPosition = mapPos;

                    // Color by AI size relative to player
                    float playerSize = BlobController.Instance != null ? BlobController.Instance.CurrentSize : 1f;
                    dots[dotIndex].color = ai.CurrentSize > playerSize * 1.1f ?
                        new Color(1f, 0.3f, 0.3f) : // Danger (red)
                        new Color(0.3f, 0.8f, 0.4f); // Prey (green)

                    // Scale dot by AI size
                    float dotSize = Mathf.Clamp(ai.CurrentSize * 0.4f, 3f, 10f);
                    dots[dotIndex].rectTransform.sizeDelta = new Vector2(dotSize, dotSize);

                    dotIndex++;
                }
            }

            // Hide unused dots
            for (int i = dotIndex; i < MaxDots; i++)
            {
                dots[i].gameObject.SetActive(false);
            }

            // Update boundary circle scale based on field shrinking
            if (boundaryCircle != null)
            {
                float normalizedRadius = fieldRadius / 150f; // Assuming original radius ~150
                boundaryCircle.rectTransform.sizeDelta = new Vector2(
                    mapRadius * 1.9f * normalizedRadius,
                    mapRadius * 1.9f * normalizedRadius);
            }
        }

        private Vector2 WorldToMinimap(Vector3 worldPos, Vector3 centerPos, float fieldRadius)
        {
            Vector3 offset = worldPos - centerPos;
            float scale = mapRadius * 0.85f / fieldRadius;
            return new Vector2(offset.x * scale, offset.z * scale);
        }
    }
}
