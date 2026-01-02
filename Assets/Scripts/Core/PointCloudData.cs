using System.Data;
using UnityEngine;

namespace PointCloudViewer.Core
{
    public class PointCloudData : MonoBehaviour
    {
        #region Read-Only Backing Fields

        private readonly PointStruct[] _points;
        private readonly Bounds _bounds;
        private readonly int _pointCount;

        #endregion

        #region Public Read-Only Properties

        /// <summary>Gets the raw point array.</summary>
        public PointStruct[] Points => _points;

        /// <summary>Gets the axis-aligned bounding box containing all points.</summary>
        public Bounds Bounds => _bounds;

        /// <summary>Gets the number of points in this cloud.</summary>
        public int PointCount => _pointCount;

        /// <summary>Memory footprint in bytes.</summary>
        public long MemorySize => (long)_pointCount * PointStruct.STRIDE;

        #endregion

        /// <summary>
        /// Creates a new point cloud data container from an array of points.
        /// Computes bounding box automatically.
        /// </summary>
        /// <param name="points">Array of points. Null is treated as empty array.</param>
        public PointCloudData(PointStruct[] points)
        {
            _points = points ?? System.Array.Empty<PointStruct>();
            _bounds = ComputeBounds(_points);
            _pointCount = _points.Length;
        }

        /// <summary>
        /// Compute axis-aligned bounding box from point positions.
        /// Uses Vector3 min/max for performance.
        /// </summary>
        private static Bounds ComputeBounds(PointStruct[] points)
        {
            if (points.Length == 0) { return new Bounds(Vector3.zero, Vector3.zero); }

            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            for (int i = 0; i < points.Length; i++)
            {
                ref readonly PointStruct p = ref points[i];

                if (p.x < min.x) min.x = p.x;
                if (p.y < min.y) min.y = p.y;
                if (p.z < min.z) min.z = p.z;

                if (p.x > max.x) max.x = p.x;
                if (p.y > max.y) max.y = p.y;
                if (p.z > max.z) max.z = p.z;
            }

            Vector3 center = (min + max) * 0.5f;
            Vector3 size = max - min;

            return new Bounds(center, size);
        }

        /// <summary>
        /// Get subset of points for debugging/testing.
        /// </summary>
        /// <param name="startIndex">Starting index in the point array.</param>
        /// <param name="count">Maximum number of points to retrieve.</param>
        /// <returns>Array containing the requested subset. May be smaller than count if bounds exceeded.</returns>
        public PointStruct[] GetSubset(int startIndex, int count)
        {
            count = Mathf.Min(count, _pointCount - startIndex);
            if (count <= 0) { return System.Array.Empty<PointStruct>(); }

            PointStruct[] subset = new PointStruct[count];
            System.Array.Copy(_points, startIndex, subset, 0, count);
            return subset;
        }

        public override string ToString()
        {
            return $"PointCloudData: {_pointCount:N0} points, Bounds: {_bounds}, Memory: {MemorySize / (1024f * 1024f):F2} MB";
        }

    }
}