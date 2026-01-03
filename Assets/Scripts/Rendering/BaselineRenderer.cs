using UnityEngine;
using PointCloudViewer.Core;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using JetBrains.Annotations;

namespace PointCloudViewer.Rendering
{
    /// <summary>
    /// Baseline mesh-based point renderer.
    /// Intentionally simple/slow to serve as performance comparison.
    /// Rebuilds mesh every frame for worst-case baseline.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class BaselineRenderer : MonoBehaviour, IPointRenderer
    {
        #region Member Variables

        [SerializeField] private Material _pointMaterial;
        [SerializeField] private float _pointSize = 0.02f;

        private Mesh _mesh;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;

        private PointCloudData _data;
        private RenderStats _stats;
        private FrameTimeTracker _frameTracker;
        private Stopwatch _cpuTimer;

        private Vector3[] _vertices;
        private Color32[] _colors;
        private int[] _indices;

        // Visible point tracking for culling
        private int[] _visibleIndices;
        private int _visibleCount;
        private bool _useVisibleSubset;

        /// <inheritdoc/>
        public string RendererName => "Baseline (Mesh)";

        /// <inheritdoc/>
        public bool IsActive => enabled && gameObject.activeInHierarchy;

        /// <inheritdoc/>
        public float PointSize { get => _pointSize; set => _pointSize = Mathf.Max(0.001f, value); }

        #endregion

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            _mesh = new Mesh { name = "PointCloudMesh" };
            _mesh.MarkDynamic(); // Hint for frequent updates
            _meshFilter.mesh = _mesh;

            _frameTracker = new FrameTimeTracker();
            _cpuTimer = new Stopwatch();

            if (_pointMaterial != null)
            {
                _meshRenderer.material = _pointMaterial;
            }
        }

        /// <inheritdoc/>
        public void Initialize(PointCloudData data)
        {
            _data = data;

            if (_data == null || _data.PointCount == 0) 
            {
                Debug.LogWarning("BaselineRenderer: No data to initialize.");
                return;
            }

            int count = _data.PointCount;

            // Pre-allocate reusable arrays
            _vertices = new Vector3[count];
            _colors = new Color32[count];
            _indices = new int[count];

            // Setup mesh strucutre
            _mesh.Clear();
            _mesh.indexFormat = count > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            // Initial population
            for (int i = 0; i < count; i++)
            {
                _vertices[i] = _data.Points[i].Position;
                _colors[i] = _data.Points[i].Color;
                _indices[i] = i;
            }

            _mesh.vertices = _vertices;
            _mesh.colors32 = _colors;
            _mesh.SetIndices(_indices, MeshTopology.Points, 0);
            _mesh.bounds = _data.Bounds;

            _visibleCount = count;
            _useVisibleSubset = false;

            _stats.TotalPoints = count;
            _stats.RendererName = RendererName;
            _stats.MemoryUsedMB = _data.MemorySize / (1024f * 1024f);

            Debug.Log($"BaselineRenderer initialized with {count:N0} points");
        }

        /// <inheritdoc/>
        public void Render()
        {
            if (_data == null || !IsActive) return;

            _cpuTimer.Restart();

            // In the unoptimized baseline mode, we're rebuilding the mesh every frame
            // Demonstrating the cost of CPU-side mesh operations
            RebuildMesh();

            _cpuTimer.Stop();

            _frameTracker.Update(Time.deltaTime);

            _stats.CpuPrepTimeMs = (float)_cpuTimer.Elapsed.TotalMilliseconds;
            _stats.FrameTimeMs = _frameTracker.SmoothedFrameTimeMs;
            _stats.Fps = _frameTracker.SmoothedFps;
            _stats.RenderedPoints = _useVisibleSubset ? _visibleCount : _data.PointCount;
            _stats.CulledPoints = _data.PointCount - _stats.RenderedPoints;

        }

        private void RebuildMesh()
        {
            if (_useVisibleSubset && _visibleIndices != null)
            {
                // Rebuild with visible subset only
                for (int i = 0; i < _visibleCount; i++)
                {
                    int srcIdx = _visibleIndices[i];
                    _vertices[i] = _data.Points[srcIdx].Position;
                    _colors[i] = _data.Points[srcIdx].Color;
                    _indices[i] = i;
                }

                _mesh.Clear();
                _mesh.SetVertices(_vertices, 0, _visibleCount);
                _mesh.SetColors(_colors, 0, _visibleCount);
                _mesh.SetIndices(_indices, 0, _visibleCount, MeshTopology.Points, 0);
            }
            else
            {
                // Full rebuild - update all points
                for (int i = 0; i < _data.PointCount; i++)
                {
                    _vertices[i] = _data.Points[i].Position;
                    _colors[i] = _data.Points[i].Color;
                }

                // Use Clear() to ensure consistent state when switching from culled to full rendering
                _mesh.Clear();
                _mesh.SetVertices(_vertices, 0, _data.PointCount);
                _mesh.SetColors(_colors, 0, _data.PointCount);
                _mesh.SetIndices(_indices, 0, _data.PointCount, MeshTopology.Points, 0);
            }

            _mesh.bounds = _data.Bounds;
        }

        /// <inheritdoc/>
        public void UpdateVisiblePoints(int[] visibleIndices, int visibleCount)
        {
            _visibleIndices = visibleIndices;
            _visibleCount = Mathf.Min(visibleCount, visibleIndices?.Length ?? 0);
            _useVisibleSubset = _visibleIndices != null && _visibleCount > 0;
            _stats.CullingEnabled = _useVisibleSubset;
        }

        /// <inheritdoc/>
        public RenderStats GetStats() => _stats;

        /// <inheritdoc/>
        public void SetActive(bool active)
        {
            enabled = active;
            _meshRenderer.enabled = active;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_mesh != null)
            {
                if (Application.isPlaying)
                    Destroy(_mesh);
                else
                    DestroyImmediate(_mesh);
                _mesh = null;
            }

            _vertices = null;
            _colors = null;
            _indices = null;
            _visibleIndices = null;
        }

        private void OnDestroy()
        {
            Dispose();
        }
    }
}