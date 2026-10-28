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

**Unity MeshGen** is a helper mesh generation library, providing a very fast simple shape generation
capabilities.

## Key Types

| Type                 | Description                                                            |
|----------------------|------------------------------------------------------------------------|
| `MeshGenerator`      | Main entry point for all of the mesh generation functions              |
| `MeshGeneratorUtils` | A pack of vectorized helper functions that are used in mesh generation |

## Quick Start

Create a specific mesh descriptor - compact data structure that would be passed right inside the
generation function.

```csharp
using Saesentsessis.MeshGeneration;
using Saesentsessis.MeshGeneration.Shapes;

var resolution = 16;
var halfAngleDegrees = 45f;

// Create a reusable descriptor, that describes the shape
var descriptor = MeshGenerator.CreateConeMeshDescriptor(resolution, halfAngleDegrees);

// Then, either generate the mesh via an extension method
var coneMesh = descriptor.GenerateMesh();

// Or call the method manually, result would be the same
var coneMesh2 = MeshGenerator.GenerateHollowConeMesh(in descriptor);
```

You can also generate mesh data exclusively inside two native arrays - vertices and indices.

```csharp
// Allocate the resulting arrays
var vertices = new NativeArray(descriptor.VertexCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
var indices = new NativeArray(descriptor.IndexCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

// Call the shape-specific function, passing a descriptor, vertices and indices array
MeshGenerator.GenerateHollowConeMeshData(in descriptor, vertices, indices);
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
   https://github.com/Saesentsessis/Unity-MeshGen.git?path=Unity-Data-Shards/Assets/root
   ```
4. Click Add.

You can specify exact release version of this package like this:

```
https://github.com/Saesentsessis/Unity-Data-Shards.git?path=Unity-MeshGen/Assets/root#0.1.0
```

## License

Licensed under the [MIT License](LICENSE).
