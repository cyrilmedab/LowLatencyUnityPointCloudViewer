using System.Diagnostics;
using UnityEngine;
using PointCloudViewer.Core;

namespace PointCloudViewer.Optimization
{
    /// <summary>
    /// CPU-based frustum culling for point clouds.
    /// Tests each point against camera frustum planes.
    /// </summary>
    public class FrustumCuller : MonoBehaviour, IOptimization
    {
        #region Serialized Member Variables

        [Header("Settings")]
        [SerializeField] private bool _enabled = true;
        [SerializeField] private UnityEngine.Camera _camera;
        [SerializeField] private float _nearPlaneOffset = 0f;
        [SerializeField] private float _farPlaneOffset = 0f;

        [Header("Optimization")]
        //[SerializeField] private bool _useSpatialHash = false;
        [SerializeField] private float _cellSize = 1f;

        #endregion

        #region Private Member Variables

        // Frustum plane indices (per Unity GeometryUtility.CalculateFrustumPlanes documentation)
        private const int FRUSTUM_PLANE_LEFT = 0;
        private const int FRUSTUM_PLANE_RIGHT = 1;
        private const int FRUSTUM_PLANE_DOWN = 2;
        private const int FRUSTUM_PLANE_UP = 3;
        private const int FRUSTUM_PLANE_NEAR = 4;
        private const int FRUSTUM_PLANE_FAR = 5;

        // Frustum planes (updated each frame)
        private Plane[] _frustumPlanes = new Plane[6];

        // Cached corners array for bounds testing (avoids per-call allocation)
        private readonly Vector3[] _boundsCorners = new Vector3[8];

        // Stats
        private OptimizationStats _stats;
        private Stopwatch _timer;

        #endregion

        /// <inheritdoc/>
        public string OptimizationName => "Frustum Culling";

        /// <inheritdoc/>
        public bool IsEnabled { get => _enabled; set => _enabled = value; }

        private void Awake()
        {
            _timer = new Stopwatch();

            if (_camera == null) { _camera = UnityEngine.Camera.main; }
        }

        public OptimizationStats Process(PointCloudData data, int[] indices, ref int count)
        {
            if (!_enabled || _camera == null || data == null)
            {
                _stats = OptimizationStats.PassThrough(OptimizationName, count);
                return _stats;
            }

            _timer.Restart();

            int inputCount = count;

            // Update frustum planes
            UpdateFrustumPlanes();

            // Quick bounds check - if entire cloud is visible, skip per-point testing
            if (BoundsFullyInFrustum(data.Bounds))
            {
                _timer.Stop();
                _stats = OptimizationStats.CreateActive(
                    OptimizationName,
                    inputCount,
                    count,
                    (float)_timer.Elapsed.TotalMilliseconds);
                return _stats;
            }

            // Quick bounds check - if entirely outside, output zero
            if (!BoundsIntersectsFrustum(data.Bounds))
            {
                count = 0;
                _timer.Stop();
                _stats = OptimizationStats.CreateActive(
                    OptimizationName,
                    inputCount,
                    0,
                    (float)_timer.Elapsed.TotalMilliseconds);
                return _stats;
            }

            // Per-point frustum testing
            int writeIndex = 0;
            PointStruct[] points = data.Points;

            if (inputCount == data.PointCount)
            {
                // Fresh pass - test all points
                for (int i = 0; i < data.PointCount; i++)
                {
                    if (IsPointInFrustum(points[i].x, points[i].y, points[i].z))
                    {
                        indices[writeIndex++] = i;
                    }
                }
            }
            else
            {
                // Stacked pass - test only previously visible points
                for (int i = 0; i < inputCount; i++)
                {
                    int pointIndex = indices[i];
                    if (IsPointInFrustum(points[pointIndex].x, points[pointIndex].y, points[pointIndex].z))
                    {
                        indices[writeIndex++] = pointIndex;
                    }
                }
            }

            count = writeIndex;

            _timer.Stop();

            _stats = OptimizationStats.CreateActive(
                OptimizationName,
                inputCount,
                count,
                (float)_timer.Elapsed.TotalMilliseconds);

            return _stats;
        }

        private void UpdateFrustumPlanes()
        {
            // Get frustum planes from camera
            GeometryUtility.CalculateFrustumPlanes(_camera, _frustumPlanes);

            // Apply near/far offsets if configured
            if (_nearPlaneOffset != 0f)
            {
                _frustumPlanes[FRUSTUM_PLANE_NEAR].distance -= _nearPlaneOffset;
            }
            if (_farPlaneOffset != 0f)
            {
                _frustumPlanes[FRUSTUM_PLANE_FAR].distance += _farPlaneOffset;
            }
        }

        /// <summary>
        /// Test if point is inside frustum (zero-allocation version).
        /// </summary>
        /// <param name="x">Point X coordinate</param>
        /// <param name="y">Point Y coordinate</param>
        /// <param name="z">Point Z coordinate</param>
        /// <returns>True if point is inside all frustum planes</returns>
        private bool IsPointInFrustum(float x, float y, float z)
        {
            // Test against all 6 frustum planes
            // Plane.GetDistanceToPoint equivalent: dot(normal, point) + distance
            for (int i = 0; i < 6; i++)
            {
                Plane plane = _frustumPlanes[i];
                float distance = plane.normal.x * x + plane.normal.y * y + plane.normal.z * z + plane.distance;
                if (distance < 0)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Test if point is inside frustum (Vector3 overload for bounds testing).</summary>
        private bool IsPointInFrustum(Vector3 point) => IsPointInFrustum(point.x, point.y, point.z);

        private bool BoundsIntersectsFrustum(Bounds bounds) => GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds);

        private bool BoundsFullyInFrustum(Bounds bounds)
        {
            // Check if all 8 corners are inside frustum
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            // Populate cached corners array (zero allocation by direct component assignment)
            _boundsCorners[0].x = min.x; _boundsCorners[0].y = min.y; _boundsCorners[0].z = min.z;
            _boundsCorners[1].x = min.x; _boundsCorners[1].y = min.y; _boundsCorners[1].z = max.z;
            _boundsCorners[2].x = min.x; _boundsCorners[2].y = max.y; _boundsCorners[2].z = min.z;
            _boundsCorners[3].x = min.x; _boundsCorners[3].y = max.y; _boundsCorners[3].z = max.z;
            _boundsCorners[4].x = max.x; _boundsCorners[4].y = min.y; _boundsCorners[4].z = min.z;
            _boundsCorners[5].x = max.x; _boundsCorners[5].y = min.y; _boundsCorners[5].z = max.z;
            _boundsCorners[6].x = max.x; _boundsCorners[6].y = max.y; _boundsCorners[6].z = min.z;
            _boundsCorners[7].x = max.x; _boundsCorners[7].y = max.y; _boundsCorners[7].z = max.z;

            for (int i = 0; i < 8; i++)
            {
                if (!IsPointInFrustum(_boundsCorners[i])) { return false; }
            }

            return true;
        }

        /// <inheritdoc/>
        public void Reset() => _stats = new OptimizationStats { Name = OptimizationName };

        /// <inheritdoc/>
        public OptimizationStats GetStats() => _stats;

    }
}