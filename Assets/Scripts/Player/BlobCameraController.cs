using UnityEngine;
using UnityEngine.EventSystems;
using Blob3D.Core;

namespace Blob3D.Player
{
    /// <summary>
    /// Third-person orbital camera that follows the player blob.
    /// Supports 360-degree rotation via right-side touch drag (mobile) or right mouse button (PC).
    /// Camera distance and height scale dynamically with blob size.
    /// </summary>
    public class BlobCameraController : MonoBehaviour
    {
        [Header("Follow Settings")]
        [SerializeField] private Transform target;
        [SerializeField] private float followSmoothing = 8f;
        [SerializeField] private float baseFOV = 70f;

        [Header("Orbit Settings")]
        [SerializeField] private float baseDistance = 6f;
        [SerializeField] private float baseHeight = 5f;
        [SerializeField] private float horizontalSensitivity = 0.25f;
        [SerializeField] private float verticalSensitivity = 0.15f;
        [SerializeField] private float mouseSensitivity = 2f;
        [SerializeField] private float scrollZoomSpeed = 2f;
        [SerializeField] private float minVerticalAngle = 15f;
        [SerializeField] private float maxVerticalAngle = 75f;
        [SerializeField] private float rotationDamping = 5f; // How quickly rotation momentum decays

        [Header("Size Scaling")]
        [SerializeField] private float distancePerSize = 0.8f;  // Camera pulls back significantly as blob grows
        [SerializeField] private float heightPerSize = 0.4f;
        [SerializeField] private float maxDistance = 80f;
        [SerializeField] private float zoomSmoothing = 2f;

        [Header("Zoom (PC Scroll)")]
        [SerializeField] private float minZoomMultiplier = 0.5f;
        [SerializeField] private float maxZoomMultiplier = 2f;

        [Header("Auto-Follow Rotation")]
        [SerializeField] private bool autoFollowEnabled = true;
        [SerializeField] private float autoFollowSpeed = 2.5f;
        [SerializeField] private float autoFollowDelay = 1.0f;
        [SerializeField] private float autoFollowMinSpeed = 1.5f;

        // Current orbit angles
        private float yaw;         // Horizontal rotation around player (degrees)
        private float pitch = 45f; // Vertical angle from horizontal (degrees), default 45

        // Rotation velocity for smooth damping after input release
        private float yawVelocity;
        private float pitchVelocity;

        // Scroll zoom multiplier (PC only)
        private float zoomMultiplier = 1f;

        // Smoothed distance for size-based scaling
        private float currentDistance;
        private float currentBlobSize = 1f;

        // Touch tracking for mobile right-side drag
        private int rotationFingerId = -1;
        private Vector2 lastTouchPosition;

        // Auto-follow state
        private float timeSinceManualInput;
        private bool hadManualInput;

        private Camera cam;

        /// <summary>Current horizontal rotation angle (yaw) in degrees. Used by BlobController for camera-relative movement.</summary>
        public float Yaw => yaw;

        private void Start()
        {
            cam = GetComponent<Camera>();
            if (cam != null)
            {
                cam.fieldOfView = baseFOV;
            }

            currentDistance = baseDistance;

            // Initialize yaw from current camera position relative to target
            if (target != null)
            {
                Vector3 dir = transform.position - target.position;
                yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            HandleInput();
            ApplyDamping();
            AutoFollowRotation();
            UpdateCameraPosition();
        }

        /// <summary>Set follow target.</summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        /// <summary>Update touch sensitivity from settings.</summary>
        public void SetSensitivity(float value)
        {
            horizontalSensitivity = value;
            verticalSensitivity = value * 0.6f;
        }

        /// <summary>Called when blob size changes.</summary>
        public void OnBlobSizeChanged(float newSize)
        {
            currentBlobSize = newSize;
        }

        // -------- Input Handling --------

        private void HandleInput()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            HandleMouseInput();
#endif
            HandleTouchInput();
        }

        /// <summary>PC/Editor: right mouse button or left mouse button (outside UI) drag rotates, scroll wheel zooms.</summary>
        private void HandleMouseInput()
        {
            // Right mouse button or left mouse button (outside UI) drag for rotation
            bool rightDrag = Input.GetMouseButton(1);
            bool leftDrag = Input.GetMouseButton(0) &&
                            (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject());

            if (rightDrag || leftDrag)
            {
                float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
                float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

                yawVelocity = mouseX * 10f;
                pitchVelocity = -mouseY * 10f;

                yaw += mouseX * 10f;
                pitch = Mathf.Clamp(pitch - mouseY * 10f, minVerticalAngle, maxVerticalAngle);

                timeSinceManualInput = 0f;
                hadManualInput = true;
            }

            // Scroll wheel zoom
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                zoomMultiplier = Mathf.Clamp(zoomMultiplier - scroll * scrollZoomSpeed, minZoomMultiplier, maxZoomMultiplier);
            }
        }

