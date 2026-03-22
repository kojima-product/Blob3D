using UnityEngine;
using Blob3D.Core;
using Blob3D.UI;

namespace Blob3D.Player
{
    /// <summary>
    /// Input hub for mobile and editor.
    /// Handles WASD keyboard input, double-tap detection for split.
    /// Camera rotation is handled entirely by BlobCameraController (right-side touch / right mouse button).
    /// </summary>
    public class MobileInputHandler : MonoBehaviour
    {
        [Header("Double Tap Settings")]
        [SerializeField] private float doubleTapThreshold = 0.3f;
        [SerializeField] private float doubleTapMaxDistance = 50f; // In pixels

        private float lastTapTime;
        private Vector2 lastTapPosition;
        private VirtualJoystick joystick;

        private void Start()
        {
            joystick = FindObjectOfType<VirtualJoystick>();
        }

        private void Update()
        {
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;

            DetectDoubleTap();

            // Keyboard input works in editor and on mobile simultaneously
            HandleKeyboardInput();
        }

        private void DetectDoubleTap()
        {
            if (Input.touchCount == 0) return;

            Touch touch = Input.GetTouch(0);
            if (touch.phase != TouchPhase.Began) return;

            // Only left half of screen for double-tap (right half is camera rotation)
            if (touch.position.x > Screen.width * 0.5f) return;

            float timeSinceLastTap = Time.time - lastTapTime;
            float distance = Vector2.Distance(touch.position, lastTapPosition);

            if (timeSinceLastTap < doubleTapThreshold && distance < doubleTapMaxDistance)
            {
                // Double tap -> split
                BlobController.Instance?.TriggerSplit();
                lastTapTime = 0f; // Reset
            }
            else
            {
                lastTapTime = Time.time;
                lastTapPosition = touch.position;
            }
        }

        /// <summary>WASD / Arrow key input, available in both editor and builds.</summary>
        private void HandleKeyboardInput()
        {
            // Convert WASD / Arrow keys to joystick-style input
            Vector2 input = Vector2.zero;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) input.y += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) input.y -= 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) input.x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) input.x += 1f;

            if (input.sqrMagnitude > 0.01f)
            {
                BlobController.Instance?.SetInput(input.normalized);
            }
            else if (joystick == null || !joystick.IsActive)
            {
                // Only reset when joystick is not active to prevent overriding joystick input
                BlobController.Instance?.SetInput(Vector2.zero);
            }

            // Space: dash
            if (Input.GetKeyDown(KeyCode.Space))
            {
                BlobController.Instance?.TriggerDash();
            }

            // F: split
            if (Input.GetKeyDown(KeyCode.F))
            {
                BlobController.Instance?.TriggerSplit();
            }
        }
    }
}
