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

### Vertex Data

Currently, all generation functions produce zero vertex attributes, except of position.
Triangulation is included, although all generated meshes are smooth shaded where possible.

## Benchmarks

Configuration: Unity 6000.7.0b4 (Editor), Intel Core i5-11400F, 100000 iterations, 10 runs median.

Timings refresher: `1 s = 1000 ms, 1 ms = 1000 us, 1 us = 1000 ns.`

### Mesh Data Generation

Pre-allocated `NativeArray`'s, calls to shape-specific `MeshGenerator.Generate{Shape}MeshData` function
only. Zero managed allocations, pure generation performance.

| Shape                               | Timings | Geometry Data       |
|-------------------------------------|:-------:|---------------------|
| Hollow Cone (64 resolution)         | 116 ns  | 65 verts, 64 tris   |
| Hollow Cone (256 resolution)        | 256 ns  | 257 verts, 256 tris |
| Hollow Half-Sphere (64 resolution)  | 305 ns  | 125 verts, 184 tris |
| Hollow Half-Sphere (256 resolution) | 853 ns  | 509 verts, 760 tris |
| Hollow Half-Sphere (Custom-78)      | 402 ns  | 78 verts, 134 tris  |
| Hollow Half-Sphere (Custom-214)     | 843 ns  | 214 verts, 382 tris |

There are still a lot of potential improvements that can be added to boost the generation algorithm,
but I am almost certain that those numbers are good enough for the purpose of this plugin.

### Descriptor + Mesh Object

Nothing is pre-allocated, full generation lifecycle: create `{Shape}MeshDescriptor`, pass it to a mesh
object generation function, attach mesh to a `MeshFilter.sharedMesh`. Time required to destroy the
generated mesh or upload mesh data on the GPU are not accounted.

| Shape                               | Timings  | Geometry Data       |
|-------------------------------------|:--------:|---------------------|
| Hollow Cone (64 resolution)         | 4.181 us | 65 verts, 64 tris   |
| Hollow Cone (256 resolution)        | 4.509 us | 257 verts, 256 tris |
| Hollow Half-Sphere (64 resolution)  | 4.637 us | 125 verts, 184 tris |
| Hollow Half-Sphere (256 resolution) | 5.163 us | 509 verts, 760 tris |
| Hollow Half-Sphere (Custom-78)      | 5.078 us | 78 verts, 134 tris  |
| Hollow Half-Sphere (Custom-214)     | 5.736 us | 214 verts, 382 tris |

`Custom-78` sphere mesh was generated from this set of bands: `20, 16, 13, 10, 8, 6, 4`.
![Custom Half-Sphere](resources/images/Half-Sphere(Custom-78).png)

`Custom-214` sphere mesh was generated from this set of bands: `44, 36, 31, 25, 20, 16, 13, 10, 8, 6, 4`.
![Custom Half-Sphere.png](resources/images/Half-Sphere(Custom-214).png)

> [!NOTE]
> Expect better numbers inside the Player build, as Burst introduces significant overhead tied to
> calling Burst-generated functions in editor (especially if they contain conditional scopes with
> different code paths for different CPU instruction sets, e.g. `X86.Avx.IsAvxSupported`).

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

// Your code goes here...

// Don't forget to call dispose when you're done with generated mesh data
vertices.Dispose();
indices.Dispose();
```

> [!NOTE]
> Some of the descriptors may inherit from `IDisposable`, so don't forget to call `Dispose()` method on
> them, or wrap such structures inside a `using` block.

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
    - _Direct Link:_ [Unity-Data-Shards-Installer.unitypackage](https://github.com/Saesentsessis/Unity-MeshGen/releases/download/0.1.0/Unity-MeshGen-Installer.unitypackage)
2. Import the downloaded package into your Unity project.
3. The installer will automatically configure OpenUPM in your `manifest.json` file and install the package dependencies.

### Method 3: Manual installation

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
