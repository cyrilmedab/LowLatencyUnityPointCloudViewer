using UnityEngine;
using PointCloudViewer;

namespace PointCloudViewer.Rendering
{
    /// <summary>
    /// Statistics from point cloud rendering.
    /// Designed to be allocation-free when used as struct.
    /// </summary>
    public struct RenderStats
    {
        /// <summary>Total points in the data set.</summary>
        public int TotalPoints;

        /// <summary>Points actually rendered this frame.</summary>
        public int RenderedPoints;

        /// <summary>Time spent on CPU preparation (ms).</summary>
        public float CpuPrepTimeMs;

        /// <summary>Estimated GPU render time (ms). May be 0 if not measurable.</summary>
        public float GpuRenderTimeMs;

        /// <summary>Current frame time (ms).</summary>
        public float FrameTimeMs;

        /// <summary>Current frames per second.</summary>
        public float Fps;

        /// <summary>Memory used by point data (MB).</summary>
        public float MemoryUsedMB;

        /// <summary>Name of the active renderer.</summary>
        public string RendererName;

        /// <summary>Whether any optimizations are active.</summary>
        public bool OptimizationsEnabled;

        /// <summary>Whether culling is currently enabled.</summary>
        public bool CullingEnabled;

        /// <summary>Points culled this frame.</summary>
        public int CulledPoints;

        /// <summary>Active optimization statistics (culling, decimation, etc.).</summary>
        //public OptimizationStats[] ActiveOptimizations;

        /// <summary>Calculate cull percentage.</summary>
        public float CullPercentage => TotalPoints > 0 ? (CulledPoints / (float)TotalPoints) * 100f : 0f;

        /// <summary>
        /// Render efficiency (rendered / total).
        /// </summary>
        public float RenderEfficiency => TotalPoints > 0 ? (RenderedPoints / (float)TotalPoints) * 100f : 0f;

        public override string ToString()
        {
            return $"[{RendererName}] {RenderedPoints:N0}/{TotalPoints:N0} points " +
                   $"({RenderEfficiency:F1}%) | {Fps:F1} FPS | CPU: {CpuPrepTimeMs:F2}ms";
        }


        /// <summary>Reset stats for new frame.</summary>
        public void Reset()
        {
            CpuPrepTimeMs = 0;
            GpuRenderTimeMs = 0;
            CulledPoints = 0;
            RenderedPoints = 0;
        }
    }

    public class FrameTimeTracker
    {
        private float _emaFps;
        private float _emaFrameTime;
        private readonly float _smoothingFactor;

        public float SmoothedFps => _emaFps;
        public float SmoothedFrameTimeMs => _emaFrameTime;
        public float InstantFps { get; private set; }
        public float InstantFrameTimeMs { get; private set; }

        public FrameTimeTracker(float smoothingFactor = 0.1f)
        {
            _smoothingFactor = Mathf.Clamp01(smoothingFactor);
            _emaFps = 60f;
            _emaFrameTime = 16.67f;
        }

        /// <summary>Update with current frame's delta time.</summary>
        public void Update(float deltaTime)
        {
            InstantFrameTimeMs = deltaTime * 1000f;
            InstantFps = deltaTime > 0 ? 1f / deltaTime : 0f;

            // EMA update
            _emaFrameTime += _smoothingFactor * (InstantFrameTimeMs - _emaFrameTime);
            _emaFps += _smoothingFactor * (InstantFps - _emaFps);
        }
    }
}