        /// <summary>Mobile: right half of screen drag rotates camera.</summary>
        private void HandleTouchInput()
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);

                if (touch.phase == TouchPhase.Began)
                {
                    // Skip touches over UI elements (joystick, buttons, etc.)
                    if (EventSystem.current != null &&
                        EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                        continue;

                    // Only track touches that start on the right half of screen
                    if (touch.position.x > Screen.width * 0.5f && rotationFingerId == -1)
                    {
                        rotationFingerId = touch.fingerId;
                        lastTouchPosition = touch.position;
                    }
                }
                else if (touch.fingerId == rotationFingerId)
                {
                    if (touch.phase == TouchPhase.Moved)
                    {
                        Vector2 delta = touch.position - lastTouchPosition;
                        lastTouchPosition = touch.position;

                        float deltaYaw = delta.x * horizontalSensitivity;
                        float deltaPitch = -delta.y * verticalSensitivity;

                        yaw += deltaYaw;
                        pitch = Mathf.Clamp(pitch + deltaPitch, minVerticalAngle, maxVerticalAngle);

                        // Store velocity for damping after release
                        yawVelocity = deltaYaw / Time.deltaTime;
                        pitchVelocity = deltaPitch / Time.deltaTime;

                        timeSinceManualInput = 0f;
                        hadManualInput = true;
                    }
                    else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    {
                        rotationFingerId = -1;
                    }
                }
            }
        }

        /// <summary>Apply smooth damping so rotation continues briefly after input release.</summary>
        private void ApplyDamping()
        {
            // Only apply damping when not actively rotating
            bool leftDrag = Input.GetMouseButton(0) &&
                            (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject());
            bool isRotating = Input.GetMouseButton(1) || leftDrag || rotationFingerId != -1;
            if (isRotating) return;

            if (Mathf.Abs(yawVelocity) > 0.1f || Mathf.Abs(pitchVelocity) > 0.1f)
            {
                yaw += yawVelocity * Time.deltaTime;
                pitch = Mathf.Clamp(pitch + pitchVelocity * Time.deltaTime, minVerticalAngle, maxVerticalAngle);

                // Decay velocity
                yawVelocity = Mathf.Lerp(yawVelocity, 0f, Time.deltaTime * rotationDamping);
                pitchVelocity = Mathf.Lerp(pitchVelocity, 0f, Time.deltaTime * rotationDamping);
            }
            else
            {
                yawVelocity = 0f;
                pitchVelocity = 0f;
            }
        }

        // -------- Auto-Follow Rotation --------

        /// <summary>
        /// Automatically rotate camera to follow blob movement direction
        /// when no manual camera input has been given recently.
        /// </summary>
        private void AutoFollowRotation()
        {
            if (!autoFollowEnabled) return;

            timeSinceManualInput += Time.deltaTime;
            if (hadManualInput && timeSinceManualInput < autoFollowDelay) return;

            if (target == null) return;
            var targetRb = target.GetComponent<Rigidbody>();
            if (targetRb == null) return;

            Vector3 velocity = targetRb.velocity;
            velocity.y = 0f;
            if (velocity.sqrMagnitude < autoFollowMinSpeed * autoFollowMinSpeed) return;

            // Camera should be BEHIND the player: target yaw = direction + 180
            float targetYaw = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg + 180f;
            yaw = Mathf.LerpAngle(yaw, targetYaw, Time.deltaTime * autoFollowSpeed);
        }

        // -------- Camera Positioning --------

        private void UpdateCameraPosition()
        {
            // Calculate target distance based on blob size and zoom
            float targetDistance = Mathf.Min(baseDistance + currentBlobSize * distancePerSize, maxDistance);
            targetDistance *= zoomMultiplier;
            currentDistance = Mathf.Lerp(currentDistance, targetDistance, Time.deltaTime * zoomSmoothing);

            // Convert spherical coordinates (yaw, pitch, distance) to cartesian offset
            float pitchRad = pitch * Mathf.Deg2Rad;
            float yawRad = yaw * Mathf.Deg2Rad;

            // Horizontal distance from target at this pitch angle
            float horizontalDist = currentDistance * Mathf.Cos(pitchRad);
            float verticalDist = currentDistance * Mathf.Sin(pitchRad);

            // Offset from target: yaw controls XZ direction, pitch controls height
            Vector3 offset = new Vector3(
                -horizontalDist * Mathf.Sin(yawRad),
                verticalDist,
                -horizontalDist * Mathf.Cos(yawRad)
            );

            // Smooth follow position
            Vector3 targetPos = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * followSmoothing);

            // Always look at the player
            transform.LookAt(target.position);
        }
    }
}
