using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Windows;

namespace PointCloudViewer.Camera
{
    /// <summary>
    /// Orbit camera controller for point cloud inspection.
    /// Supports orbit, pan, and zoom with smooth damping
    /// </summary>
    public class OrbitCamera : MonoBehaviour
    {
        // The mode in which the camera is set to move around the screen in
        private enum CameraMode
        {
            None = 0,
            Orbit = 1,
            Pan = 2
        }

        [Header("Target")]
        [SerializeField] private Transform _target;
        [SerializeField] private Vector3 _targetOffset = Vector3.zero;

        [Header("Orbit Settings")]
        [SerializeField] private float _orbitSpeed = 5f;
        [SerializeField] private float _minPitch = -89f;
        [SerializeField] private float _maxPitch = 89f;

        [Header("Zoom Settings")]
        [SerializeField] private float _zoomSpeed = 10f;
        [SerializeField] private float _minDistance = 0.5f;
        [SerializeField] private float _maxDistance = 100f;

        [Header("Pan Settings")]
        [SerializeField] private float _panSpeed = 0.5f;

        [Header("Damping")]
        [SerializeField] private float _rotationDamping = 10f;
        [SerializeField] private float _zoomDamping = 8f;
        [SerializeField] private float _panDamping = 10f;

        [Header("Input")]
        [SerializeField] private InputActionAsset _inputActions;

        private InputAction _orbitAction;
        private InputAction _panAction;
        private InputAction _zoomAction;
        private InputAction _resetAction;
        private InputAction _pointerDeltaAction;

        // Current state
        private float _yaw;
        private float _pitch;
        private float _distance;
        private Vector3 _panOffset;

        // Target state (for damping)
        private float _targetYaw;
        private float _targetPitch;
        private float _targetDistance;
        private Vector3 _targetPanOffset;

        // Initial state (for reset)
        private float _initialYaw;
        private float _initialPitch;
        private float _initialDistance;

        // Cached input state (updated via callbacks)
        private CameraMode _currentMode = CameraMode.None;
        private Vector2 _mouseDelta;
        private float _zoomDelta;

        /// <summary>Gets the current distance from the focus point.</summary>
        public float Distance => _distance;

        /// <summary>Gets the current focus point in world space.</summary>
        public Vector3 FocusPoint => GetTargetPosition() + _panOffset;

        private void Awake()
        {
            SetInputActionRefs();
        }

        private void OnEnable()
        {
            if (_orbitAction != null)
            {
                _orbitAction.started += OnOrbitStarted;
                _orbitAction.canceled += OnOrbitCanceled;
            }

            if (_panAction != null)
            {
                _panAction.started += OnPanStarted;
                _panAction.canceled += OnPanCanceled;
            }

            if (_zoomAction != null)
            {
                _zoomAction.performed += OnZoomPerformed;
            }

            if (_resetAction != null)
            {
                _resetAction.performed += OnResetPerformed;
            }

            if (_pointerDeltaAction != null)
            {
                _pointerDeltaAction.performed += OnPointerDeltaPerformed;
            }
        }

        private void OnDisable()
        {
            if (_orbitAction != null)
            {
                _orbitAction.started -= OnOrbitStarted;
                _orbitAction.canceled -= OnOrbitCanceled;
            }

            if (_panAction != null)
            {
                _panAction.started -= OnPanStarted;
                _panAction.canceled -= OnPanCanceled;
            }

            if (_zoomAction != null)
            {
                _zoomAction.performed -= OnZoomPerformed;
            }

            if (_resetAction != null)
            {
                _resetAction.performed -= OnResetPerformed;
            }

            if (_pointerDeltaAction != null)
            {
                _pointerDeltaAction.performed -= OnPointerDeltaPerformed;
            }
        }

        private void Start()
        {
            InitializeFromCurrentPosition();
        }

        private void LateUpdate()
        {
            ApplyDamping();
            UpdateCameraPosition();
        }

        #region Input Actions

        private void SetInputActionRefs()
        {
            if (_inputActions == null) return;

            _orbitAction = _inputActions.FindAction("Orbit");
            _panAction = _inputActions.FindAction("Pan");
            _zoomAction = _inputActions.FindAction("Zoom");
            _resetAction = _inputActions.FindAction("Reset");
            _pointerDeltaAction = _inputActions.FindAction("PointerDelta");
        }

        private void OnOrbitStarted(InputAction.CallbackContext _) => _currentMode = CameraMode.Orbit;
        private void OnOrbitCanceled(InputAction.CallbackContext _) => _currentMode = CameraMode.None;
        private void OnPanStarted(InputAction.CallbackContext _) => _currentMode = CameraMode.Pan;
        private void OnPanCanceled(InputAction.CallbackContext _) => _currentMode = CameraMode.None;
        private void OnResetPerformed(InputAction.CallbackContext context) => ResetView();

