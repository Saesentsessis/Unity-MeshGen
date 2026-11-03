using System;
using Saesentsessis.MeshGeneration.Shapes;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Saesentsessis.MeshGeneration
{
	[BurstCompile]
	public static partial class MeshGenerator
	{
		/// <summary>
		/// Creates a hollow cone shape descriptor, that could be passed into
		/// <see cref="CreateHollowConeMesh"/> function.
		/// </summary>
		/// <param name="baseResolution">Resolution of the cone base.</param>
		/// <param name="halfAngleRadians">Half-angle of the cone in radians.</param>
		/// <returns>A hollow cone descriptor.</returns>
		public static HollowConeMeshDescriptor CreateHollowConeMeshDescriptor(int baseResolution, float halfAngleRadians)
			=> new(baseResolution, halfAngleRadians);

		/// <summary>
		/// Generates hollow cone mesh data from <see cref="HollowConeMeshDescriptor"/>.
		/// There are zero safety checks inside this method, so the caller should ensure, that the provided
		/// native arrays are correctly sized and that the descriptor contains valid data.
		/// </summary>
		/// <param name="descriptor">Hollow cone descriptor, that can be obtained using <see cref="CreateHollowConeMeshDescriptor"/> function.</param>
		/// <param name="vertices">Vertex buffer, owned by the caller.</param>
		/// <param name="indices">Index buffer, owned by the caller.</param>
		public static unsafe void GenerateHollowConeMeshData(in HollowConeMeshDescriptor descriptor,
			NativeArray<float3> vertices, NativeArray<ushort> indices)
		{
			var verticesPtr = (float3*)vertices.GetUnsafePtr();
			var indicesPtr = (ushort*)indices.GetUnsafePtr();

			GenerateHollowConeMeshData(descriptor.Resolution, descriptor.HalfAngleRadians, verticesPtr, indicesPtr);
		}

		/// <summary>
		/// Generates hollow cone mesh data from <see cref="HollowConeMeshDescriptor"/>.
		/// There are zero safety checks inside this method, so the caller should ensure, that the provided
		/// native arrays are correctly sized and that the descriptor contains valid data.
		/// </summary>
		/// <param name="descriptor">Hollow cone descriptor, that can be obtained using <see cref="CreateHollowConeMeshDescriptor"/> function.</param>
		/// <param name="vertices">Vertex buffer, owned by the caller.</param>
		/// <param name="indices">Index buffer, owned by the caller.</param>
		public static unsafe void GenerateHollowConeMeshData(in HollowConeMeshDescriptor descriptor,
			NativeArray<float3> vertices, NativeArray<int> indices)
		{
			var verticesPtr = (float3*)vertices.GetUnsafePtr();
			var indicesPtr = (int*)indices.GetUnsafePtr();

			GenerateHollowConeMeshData(descriptor.Resolution, descriptor.HalfAngleRadians, verticesPtr, indicesPtr);
		}

		/// <summary>
		/// Uses AdvancedMeshAPI to populate the mesh data.
		/// </summary>
		/// <param name="descriptor">Hollow cone descriptor.</param>
		/// <returns>New hollow cone mesh instance, owned by the caller.</returns>
		public static Mesh CreateHollowConeMesh(in HollowConeMeshDescriptor descriptor)
		{
			const MeshUpdateFlags flags = MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds |
			                              MeshUpdateFlags.DontValidateIndices
#if UNITY_6000_OR_NEWER
			                              | MeshUpdateFlags.DontValidateLodRanges
#endif
				;

			if ((uint)descriptor.Resolution + 1 > int.MaxValue)
				throw new ArgumentOutOfRangeException(nameof(descriptor.Resolution), descriptor.Resolution,
					"Vertex count should be in range [3 : 2147483646].");

			if (descriptor.HalfAngleRadians is < 0.00174533f or > 1.553343f)
				throw new ArgumentOutOfRangeException(nameof(descriptor.HalfAngleRadians), 
					math.degrees(descriptor.HalfAngleRadians),
					"Half angle should be in range [0.01 : 89.9] degrees.");

			var meshDataArray = Mesh.AllocateWritableMeshData(1);
			var use32BitIndexing = descriptor.Resolution >= ushort.MaxValue;

			try
			{
				var meshData = meshDataArray[0];
				var indexCount = descriptor.IndexCount;

				meshData.SetVertexBufferParams(descriptor.VertexCount, new VertexAttributeDescriptor(VertexAttribute.Position));
				meshData.SetIndexBufferParams(indexCount, use32BitIndexing ? IndexFormat.UInt32 : IndexFormat.UInt16);

				var vertices = meshData.GetVertexData<float3>();

				if (use32BitIndexing)
				{
					var indices = meshData.GetIndexData<int>();

					GenerateHollowConeMeshData(descriptor, vertices, indices);
				}
				else
				{
					var indices = meshData.GetIndexData<ushort>();

					GenerateHollowConeMeshData(descriptor, vertices, indices);					
				}

				var bound = vertices[1].y * 2f;

				meshData.subMeshCount = 1;
				var subMesh = new SubMeshDescriptor(0, indexCount)
				{
					bounds = new Bounds(new Vector3(0f, 0f, 0.5f), new Vector3(bound, bound, 1f))
				};
				meshData.SetSubMesh(0, subMesh, flags);

				var result = new Mesh();
				Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, result, flags);
				result.bounds = subMesh.bounds;

				return result;
			}
			catch
			{
				meshDataArray.Dispose();
				throw;
			}
		}

		[Unity.Burst.CompilerServices.SkipLocalsInit]
		[BurstCompile(DisableSafetyChecks = true, FloatMode = FloatMode.Fast)]
		private static unsafe void GenerateHollowConeMeshData(int resolution, float halfAngleRadians,
			float3* verticesPtr, ushort* indicesPtr)
		{
			var radius = math.sin(halfAngleRadians);

			*verticesPtr++ = float3.zero;
			MeshGeneratorUtils.PopulateCircleVertices(verticesPtr, radius, 1f, 0f, resolution);

			*indicesPtr++ = 0;
			*indicesPtr++ = (ushort)resolution;
			*indicesPtr++ = 1;

			for (var i = 1; i < resolution; i++)
			{
				*indicesPtr++ = 0;
				*indicesPtr++ = (ushort)i;
				*indicesPtr++ = (ushort)(i + 1);
			}
		}

		[Unity.Burst.CompilerServices.SkipLocalsInit]
		[BurstCompile(DisableSafetyChecks = true, FloatMode = FloatMode.Fast)]
		private static unsafe void GenerateHollowConeMeshData(int resolution, float halfAngleRadians,
			float3* verticesPtr, int* indicesPtr)
		{
			var radius = math.sin(halfAngleRadians);

			*verticesPtr++ = float3.zero;
			MeshGeneratorUtils.PopulateCircleVertices(verticesPtr, radius, 1f, 0f, resolution);

			*indicesPtr++ = 0;
			*indicesPtr++ = 1;
			*indicesPtr++ = resolution;

			for (var i = 1; i < resolution; i++)
			{
				*indicesPtr++ = 0;
				*indicesPtr++ = i;
				*indicesPtr++ = i + 1;
			}
		}
	}
}
