using UnityEngine;
using Blob3D.Core;
using Blob3D.UI;

namespace Blob3D.Player
{
    /// <summary>
    /// Input hub for mobile and editor.
    /// W/S (or Up/Down arrows): forward/backward movement relative to camera.
    /// A/D (or Left/Right arrows): rotate camera (turning/steering).
    /// Joystick: vertical axis = forward/backward, horizontal axis = camera rotation.
    /// </summary>
    public class MobileInputHandler : MonoBehaviour
    {
        [Header("Double Tap Settings")]
        [SerializeField] private float doubleTapThreshold = 0.3f;
        [SerializeField] private float doubleTapMaxDistance = 50f;

        [Header("Steering")]
        [SerializeField] private float keyboardTurnSpeed = 120f; // Degrees per second

        private float lastTapTime;
        private Vector2 lastTapPosition;
        private VirtualJoystick joystick;
        private BlobCameraController cameraController;

        private void Start()
        {
            joystick = FindObjectOfType<VirtualJoystick>();
            cameraController = FindObjectOfType<BlobCameraController>();
        }

        private void Update()
        {
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;

            DetectDoubleTap();
            HandleKeyboardInput();
            HandleJoystickSteering();
        }

        private void DetectDoubleTap()
        {
            if (Input.touchCount == 0) return;

            Touch touch = Input.GetTouch(0);
            if (touch.phase != TouchPhase.Began) return;

            // Only left half of screen for double-tap
            if (touch.position.x > Screen.width * 0.5f) return;

            float timeSinceLastTap = Time.time - lastTapTime;
            float distance = Vector2.Distance(touch.position, lastTapPosition);

            if (timeSinceLastTap < doubleTapThreshold && distance < doubleTapMaxDistance)
            {
                BlobController.Instance?.TriggerSplit();
                lastTapTime = 0f;
            }
            else
            {
                lastTapTime = Time.time;
                lastTapPosition = touch.position;
            }
        }

        /// <summary>
        /// W/S = forward/backward (input.y).
        /// A/D = rotate camera yaw (steering), NOT lateral movement.
        /// </summary>
        private void HandleKeyboardInput()
        {
            // Forward / backward only
            float forward = 0f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) forward += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) forward -= 1f;

            // A/D rotates camera (steering)
            float turn = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) turn -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) turn += 1f;

            // Apply camera rotation for steering
            if (Mathf.Abs(turn) > 0.01f && cameraController != null)
            {
                cameraController.RotateYaw(turn * keyboardTurnSpeed * Time.deltaTime);
            }

            // Send forward/backward as input (only Y axis, X is always 0 for keyboard)
            if (Mathf.Abs(forward) > 0.01f)
            {
                BlobController.Instance?.SetInput(new Vector2(0f, forward));
            }
            else if (joystick == null || !joystick.IsActive)
            {
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

        /// <summary>
        /// Joystick horizontal axis rotates camera (steering).
        /// Joystick vertical axis moves forward/backward.
        /// </summary>
        private void HandleJoystickSteering()
        {
            if (joystick == null || !joystick.IsActive) return;

            Vector2 input = joystick.GetInput();

            // Horizontal = camera rotation (steering)
            if (Mathf.Abs(input.x) > 0.1f && cameraController != null)
            {
                cameraController.RotateYaw(input.x * keyboardTurnSpeed * Time.deltaTime);
            }

            // Vertical = forward/backward movement
            if (Mathf.Abs(input.y) > 0.1f)
            {
                BlobController.Instance?.SetInput(new Vector2(0f, input.y));
            }
            else
            {
                BlobController.Instance?.SetInput(Vector2.zero);
            }
        }
    }
}
