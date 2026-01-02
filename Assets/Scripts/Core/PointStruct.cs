using System.Runtime.InteropServices;
using UnityEngine;

namespace PointCloudViewer.Core
{
    /// <summary>
    /// Blittable 16-byte point structure optimized for GPU buffer uploads.
    /// Layout arranged sequentially to match future shaders exactly
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PointStruct
    {
        public float x;
        public float y;
        public float z;
        public uint rgba; // Packed RGBA

        public const int STRIDE = 16; // 3 floats + 1 uint (4) = 16 bytes

        /// <summary>
        /// Creates a point from raw float coordinates and packed color.
        /// </summary>
        /// <param name="x">X position in world space.</param>
        /// <param name="y">Y position in world space.</param>
        /// <param name="z">Z position in world space.</param>
        /// <param name="rgba">Packed RGBA color (R in low byte, A in high byte).</param>
        public PointStruct(float x, float y, float z, uint rgba)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.rgba = rgba;
        }

        /// <summary>
        /// Creates a point from Unity Vector3 and Color32.
        /// </summary>
        /// <param name="position">Position in world space.</param>
        /// <param name="color">Point color.</param>
        public PointStruct(Vector3 position, Color32 color)
        {
            x = position.x;
            y = position.y;
            z = position.z;
            rgba = PackColor(color);
        }

        /// <summary>Gets or sets the point position as a Unity Vector3.</summary>
        public Vector3 Position
        {
            get => new Vector3(x, y, z);
            set { x = value.x; y = value.y; z = value.z; }
        }

        /// <summary>Gets or sets the point color as a Unity Color32.</summary>
        public Color32 Color
        {
            get => UnpackColor(rgba);
            set => rgba = PackColor(value);
        }

        /// <summary>
        /// Pack Color32 into uint (RGBA order, R in low byte).
        /// </summary>
        /// <param name="c">Color to pack.</param>
        /// <returns>Packed color as uint.</returns>
        public static uint PackColor(Color32 color) => (uint)(color.r | (color.g << 8) | (color.b << 16) | (color.a << 24));

        /// <summary>
        /// Unpack uint to Color32
        /// </summary>
        /// <param name="packed">Packed RGBA color.</param>
        /// <returns>Unpacked Color32.</returns>
        public static Color32 UnpackColor(uint packed)
        {
            return new Color32(
                (byte)(packed & 0xFF),
                (byte)((packed >> 8) & 0xFF),
                (byte)((packed >> 16) & 0xFF),
                (byte)((packed >> 24) & 0xFF)
            );
        }

        public override string ToString()
        {
            return $"Point({x:F2}, {y:F2}, {z:F2}) RGBA:{rgba:X8}";
        }
    }
}