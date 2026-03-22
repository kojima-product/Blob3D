using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Blob3D.UI
{
    /// <summary>
    /// モバイル向けバーチャルジョイスティック。
    /// 画面左下に配置し、タッチドラッグで方向入力を行う。
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("References")]
        [SerializeField] private RectTransform background;   // ジョイスティック背景
        [SerializeField] private RectTransform handle;       // 動かすノブ
        [SerializeField] private Canvas canvas;

        [Header("Settings")]
        [SerializeField] private float handleRange = 80f;    // Larger range for mobile usability
        [SerializeField] private float deadZone = 0.15f;     // Larger dead zone for mobile

        private Vector2 inputVector = Vector2.zero;
        private Camera uiCamera;
        private Vector2 originalBackgroundPosition;

        /// <summary>Whether the joystick is currently being touched/dragged.</summary>
        public bool IsActive { get; private set; }

        private void Start()
        {
            // Fix: null checks on serialized references to prevent NullReferenceException
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                uiCamera = canvas.worldCamera;
            }

            // Save original position for reset on pointer up
            if (background != null)
                originalBackgroundPosition = background.anchoredPosition;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // Move joystick background to touch/click position
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform, eventData.position, uiCamera, out localPoint);
            background.anchoredPosition = localPoint;

            // Reset handle and input to prevent drift from coordinate mismatch
            handle.anchoredPosition = Vector2.zero;
            inputVector = Vector2.zero;
            IsActive = true;
            // Do NOT call OnDrag here — let the next drag event handle input naturally
        }

        public void OnDrag(PointerEventData eventData)
        {
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                background, eventData.position, uiCamera, out localPoint);

            // 正規化（-1〜1）
            inputVector = localPoint / (background.sizeDelta * 0.5f);
            inputVector = Vector2.ClampMagnitude(inputVector, 1f);

            // デッドゾーン
            if (inputVector.magnitude < deadZone)
            {
                inputVector = Vector2.zero;
            }

            // ハンドルの位置を更新
            handle.anchoredPosition = inputVector * handleRange;

            // Input is read by MobileInputHandler via GetInput() — no direct SetInput here
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            inputVector = Vector2.zero;
            handle.anchoredPosition = Vector2.zero;
            IsActive = false;

            // Reset joystick background to original position
            background.anchoredPosition = originalBackgroundPosition;

            // Input reset handled by MobileInputHandler when IsActive becomes false
        }

        /// <summary>現在の入力ベクトルを取得</summary>
        public Vector2 GetInput()
        {
            return inputVector;
        }
    }
}
