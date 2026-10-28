using System.Runtime.CompilerServices;
using Saesentsessis.MeshGeneration.Shapes;
using UnityEngine;

namespace Saesentsessis.MeshGeneration
{
	public static class MeshGeneratorExtensions
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Mesh GenerateMesh(ref this HollowConeMeshDescriptor descriptor)
			=> MeshGenerator.CreateHollowConeMesh(in descriptor);
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Mesh GenerateMesh(ref this HollowHalfSphereMeshDescriptor descriptor)
			=> MeshGenerator.CreateHollowHalfSphereMesh(in descriptor);
	}
}
