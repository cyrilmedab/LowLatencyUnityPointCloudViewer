#ifndef POINTCLOUD_COMMON_INCLUDED
#define POINTCLOUD_COMMON_INCLUDED

// Point data structure - must match C# PointStruct layout
struct PointData
{
    float x;
    float y;
    float z;
    uint rgba;
};

// Unpack RGBA from uint (R in low byte)
float4 UnpackColor(uint packed)
{
    float4 c;
    c.r = (packed & 0xFF) / 255.0;
    c.g = ((packed >> 8) & 0xFF) / 255.0;
    c.b = ((packed >> 16) & 0xFF) / 255.0;
    c.a = ((packed >> 24) & 0xFF) / 255.0;
    return c;
}

#endif // POINTCLOUD_COMMON_INCLUDED
