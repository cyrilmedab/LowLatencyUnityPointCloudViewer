using PointCloudViewer.Core;

namespace PointCloudViewer.Optimization
{
    /// <summary>
    /// Interface for point cloud optimization techniques.
    /// Optimizations filter the visible point set to improve performance.
    /// </summary>
    public interface IOptimization
    {
        /// <summary>Human-readable name for UI display.</summary>
        string OptimizationName { get; }

        /// <summary>Whether this optimization is currently enabled.</summary>
        bool IsEnabled { get; set; }

        /// <summary>
        /// Process the point cloud and update visible indices.
        /// </summary>
        /// <param name="data">Source point cloud data.</param>
        /// <param name="indices">Buffer for visible point indices. May contain previous results.</param>
        /// <param name="count">On input: current count. On output: updated count.</param>
        /// <returns>Statistics from this optimization pass.</returns>
        OptimizationStats Process(PointCloudData data, int[] indices, ref int count);

        /// <summary>Reset optimization state (e.g., when data changes).</summary>
        void Reset();

        /// <summary>Get the most recent statistics.</summary>
        OptimizationStats GetStats();
    }
}