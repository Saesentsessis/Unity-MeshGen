using System;
using System.Runtime.CompilerServices;
using Saesentsessis.MeshGeneration.Shapes;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Saesentsessis.MeshGeneration
{
	public static partial class MeshGenerator
	{
		/// <summary>
		/// Creates a hollow half-sphere shape descriptor, that could be passed into
		/// <see cref="CreateHollowHalfSphereMesh"/> function.
		/// </summary>
		/// <param name="bandVertexCounts">
		/// Per-band vertex counts of the half sphere. Each band should have more than 3
		/// vertices and the sum of all bands should be less or equals than int.MaxValue.
		/// This property is passed directly to the resulting structure, thus not being
		/// copied. If you want opposite behavior - use <see cref="CreateHollowHalfSphereMeshDescriptorWithCopy"/>.
		/// </param>
		/// <returns>
		/// A disposable hollow half-sphere descriptor, owned by the caller.
		/// Don't forget to call Dispose() or wrap that structure inside a using block.
		/// </returns>
		public static HollowHalfSphereMeshDescriptor CreateHollowHalfSphereMeshDescriptor(
			in NativeArray<int> bandVertexCounts)
		{
			long vertexCount = bandVertexCounts[0] + 1;
			long triangleCount = bandVertexCounts[^1];

			for (var bandIndex = bandVertexCounts.Length - 1; bandIndex > 0; bandIndex--)
			{
				var bandVertexCount = bandVertexCounts[bandIndex];

				if (bandVertexCount < 3)
					throw new ArgumentOutOfRangeException(nameof(bandVertexCounts), bandVertexCount,
						$"Vertex count for band {bandIndex} should be more or equal to 3.");
				
				vertexCount += bandVertexCount;
				triangleCount += bandVertexCount + bandVertexCounts[bandIndex - 1];
			}
			
			if (vertexCount > int.MaxValue)
				throw new OverflowException("Unable to create HollowHalfSphereMeshDescriptor: vertex count must be less or equal to 2147483647.");
			
			if (triangleCount > uint.MaxValue / 3)
				throw new OverflowException("Unable to create HollowHalfSphereMeshDescriptor: index count must be less or equal to 4294967295.");

			return new HollowHalfSphereMeshDescriptor((int)vertexCount, (int)triangleCount * 3, bandVertexCounts);
		}
		
		/// <summary>
		/// Same as <see cref="CreateHollowHalfSphereMeshDescriptor(in Unity.Collections.NativeArray{int})"/>,
		/// but does not perform any safety checks. Use it only if you are certain in the data you are providing.
		/// </summary>
		[BurstCompile(DisableSafetyChecks = true)]
		public static void CreateHollowHalfSphereMeshDescriptor(in NativeArray<int> bandVertexCounts,
			out HollowHalfSphereMeshDescriptor descriptor)
		{
			long vertexCount = bandVertexCounts[0] + 1;
			long triangleCount = bandVertexCounts[^1];

			for (var bandIndex = bandVertexCounts.Length - 1; bandIndex > 0; bandIndex--)
			{
				var bandVertexCount = bandVertexCounts[bandIndex];
				
				vertexCount += bandVertexCount;
				triangleCount += bandVertexCount + bandVertexCounts[bandIndex - 1];
			}

			descriptor = new HollowHalfSphereMeshDescriptor((int)vertexCount, (int)triangleCount * 3, bandVertexCounts);
		}

		/// <summary>
		/// Creates a hollow half-sphere shape descriptor, that could be passed into
		/// <see cref="CreateHollowHalfSphereMesh"/> function.
		/// </summary>
		/// <param name="bandVertexCounts">
		/// Per-band vertex counts of the half sphere. Each band should have more than 3
		/// vertices and the sum of all bands should be less or equals than int.MaxValue.
		/// </param>
		/// <param name="allocator">An allocator for an internal buffers.</param>
		/// <returns>
		/// A disposable hollow half-sphere descriptor, owned by the caller.
		/// Don't forget to call Dispose() or wrap that structure inside a using block.
		/// </returns>
		public static HollowHalfSphereMeshDescriptor CreateHollowHalfSphereMeshDescriptorWithCopy(
			in NativeArray<int> bandVertexCounts, Allocator allocator)
		{
			var localVertexCounts = new NativeArray<int>(bandVertexCounts, allocator);

			return CreateHollowHalfSphereMeshDescriptor(in localVertexCounts);
		}
		
		/// <summary>
		/// Creates a hollow half-sphere shape descriptor, that could be passed into
		/// <see cref="CreateHollowHalfSphereMesh"/> function.
		/// </summary>
		/// <remarks>
		/// Resolution of 32771 and higher overflows 16-bit indexing, doubling the effective
		/// memory payload required to store indices.
		/// </remarks>
		/// <param name="baseResolution">Resolution of the largest band of the half-sphere.</param>
		/// <param name="allocator">An allocator for an internal buffers.</param>
		/// <returns>
		/// A disposable hollow half-sphere descriptor, owned by the caller.
		/// Don't forget to call Dispose() or wrap that structure inside a using block.
		/// </returns>
		public static HollowHalfSphereMeshDescriptor CreateHollowHalfSphereMeshDescriptor(int baseResolution, Allocator allocator)
		{
			if (baseResolution < 4)
			{
				var bandsArray = new NativeArray<int>(1, allocator, NativeArrayOptions.UninitializedMemory);
				bandsArray[0] = 3;

				return new HollowHalfSphereMeshDescriptor(4, 9, bandsArray);
			}

			// Special case if baseResolution is the power of 2
			if (math.ispow2(baseResolution))
				return CreateHollowHalfSphereMeshDescriptorPowerOfTwo(baseResolution, allocator);

			var totalVertexCount = baseResolution + 1;
			var bandVertexCount = baseResolution / 2;

			var approxBandCount = (int)math.max(math.sqrt(baseResolution), 1);
			var bands = new NativeList<int>(approxBandCount, Allocator.Temp);
			bands.Add(baseResolution);

			try
			{
				// Compute all bands first
				while (bandVertexCount > 2)
				{
					bands.Add(bandVertexCount);
					totalVertexCount += bandVertexCount;

					bandVertexCount /= 2;
				}

				var totalTriangleCount = 0;

				// Then figure out how many indices would they require
				for (var bandIndex = bands.Length - 1; bandIndex > 0; bandIndex--)
					totalTriangleCount += bands[bandIndex] + bands[bandIndex - 1];

				var lastBandVertexCount = bands[^1];

				// Add transition triangles from last band to the single point at the apex of the half-sphere
				totalTriangleCount += lastBandVertexCount;

				return new HollowHalfSphereMeshDescriptor(totalVertexCount, totalTriangleCount * 3,
					bands.ToArray(allocator));
			}
			finally
			{
				bands.Dispose();
			}
		}

		private static HollowHalfSphereMeshDescriptor CreateHollowHalfSphereMeshDescriptorPowerOfTwo(int baseResolution, Allocator allocator)
		{
			// We can analytically compute the amount of bands needed for this half-sphere by
			// counting trailing zeroes minus the bit at index 1, which would produce value 2,
			// that is less than 3
			var bandCount = math.tzcnt(baseResolution) - 1;

			// Pre-allocate final array for per-band vertex counts (resolution on a per-band basis)
			var bandsArray = new NativeArray<int>(bandCount, allocator, NativeArrayOptions.UninitializedMemory);

			var vertexCount = 1;

			for (var bandIndex = 0; bandIndex < bandCount; bandIndex++)
			{
				var bandVertexCount = baseResolution >> bandIndex;
				
				bandsArray[bandIndex] = bandVertexCount;
				vertexCount += bandVertexCount;
			}

			// We can analytically compute the amount of triangles required to triangulate such
			// half-sphere. Let's say, that N - baseResolution, that is power of 2, B - bandCount.
			// Then, total amount of triangles required to triangulate this shape is:
			//
			// 3 * (N - N_last), where N_last = N / (2^B-1).
			//
			// Last step is to multiply the result by 3, netting us a final formula for a total
			// index count:
			//
			// 9 * (N - N_last)

			var lastBandResolution = baseResolution >> (bandCount - 1);
			var indexCount = 9 * (baseResolution - lastBandResolution) + 3 * lastBandResolution;

			return new HollowHalfSphereMeshDescriptor(vertexCount, indexCount, bandsArray);
		}

		/// <summary>
		/// Generates hollow half-sphere mesh data from <see cref="HollowHalfSphereMeshDescriptor"/>.
		/// There are zero safety checks inside this method, so the caller should ensure, that the provided
		/// native arrays are correctly sized and that the descriptor contains valid data.
		/// </summary>
		/// <param name="descriptor">Hollow half-sphere descriptor.</param>
		/// <param name="vertices">Resulting vertex buffer, owned by the caller.</param>
		/// <param name="indices">Resulting index buffer, owned by the caller.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe void GenerateHollowHalfSphereMeshData(in HollowHalfSphereMeshDescriptor descriptor,
			NativeArray<float3> vertices, NativeArray<ushort> indices)
		{
			var verticesPtr = (float3*)vertices.GetUnsafePtr();
			var indicesPtr = (ushort*)indices.GetUnsafePtr();
			
			GenerateHollowHalfSphereMeshData(descriptor.BandVertexCounts, descriptor.VertexCount, verticesPtr, indicesPtr);
		}
		
		/// <summary>
		/// Generates hollow half-sphere mesh data from <see cref="HollowHalfSphereMeshDescriptor"/>.
		/// There are zero safety checks inside this method, so the caller should ensure, that the provided
		/// native arrays are correctly sized and that the descriptor contains valid data.
		/// </summary>
		/// <param name="descriptor">Hollow half-sphere descriptor.</param>
		/// <param name="vertices">Resulting vertex buffer, owned by the caller.</param>
		/// <param name="indices">Resulting index buffer, owned by the caller.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe void GenerateHollowHalfSphereMeshData(in HollowHalfSphereMeshDescriptor descriptor,
			NativeArray<float3> vertices, NativeArray<int> indices)
		{
			var verticesPtr = (float3*)vertices.GetUnsafePtr();
			var indicesPtr = (int*)indices.GetUnsafePtr();
			
			GenerateHollowHalfSphereMeshData(descriptor.BandVertexCounts, descriptor.VertexCount, verticesPtr, indicesPtr);
		}

		/// <summary>
		/// Uses AdvancedMeshAPI to populate the mesh data.
		/// </summary>
		/// <param name="descriptor">Hollow half-sphere descriptor.</param>
		/// <returns>New hollow half-sphere mesh instance, owned by the caller.</returns>
		public static Mesh CreateHollowHalfSphereMesh(in HollowHalfSphereMeshDescriptor descriptor)
		{
			const MeshUpdateFlags flags = MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds |
			                              MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontValidateLodRanges;
			
			if ((uint)descriptor.VertexCount > int.MaxValue)
				throw new ArgumentOutOfRangeException(nameof(descriptor.VertexCount), descriptor.VertexCount, "Vertex count should be in range [4 : 2147483647].");
			
			var meshDataArray = Mesh.AllocateWritableMeshData(1);
			var use32BitIndexing = descriptor.VertexCount > ushort.MaxValue + 1;

			try
			{
				var meshData = meshDataArray[0];

				meshData.SetVertexBufferParams(descriptor.VertexCount,
					new VertexAttributeDescriptor(VertexAttribute.Position));
				meshData.SetIndexBufferParams(descriptor.IndexCount, use32BitIndexing ? IndexFormat.UInt32 : IndexFormat.UInt16);
				meshData.subMeshCount = 1;
				var subMesh = new SubMeshDescriptor(0, descriptor.IndexCount)
				{
					bounds = new Bounds(new Vector3(0f, 0f, 0.5f), new Vector3(2f, 2f, 1f))
				};
				meshData.SetSubMesh(0, subMesh, flags);

				var vertices = meshData.GetVertexData<float3>();

				if (use32BitIndexing)
				{
					var indices = meshData.GetIndexData<int>();

					GenerateHollowHalfSphereMeshData(descriptor, vertices, indices);
				}
				else
				{
					var indices = meshData.GetIndexData<ushort>();

					GenerateHollowHalfSphereMeshData(descriptor, vertices, indices);					
				}

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
		private static unsafe void GenerateHollowHalfSphereMeshData(in NativeArray<int>.ReadOnly vertexCountPerBand,
			int vertexCount, float3* verticesPtr, ushort* indicesPtr)
		{
			var bandCount = vertexCountPerBand.Length;
			var bandVertexIndexOffsetsPtr = stackalloc ushort[bandCount];

			// Compute per-band vertex offsets
			var accumulator = 0;
			for (var bandIndex = 0; bandIndex < bandCount; bandIndex++)
			{
				bandVertexIndexOffsetsPtr[bandIndex] = (ushort)accumulator;
				accumulator += vertexCountPerBand[bandIndex];
			}

			var bandStep = math.PIHALF / bandCount;
			var vertexAngleOffset = 0f;

			for (var bandIndex = 0; bandIndex < bandCount; bandIndex++)
			{
				var bandVertexCount = vertexCountPerBand[bandIndex];
				var bandAngle = math.PIHALF - bandStep * bandIndex;
				var vertexStep = math.PI2 / bandVertexCount;
				
				math.sincos((float4)bandAngle, out var radius, out var z);
				
				verticesPtr = MeshGeneratorUtils.PopulateCircleVertices(verticesPtr, radius.x, z.x, vertexAngleOffset, bandVertexCount, vertexStep);

				// Shift first vertex by half the angle of the vertex step, so the next band vertices would
				// less likely match current band vertices angle. This greatly increases final mesh quality
				// in most cases and especially effective if base resolution is the power of two, or dividable
				// by two multiple times without a fraction.
				vertexAngleOffset = math.mad(vertexStep, 0.5f, vertexAngleOffset);
			}

			*verticesPtr = new float3(0, 0, 1);
			var apexVertexIndex = vertexCount - 1;

			// Bridge circle loops with triangles
			for (var bandIndex = bandCount - 1; bandIndex > 0; bandIndex--)
			{
				var bandAStart = bandVertexIndexOffsetsPtr[bandIndex - 1];
				var bandBStart = bandVertexIndexOffsetsPtr[bandIndex];

				var bandACount = vertexCountPerBand[bandIndex - 1];
				var bandBCount = vertexCountPerBand[bandIndex];

				indicesPtr = MeshGeneratorUtils.BridgeCircleLoops(indicesPtr, bandAStart, bandACount, bandBStart, bandBCount);
			}

			var topBandIndex = bandCount - 1;
			var topBandStart = bandVertexIndexOffsetsPtr[topBandIndex];
			var topBandCount = vertexCountPerBand[topBandIndex];
			var prevTopVertex = (ushort)(topBandStart + topBandCount - 1);

			// Fill the triangle fan on the apex
			for (var vertexIndex = 0; vertexIndex < topBandCount; vertexIndex++)
			{
				var curTopVertex = (ushort)(topBandStart + vertexIndex);

				*indicesPtr = (ushort)apexVertexIndex;
				*(indicesPtr + 1) = curTopVertex;
				*(indicesPtr + 2) = prevTopVertex;

				indicesPtr += 3;
				prevTopVertex = curTopVertex;
			}
		}

		[Unity.Burst.CompilerServices.SkipLocalsInit]
		[BurstCompile(DisableSafetyChecks = true, FloatMode = FloatMode.Fast)]
		private static unsafe void GenerateHollowHalfSphereMeshData(in NativeArray<int>.ReadOnly vertexCountPerBand,
			int vertexCount, float3* verticesPtr, int* indicesPtr)
		{
			var bandCount = vertexCountPerBand.Length;
			var bandVertexIndexOffsetsPtr = stackalloc int[bandCount];

			// Compute per-band vertex offsets
			var accumulator = 0;
			for (var bandIndex = 0; bandIndex < bandCount; bandIndex++)
			{
				bandVertexIndexOffsetsPtr[bandIndex] = accumulator;
				accumulator += vertexCountPerBand[bandIndex];
			}

			var bandStep = math.PIHALF / bandCount;
			var vertexAngleOffset = 0f;

			for (var bandIndex = 0; bandIndex < bandCount; bandIndex++)
			{
				var bandVertexCount = vertexCountPerBand[bandIndex];
				var bandAngle = math.PIHALF - bandStep * bandIndex;
				var vertexStep = math.PI2 / bandVertexCount;
				
				math.sincos((float4)bandAngle, out var radius, out var z);
				
				verticesPtr = MeshGeneratorUtils.PopulateCircleVertices(verticesPtr, radius.x, z.x, vertexAngleOffset, bandVertexCount, vertexStep);

				// Shift first vertex by half the angle of the vertex step, so the next band vertices would
				// less likely match current band vertices angle. This greatly increases final mesh quality
				// in most cases and especially effective if base resolution is the power of two, or dividable
				// by two multiple times without a fraction.
				vertexAngleOffset = math.mad(vertexStep, 0.5f, vertexAngleOffset);
			}

			*verticesPtr = new float3(0, 0, 1);
			var apexVertexIndex = vertexCount - 1;

			// Bridge circle loops with triangles
			for (var bandIndex = bandCount - 1; bandIndex > 0; bandIndex--)
			{
				var bandAStart = bandVertexIndexOffsetsPtr[bandIndex - 1];
				var bandBStart = bandVertexIndexOffsetsPtr[bandIndex];

				var bandACount = vertexCountPerBand[bandIndex - 1];
				var bandBCount = vertexCountPerBand[bandIndex];

				indicesPtr = MeshGeneratorUtils.BridgeCircleLoops(indicesPtr, bandAStart, bandACount, bandBStart, bandBCount);
			}
			
			var topBandIndex = bandCount - 1;
			var topBandStart = bandVertexIndexOffsetsPtr[topBandIndex];
			var topBandCount = vertexCountPerBand[topBandIndex];
			var prevTopVertex = topBandStart + topBandCount - 1;

			// Fill the triangle fan on the apex
			for (var vertexIndex = 0; vertexIndex < topBandCount; vertexIndex++)
			{
				var curTopVertex = topBandStart + vertexIndex;

				*indicesPtr = apexVertexIndex;
				*(indicesPtr + 1) = curTopVertex;
				*(indicesPtr + 2) = prevTopVertex;

				indicesPtr += 3;
				prevTopVertex = curTopVertex;
			}
		}
	}
}