        private void OnPointerDeltaPerformed(InputAction.CallbackContext context)
        {
            _mouseDelta = context.ReadValue<Vector2>();

            switch (_currentMode)
            {
                case CameraMode.Orbit:
                    _targetYaw += _mouseDelta.x * _orbitSpeed;
                    _targetPitch -= _mouseDelta.y * _orbitSpeed;
                    _targetPitch = Mathf.Clamp(_targetPitch, _minPitch, _maxPitch);
                    break;

                case CameraMode.Pan:
                    Vector3 right = transform.right;
                    Vector3 up = transform.up;
                    _targetPanOffset -= (right * _mouseDelta.x + up * _mouseDelta.y) * _panSpeed * (_distance * 0.1f);
                    break;
            }
        }

        private void OnZoomPerformed(InputAction.CallbackContext context)
        {
            _zoomDelta = context.ReadValue<Vector2>().y;

            if (Mathf.Abs(_zoomDelta) > 0.001f)
            {
                _targetDistance -= _zoomDelta * _zoomSpeed * (_distance * 0.3f);
                _targetDistance = Mathf.Clamp(_targetDistance, _minDistance, _maxDistance);
            }
        }

        #endregion

        /// <summary>Helper method to calculate target position without pan offset.</summary>
        private Vector3 GetTargetPosition()
        {
            return (_target != null ? _target.position : Vector3.zero) + _targetOffset;
        }

        private void InitializeFromCurrentPosition()
        {
            Vector3 targetPos = GetTargetPosition();
            Vector3 offset = transform.position - targetPos;

            _distance = offset.magnitude;

            if (_distance > 0.001f)
            {
                _yaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
                _pitch = Mathf.Asin(offset.y / _distance) * Mathf.Rad2Deg;
            }
            else
            {
                _yaw = 0f;
                _pitch = 30f;
                _distance = 10f;
            }

            // Sync target state with current state
            _targetYaw = _yaw;
            _targetPitch = _pitch;
            _targetDistance = _distance;

            // Store initial state for reset
            _initialYaw = _yaw;
            _initialPitch = _pitch;
            _initialDistance = _distance;

            _panOffset = Vector3.zero;
            _targetPanOffset = Vector3.zero;
        }

        /// <summary>
        /// Focus the camera on a specific bounds, adjusting distance to fit.
        /// </summary>
        /// <param name="bounds">The bounds to focus on.</param>
        public void FocusOnBounds(Bounds bounds)
        {
            _targetOffset = bounds.center;
            _targetPanOffset = Vector3.zero;
            _panOffset = Vector3.zero;

            // Calculate distance to fit bounds in view
            float radius = bounds.extents.magnitude;
            _targetDistance = radius * 2.5f;
            _targetDistance = Mathf.Clamp(_targetDistance, _minDistance, _maxDistance);

            _initialDistance = _targetDistance;
        }

        private void ApplyDamping()
        {
            float dt = Time.deltaTime;

            _yaw = Mathf.Lerp(_yaw, _targetYaw, dt * _rotationDamping);
            _pitch = Mathf.Lerp(_pitch, _targetPitch, dt * _rotationDamping);
            _distance = Mathf.Lerp(_distance, _targetDistance, dt * _zoomDamping);
            _panOffset = Vector3.Lerp(_panOffset, _targetPanOffset, dt * _panDamping);
        }

        private void UpdateCameraPosition()
        {
            // Calculate position from spherical coordinates
            float yawRad = _yaw * Mathf.Deg2Rad;
            float pitchRad = _pitch * Mathf.Deg2Rad;

            Vector3 offset = new Vector3(
                Mathf.Sin(yawRad) * Mathf.Cos(pitchRad),
                Mathf.Sin(pitchRad),
                Mathf.Cos(yawRad) * Mathf.Cos(pitchRad)
            ) * _distance;

            Vector3 focusPoint = GetTargetPosition() + _panOffset;

            transform.position = focusPoint + offset;
            transform.LookAt(focusPoint);
        }

        /// <summary>Reset to initial view.</summary>
        public void ResetView()
        {
            _targetYaw = _initialYaw;
            _targetPitch = _initialPitch;
            _targetDistance = _initialDistance;
            _targetPanOffset = Vector3.zero;
        }

        /// <summary>
        /// Set view angles directly without damping interpolation.
        /// </summary>
        /// <param name="yaw">Horizontal angle in degrees.</param>
        /// <param name="pitch">Vertical angle in degrees (clamped to min/max pitch).</param>
        /// <param name="distance">Distance from focus point (clamped to min/max distance).</param>
        public void SetView(float yaw, float pitch, float distance)
        {
            _yaw = _targetYaw = yaw;
            _pitch = _targetPitch = Mathf.Clamp(pitch, _minPitch, _maxPitch);
            _distance = _targetDistance = Mathf.Clamp(distance, _minDistance, _maxDistance);
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(FocusPoint, 0.1f);
            Gizmos.DrawLine(transform.position, FocusPoint);
        }
    }
}