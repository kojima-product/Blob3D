using UnityEngine;
using UnityEngine.EventSystems;
using Blob3D.Core;

namespace Blob3D.Player
{
    /// <summary>
    /// Third-person orbital camera that follows the player blob.
    /// A/D keys and horizontal drag rotate the camera (yaw).
    /// W/S moves forward/backward relative to camera direction.
    /// Mobile: left joystick horizontal axis also rotates camera.
    /// </summary>
    public class BlobCameraController : MonoBehaviour
    {
        [Header("Follow Settings")]
        [SerializeField] private Transform target;
        [SerializeField] private float followSmoothing = 8f;
        [SerializeField] private float baseFOV = 70f;

        [Header("Orbit Settings")]
        [SerializeField] private float baseDistance = 6f;
        [SerializeField] private float dragSensitivity = 0.3f;
        [SerializeField] private float rotationDamping = 5f;

        [Header("Size Scaling")]
        [SerializeField] private float distancePerSize = 0.8f;
        [SerializeField] private float maxDistance = 80f;
        [SerializeField] private float zoomSmoothing = 2f;

        [Header("Zoom (PC Scroll)")]
        [SerializeField] private float minZoomMultiplier = 0.5f;
        [SerializeField] private float maxZoomMultiplier = 2f;
        [SerializeField] private float scrollZoomSpeed = 2f;

        // Current orbit angles
        private float yaw;
        private float pitch = 40f;

        // Rotation velocity for smooth damping
        private float yawVelocity;

        // Scroll zoom
        private float zoomMultiplier = 1f;

        // Smoothed distance
        private float currentDistance;
        private float currentBlobSize = 1f;

        // Drag tracking
        private int dragFingerId = -1;
        private Vector2 lastDragPosition;
        private bool isDragging;

        // Dead zone to prevent accidental rotation on tap
        private const float DragDeadZonePixels = 5f;
        private Vector2 dragStartPosition;
        private bool dragThresholdMet;
        private Vector2 touchDragStartPosition;
        private bool touchDragThresholdMet;

        private Camera cam;

        /// <summary>Current yaw in degrees. Used by BlobController for camera-relative movement.</summary>
        public float Yaw => yaw;

        /// <summary>Rotate the camera yaw by given degrees. Called from MobileInputHandler for A/D keys.</summary>
        public void RotateYaw(float degrees)
        {
            yaw += degrees;
        }

        private void Start()
        {
            cam = GetComponent<Camera>();
            if (cam != null)
                cam.fieldOfView = baseFOV;

            currentDistance = baseDistance;

            if (target != null)
            {
                Vector3 dir = transform.position - target.position;
                yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            HandleDragInput();
            HandleScrollZoom();
            ApplyDamping();
            UpdateCameraPosition();
        }

        /// <summary>Set follow target.</summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        /// <summary>Update drag sensitivity from settings.</summary>
        public void SetSensitivity(float value)
        {
            dragSensitivity = value;
        }

        /// <summary>Called when blob size changes.</summary>
        public void OnBlobSizeChanged(float newSize)
        {
            currentBlobSize = newSize;
        }

        // -------- Drag Input (any finger/mouse button on screen) --------

        private void HandleDragInput()
        {
            // --- Mouse drag (any button) ---
#if UNITY_EDITOR || UNITY_STANDALONE
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                // Don't start drag if clicking on UI
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                    return;
                isDragging = true;
                dragThresholdMet = false;
                dragStartPosition = Input.mousePosition;
                lastDragPosition = Input.mousePosition;
            }

            if (isDragging && (Input.GetMouseButton(0) || Input.GetMouseButton(1)))
            {
                Vector2 currentPos = Input.mousePosition;

                if (!dragThresholdMet)
                {
                    if (Vector2.Distance(currentPos, dragStartPosition) >= DragDeadZonePixels)
                    {
                        dragThresholdMet = true;
                        lastDragPosition = currentPos;
                    }
                    else
                    {
                        return;
                    }
                }

                Vector2 delta = currentPos - lastDragPosition;
                lastDragPosition = currentPos;

                float deltaYaw = delta.x * dragSensitivity;
                yaw += deltaYaw;
                yawVelocity = deltaYaw / Mathf.Max(Time.deltaTime, 0.001f);
            }

            if (Input.GetMouseButtonUp(0) && !Input.GetMouseButton(1))
                isDragging = false;
            if (Input.GetMouseButtonUp(1) && !Input.GetMouseButton(0))
                isDragging = false;
#endif

            // --- Touch drag (any finger not on UI) ---
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);

                if (touch.phase == TouchPhase.Began)
                {
                    if (EventSystem.current != null &&
                        EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                        continue;

                    if (dragFingerId == -1)
                    {
                        dragFingerId = touch.fingerId;
                        touchDragThresholdMet = false;
                        touchDragStartPosition = touch.position;
                        lastDragPosition = touch.position;
                    }
                }
                else if (touch.fingerId == dragFingerId)
                {
                    if (touch.phase == TouchPhase.Moved)
                    {
                        if (!touchDragThresholdMet)
                        {
                            if (Vector2.Distance(touch.position, touchDragStartPosition) >= DragDeadZonePixels)
                            {
                                touchDragThresholdMet = true;
                                lastDragPosition = touch.position;
                            }
                            else
                            {
                                continue;
                            }
                        }

                        Vector2 delta = touch.position - lastDragPosition;
                        lastDragPosition = touch.position;

                        float deltaYaw = delta.x * dragSensitivity;
                        yaw += deltaYaw;
                        yawVelocity = deltaYaw / Mathf.Max(Time.deltaTime, 0.001f);
                    }
                    else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    {
                        dragFingerId = -1;
                    }
                }
            }
        }

        private void HandleScrollZoom()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                zoomMultiplier = Mathf.Clamp(zoomMultiplier - scroll * scrollZoomSpeed,
                    minZoomMultiplier, maxZoomMultiplier);
            }
#endif
        }

        private void ApplyDamping()
        {
            bool isActive = isDragging || dragFingerId != -1;
            if (isActive) return;

            if (Mathf.Abs(yawVelocity) > 0.5f)
            {
                yaw += yawVelocity * Time.deltaTime;
                yawVelocity = Mathf.Lerp(yawVelocity, 0f, Time.deltaTime * rotationDamping);
            }
            else
            {
                yawVelocity = 0f;
            }
        }

        // -------- Camera Positioning --------

        private void UpdateCameraPosition()
        {
            float targetDistance = Mathf.Min(baseDistance + currentBlobSize * distancePerSize, maxDistance);
            targetDistance *= zoomMultiplier;
            currentDistance = Mathf.Lerp(currentDistance, targetDistance, Time.deltaTime * zoomSmoothing);

            float pitchRad = pitch * Mathf.Deg2Rad;
            float yawRad = yaw * Mathf.Deg2Rad;

            float horizontalDist = currentDistance * Mathf.Cos(pitchRad);
            float verticalDist = currentDistance * Mathf.Sin(pitchRad);

            Vector3 offset = new Vector3(
                -horizontalDist * Mathf.Sin(yawRad),
                verticalDist,
                -horizontalDist * Mathf.Cos(yawRad)
            );

            Vector3 targetPos = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * followSmoothing);

            transform.LookAt(target.position);
        }
    }
}
