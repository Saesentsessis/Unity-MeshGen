<div align="center">
    <h1>Unity MeshGen</h1>

[![OpenUPM](https://img.shields.io/npm/v/com.saesentsessis.unity-meshgen?label=OpenUPM&registry_uri=https://package.openupm.com&labelColor=333A41 'OpenUPM package')](https://openupm.com/packages/com.saesentsessis.unity-meshgen/)
[![Unity Editor](https://img.shields.io/badge/Editor-X?style=flat&logo=unity&labelColor=333A41&color=2A2A2A 'Unity Editor supported')](https://unity.com/releases/editor/archive)
[![Unity Runtime](https://img.shields.io/badge/Runtime-X?style=flat&logo=unity&labelColor=333A41&color=2A2A2A 'Unity Runtime supported')](https://unity.com/releases/editor/archive)
[![Tests Passed](https://github.com/Saesentsessis/Unity-MeshGen/actions/workflows/release.yml/badge.svg 'Tests Passed')](https://github.com/Saesentsessis/Unity-MeshGen/actions/workflows/release.yml)<br/>
[![Releases](https://img.shields.io/github/release/Saesentsessis/Unity-MeshGen.svg)](https://github.com/Saesentsessis/Unity-MeshGen/releases)
[![Stars](https://img.shields.io/github/stars/Saesentsessis/Unity-MeshGen 'Stars')](https://github.com/Saesentsessis/Unity-MeshGen/stargazers)
[![License](https://img.shields.io/github/license/Saesentsessis/Unity-MeshGen?label=License&labelColor=333A41)](https://github.com/Saesentsessis/Unity-MeshGen/blob/main/LICENSE)

</div>

**Unity MeshGen** is a fast, Burst-compatible library for generating simple procedural meshes in Unity.

## Key Types

| Type                 | Description                                                            |
|----------------------|------------------------------------------------------------------------|
| `MeshGenerator`      | Main entry point for all of the mesh generation functions              |
| `MeshGeneratorUtils` | A pack of vectorized helper functions that are used in mesh generation |

## Supported Shapes

| Shape              | Descriptor                       | Function                                             |
|--------------------|----------------------------------|------------------------------------------------------|
| Hollow Cone        | `HollowConeMeshDescriptor`       | `MeshGenerator.CreateHollowConeMeshDescriptor`       |
| Hollow Half-sphere | `HollowHalfSphereMeshDescriptor` | `MeshGenerator.CreateHollowHalfSphereMeshDescriptor` |

> [!NOTE]
> Any shape descriptor that has `Hollow` prefix in it comes with a hole. For example, a cone
> would not have a base filled with triangles, a half-sphere would not have a cap on the equator.

## Quick Start

Create a specific mesh descriptor - compact data structure that would be passed right inside the
generation function.

```csharp
using Saesentsessis.MeshGeneration;
using Saesentsessis.MeshGeneration.Shapes;

var resolution = 16;
var halfAngleDegrees = 45f;

// Create a reusable descriptor, that describes the shape
var descriptor = MeshGenerator.CreateHollowConeMeshDescriptor(resolution, halfAngleDegrees);

// Then, either generate the mesh via an extension method
var coneMesh = descriptor.CreateMesh();

// Or call the method manually, result would be the same
var coneMesh2 = MeshGenerator.CreateHollowConeMesh(in descriptor);
```

You can also generate mesh data exclusively inside two native arrays - vertices and indices.

```csharp
using Unity.Collections;
using Unity.Mathematics;

// Allocate the resulting arrays
var vertices = new NativeArray<float3>(descriptor.VertexCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
var indices = new NativeArray<ushort>(descriptor.IndexCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

// Call the shape-specific function, passing a descriptor, vertices and indices array
MeshGenerator.GenerateHollowConeMeshData(in descriptor, vertices, indices);

// Now you can create your own mesh object or do whatever you want with the generated data
// After you've done with it, don't forget to call dispose on the arrays
vertices.Dispose();
indices.Dispose();
```

> [!NOTE]
> Some of the descriptors may inherit from `IDisposable`, so don't forget to call `Dispose()` method on
> them, or wrap such structures inside a `using` block.

Some of the shapes may have special descriptor creation functions. Let's take as example a `HollowHalfSphere`.
`MeshGenerator.CreateHollowHalfSphereMeshDescriptor` has several overloads for this function, including the
generation of the half-sphere from custom array of vertex loop sizes, that is represented by `NativeArray<int>`.

```csharp
// Allocate a buffer that would contain vertex loop sizes
using var bands = new NativeArray<int>(5, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

// It is not necessary to fill loop sizes in descending order
bands[0] = 12;
bands[1] = 9;
bands[2] = 7;
bands[3] = 5;
bands[4] = 4;

// This method would create a descriptor from pre-defined set of vertex loops,
// from 12 vertices at the equator down to a single vertex at apex.
var descriptor = MeshGenerator.CreateHollowHalfSphereMeshDescriptor(bands);

// Then you can generate mesh or mesh data using the examples provided above...
var mesh = descriptor.CreateMesh();
```

## Vertex Data

Currently, all generation functions produce no vertex attributes other than position.
Triangulation is included, although all generated meshes lack normals. Index size is
selected depending on vertex buffer capacity (16-bit indices for meshes with `<= 65536`
verts, otherwise 32-bit indices).

## Benchmarks

Configuration: Unity 6000.7.0b4 (Editor), Intel Core i5-11400F.
50 warmup iterations, 100000 iterations, 10 runs median.
Burst safety checks and leak detection enabled.

### Mesh Data Generation

**Baseline**: A straightforward managed implementation. No Burst, no Jobs, no native memory, no
`unsafe` context.

**MeshGen**: Pre-allocated `NativeArray`'s, calls to shape-specific `MeshGenerator.Generate{Shape}MeshData`
function only. Zero managed allocations, pure generation performance.

| Shape                               | MeshGen | Baseline  | Speedup | Baseline GC Alloc (per call) | Geometry Data       |
|-------------------------------------|:-------:|:---------:|:-------:|:----------------------------:|---------------------|
| Hollow Cone (64 resolution)         | 116 ns  | 3.045 us  |  26.5x  |           1.51 KiB           | 65 verts, 64 tris   |
| Hollow Cone (256 resolution)        | 256 ns  | 11.639 us |  44.7x  |           6.01 KiB           | 257 verts, 256 tris |
| Hollow Half-Sphere (64 resolution)  | 305 ns  | 7.129 us  |  23.9x  |           3.62 KiB           | 125 verts, 184 tris |
| Hollow Half-Sphere (256 resolution) | 853 ns  | 33.011 us |  38.9x  |          14.87 KiB           | 509 verts, 760 tris |
| Hollow Half-Sphere (Custom-78)      | 389 ns  | 4.577 us  |  11.8x  |           2.48 KiB           | 78 verts, 134 tris  |
| Hollow Half-Sphere (Custom-214)     | 840 ns  | 12.148 us |  14.5x  |           6.98 KiB           | 214 verts, 382 tris |

Further optimizations are possible. See the roadmap.

### Descriptor + Mesh Object

**Baseline**: A straightforward managed implementation. No Burst, no Jobs, no native memory, no
`unsafe` context.

**MeshGen**: Nothing is pre-allocated, full generation lifecycle: create `{Shape}MeshDescriptor`,
pass it to a mesh object generation function, attach mesh to a `MeshFilter.sharedMesh`. Time
required to destroy the generated mesh or upload mesh data on the GPU are not accounted.

| Shape                               | MeshGen  | Baseline  | Speedup | Baseline GC Alloc | Geometry Data       |
|-------------------------------------|:--------:|:---------:|:-------:|:-----------------:|---------------------|
| Hollow Cone (64 resolution)         | 4.175 us | 6.996 us  |  1.7x   |     1.51 KiB      | 65 verts, 64 tris   |
| Hollow Cone (256 resolution)        | 4.313 us | 17.170 us |  4.0x   |     6.01 KiB      | 257 verts, 256 tris |
| Hollow Half-Sphere (64 resolution)  | 4.637 us | 12.197 us |  2.7x   |     3.62 KiB      | 125 verts, 184 tris |
| Hollow Half-Sphere (256 resolution) | 5.163 us | 43.462 us |  8.5x   |     14.87 KiB     | 509 verts, 760 tris |
| Hollow Half-Sphere (Custom-78)      | 4.730 us | 9.061 us  |  1.9x   |     2.48 KiB      | 78 verts, 134 tris  |
| Hollow Half-Sphere (Custom-214)     | 5.371 us | 18.392 us |  3.4x   |     6.98 KiB      | 214 verts, 382 tris |

As you can see, most of the time (around 4.1 us) was spent on Mesh object creation in all test cases,
despite the usage of Unity's `AdvancedMeshAPI`. You can run those test yourself, by attaching `MeshGenBenchmark`
to an empty game object, then pressing play. Results would be logged inside the Console window.

> [!WARNING]
> Results on the other hardware will vary. Run the benchmarks yourself to reproduce.

> [!NOTE]
> Expect better numbers inside the Player build, as Burst introduces significant overhead tied to
> calling Burst-generated functions in editor (especially if they contain conditional scopes with
> different code paths for different CPU instruction sets, e.g. `X86.Avx.IsAvxSupported`).

## Requirements

- Unity **6000.0** or newer
- [`com.unity.collections`](https://docs.unity3d.com/Packages/com.unity.collections@latest) **2.1.4** or newer
- [`com.unity.burst`](https://docs.unity3d.com/Packages/com.unity.burst@latest) **1.8.0** or newer

## Installation

### Method 1: OpenUPM (Recommended)

You can install this package via the [OpenUPM](https://openupm.com/) CLI:

```bash
openupm add com.saesentsessis.unity-meshgen
```

Or manually add the scoped registry to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.saesentsessis.unity-meshgen": "0.1.0"
  },
  "scopedRegistries": [
    {
      "name": "OpenUPM",
      "url": "https://package.openupm.com",
      "scopes": [
        "com.saesentsessis"
      ]
    }
  ]
}
```

### Method 2: Unity package installer

1. Download the latest `.unitypackage` from [GitHub Releases page](https://github.com/Saesentsessis/Unity-MeshGen/releases).
   - _Direct Link:_ [Unity-MeshGen-Installer.unitypackage](https://github.com/Saesentsessis/Unity-MeshGen/releases/download/0.1.0/Unity-MeshGen-Installer.unitypackage)
2. Import the downloaded package into your Unity project.
3. The installer will automatically configure OpenUPM in your `manifest.json` file and install the package dependencies.

### Method 3: Install via Git URL

1. Open Unity and navigate to `Window` -> `Package Manager`.
2. Click on the `+` icon in the top left corner and select `Add package from git URL...`.
3. ```
   https://github.com/Saesentsessis/Unity-MeshGen.git?path=Unity-MeshGen/Assets/root
   ```
4. Click Add.

You can specify exact release version of this package like this:

```
https://github.com/Saesentsessis/Unity-MeshGen.git?path=Unity-MeshGen/Assets/root#0.1.0
```

## License

Licensed under the [MIT License](LICENSE).
