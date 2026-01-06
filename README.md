# Low-Latency Unity Point Cloud Viewer

**A performance-focused point cloud visualization system demonstrating GPU-driven rendering pipelines and real-time optimization techniques.**

---

## Project Status

| Component              | Status                                                  |
| ---------------------- | ------------------------------------------------------- |
| Architecture & Design  | Complete                                                |
| Core Data Structures   | Complete                                                |
| Test Data Generation   | Complete (100K, 500K, 1M point datasets)                |
| Rendering Scripts      | Written                                                 |
| CPU Optimizations      | Written                                                 |
| GPU Compute Shaders    | Written                                                 |
| **Shader Compilation** | **In Progress** - HLSL include/struct resolution issues |
| Performance Profiling  | Blocked by shader issues                                |
| Demo Video             | Pending                                                 |

**Current Blocker:** Shaders are not properly recognizing shared struct definitions from separate HLSL include files. Once resolved, profiling and performance validation can proceed.

---

## Purpose

This project was built as a technical demonstration for [Dyna Robotics](https://dyna.co), targeting their Senior Software Engineer - Spatial Computing role. The position requires:

- 60+ FPS rendering of point clouds and depth maps
- Low-latency spatial visualization for robotic teleoperation
- Performance-critical Unity development

This viewer demonstrates architectural thinking around these constraints, even in prototype form.

---

## What This Project Demonstrates

1. **GPU-Driven Rendering** - Eliminates per-frame mesh rebuilding via `ComputeBuffer` and `Graphics.DrawProceduralIndirect`
2. **Compute Shader Optimization Pipeline** - Frustum culling and decimation executed entirely on GPU
3. **Zero-Allocation Design** - Pre-allocated buffers, cached shader property IDs, reusable index arrays
4. **Plugin Architecture** - Swappable renderers and optimizations via interfaces (`IPointRenderer`, `IOptimization`)
5. **Performance Instrumentation** - Real-time diagnostics overlay with FPS, frame time, cull percentages

---

## Architecture Overview

```
PointCloudManager (Orchestrator)
|-- Loaders
|   +-- BinaryPointCloudLoader
|-- Renderers (3 implementations)
|   |-- BaselineRenderer      -- CPU mesh rebuild (slow, for comparison)
|   |-- GPUPointRenderer      -- GPU-driven via RenderPrimitives
|   +-- ComputeShaderRenderer -- DrawProceduralIndirect + compute shaders
|-- CPU Optimizations
|   |-- FrustumCuller
|   +-- Decimator (stride/ratio/random/adaptive)
+-- GPU Optimizations (Compute Shaders)
    |-- FrustumCullingCS.compute
    +-- DecimationCS.compute
```

**Key Design Decision:** The `ComputeShaderPointRenderer` supports dual-mode operation:

- **CPU Mode** -- Uses existing C# optimizations, compatible with other renderers
- **GPU Mode** -- Full compute shader pipeline, 10-50x faster for large point clouds

This enables direct A/B comparison between CPU and GPU optimization strategies.

---

## Scope and Limitations

### What This Is

- A **vertical slice** demonstrating performance-critical rendering architecture
- A proof-of-concept for real-time spatial visualization
- Interview preparation material with documented design decisions

### What This Is Not

- A production-ready teleoperation system
- A networking/streaming implementation
- A complete robotics control stack

### Known Limitations

- Recorded/generated data only (no live sensor input)
- Desktop-only (no XR operator mode yet)
- Single point cloud at a time

---

## Technical Specifications

| Spec                     | Target                                   |
| ------------------------ | ---------------------------------------- |
| Frame Budget             | <=16.6 ms (60 FPS)                       |
| Point Count              | 100K - 1M+                               |
| Steady-State Allocations | 0 bytes/frame                            |
| Data Format              | Binary (16-byte blittable `PointStruct`) |

### Data Structure

```csharp
[StructLayout(LayoutKind.Sequential)]
public struct PointStruct  // 16 bytes, GPU-compatible
{
    public float x, y, z;  // Position
    public uint rgba;      // Packed color
}
```

---

## Project Structure

```
Assets/_Project/
|-- Scripts/
|   |-- Core/           # PointStruct, PointCloudData, PointCloudManager
|   |-- Rendering/      # IPointRenderer implementations
|   |-- Optimization/   # FrustumCuller, Decimator
|   |-- DataLoading/    # Binary loader, data generator
|   |-- Camera/         # OrbitCamera
|   +-- UI/             # DiagnosticsOverlay
|-- Shaders/
|   |-- PointCloud.shader
|   |-- PointCloudCompute.shader
|   |-- FrustumCullingCS.compute
|   +-- DecimationCS.compute
+-- Data/               # Generated test point clouds
```

---

## Controls

| Key                 | Action                                      |
| ------------------- | ------------------------------------------- |
| `R`                 | Cycle renderer (Baseline -> GPU -> Compute) |
| `C`                 | Toggle frustum culling                      |
| `D`                 | Toggle decimation                           |
| `F1`                | Toggle diagnostics overlay                  |
| Right Mouse + Drag  | Orbit camera                                |
| Middle Mouse + Drag | Pan                                         |
| Scroll Wheel        | Zoom                                        |

---

## Screenshots

<!-- TODO: Add screenshots once shaders compile -->

| Diagnostics Overlay | Point Cloud Rendering |
| ------------------- | --------------------- |
| _Placeholder_       | _Placeholder_         |

---

## Requirements

- **Unity Version:** 6000.2.15f1
- **Render Pipeline:** URP
- **Platform:** Windows/Mac (Desktop)

---

## Quick Start (Not Functional At The Moment)

1. Clone the repository
2. Open in Unity 6000.2.15f1
3. Open the main scene: `Assets/_Project/Scenes/Main.unity`
4. Press Play
5. Use `R`, `C`, `D` keys to switch renderers and toggle optimizations

To generate new test data:

- `Tools -> Point Cloud -> Generate Test Data`

---

## Planned Next Steps

Once shader compilation is resolved:

1. **Profile all three renderers** with 100K, 500K, 1M point datasets
2. **Document performance deltas** (baseline vs GPU vs compute)
3. **Record demo video** showing smooth 60 FPS with 1M points
4. **Validate zero-allocation claim** via Memory Profiler

### Future Extensions (Out of Scope)

- Live network ingestion (UDP/WebRTC)
- Depth map -> point cloud reconstruction
- XR operator interface
- Multi-sensor fusion

---

## License

MIT License

---

## Author

**Cyril Medabalimi**  
[LinkedIn](https://www.linkedin.com/in/cyril-medab/) | [GitHub](https://github.com/cyrilmedab)
