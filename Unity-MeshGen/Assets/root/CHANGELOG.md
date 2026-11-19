# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.0] - 2026-11-20

### Changed

- `CreateHollowConeMesh` rejects resolutions below 3 (they made degenerate meshes) and above 715827882 (their index
  count overflows an int) with an `ArgumentOutOfRangeException`. It used to accept 0 to 2147483646.
- `CreateHollowHalfSphereMeshDescriptor(NativeArray<int>)` checks every band's vertex count, the first one included,
  throws an `ArgumentException` for an empty band list (it used to fail with an index error), and throws an
  `OverflowException` when the index count exceeds `int.MaxValue` (it used to allow up to `uint.MaxValue`).

### Fixed

- Compile error on Unity 6000.0 and 6000.1: `MeshUpdateFlags.DontValidateLodRanges` exists only from 6000.2 (with
  Mesh LODs), so it is now used from 6000.2 on.
- `baseVertexIndex` was ignored in places: the cone's first triangle, the half-sphere's 16-bit overloads (never
  passed on) and the half-sphere's apex index (every overload).
- The editor test assembly compiles only when the Test Framework is present (`UNITY_INCLUDE_TESTS`).

### Added

- EditMode tests for both shapes: descriptor counts and validation, vertex placement, topology (closed except at
  the rim, consistent and outward winding, no degenerate triangles), bounds, index formats, the band-ownership rules
  and every `Generate*MeshData` overload with and without `baseVertexIndex`.

## [0.1.0] - 2026-10-28

### Added

- Initial preview release of Unity MeshGen.
- **Shape descriptors**:
  - Hollow cone (HollowConeMeshDescriptor).
  - Hollow half-sphere (HollowHalfSphereMeshDescriptor).
- **Generation functions**:
  - Hollow cone (MeshGenerator.GenerateHollowCone).
  - Hollow half-sphere (MeshGenerator.GenerateHollowHalfSphere).

[0.2.0]: https://github.com/Saesentsessis/Unity-MeshGen/releases/tag/0.2.0
[0.1.0]: https://github.com/Saesentsessis/Unity-MeshGen/releases/tag/0.1.0