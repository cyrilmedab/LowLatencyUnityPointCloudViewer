using PointCloudViewer.Core;

namespace PointCloudViewer.Rendering
{
    /// <summary>
    /// Interface for different types of point cloud renderers.
    /// Enables modular swapping between baseline and GPU-optimized implementations
    /// </summary>
    public interface IPointRenderer
    {
        /// <summary>Human-readable name for UI display.</summary>
        string RendererName { get; }

        /// <summary>Whether this renderer is currently active.</summary>
        bool IsActive { get; }

        /// <summary>Current point size in world-space units.</summary>
        float PointSize { get; set;  }

        /// <summary>
        /// Initialize the renderer with point cloud data.
        /// Called once when data is loaded.
        /// </summary>
        /// <param name="data">Point cloud to render</param>
        void Initialize(PointCloudData data);

        /// <summary>
        /// Render the point cloud.
        /// Called every frame when renderer is active.
        /// </summary>
        void Render();

        /// <summary>
        /// Update visible point indices (for culling).
        /// Optional: not all renderers will support indexed rendering.
        /// </summary>
        /// <param name="visibleIndices">Indices of visible points.</param>
        /// <param name="visibleCount">Number of valid indices.</param>
        void UpdateVisiblePoints(int[] visibleIndices, int visibleCount);

        /// <summary>Get current rendering statistics.</summary>
        RenderStats GetStats();

        /// <summary>
        /// Enable or disable the renderer
        /// </summary>
        /// <param name="active"></param>
        void SetActive(bool active);

        /// <summary>
        /// Release all resources.
        /// Called when switching renderers or shutting down.
        /// </summary>
        void Dispose();
    }
}