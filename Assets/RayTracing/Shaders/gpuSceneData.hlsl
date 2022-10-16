#ifndef GPU_SCENE_DATA
#define GPU_SCENE_DATA

#include "geometry.hlsl"
#include "GPUStructs.hlsl"


StructuredBuffer<Vertex>   Vertices;    //the origin mesh vertices of all meshes.
//the origin mesh triangle indices of all meshes, we can consider all the meshes as a big mesh, and this indices is the triangle vertex index of the whole big mesh.
//we can consider TriangleIndices as the index in Vertices declare about.
StructuredBuffer<int3>      TriangleIndices;    

StructuredBuffer<MeshInstance> MeshInstances;
StructuredBuffer<Material> materials;
StructuredBuffer<Light> lights;
StructuredBuffer<float2> Distributions1D;
StructuredBuffer<DistributionDiscript> DistributionDiscripts;

cbuffer cb
{
	uniform int instBVHAddr;
	uniform int bvhNodesNum;
	uniform float worldRadius;
	uniform float cameraConeSpreadAngle;
	uniform matrix RasterToCamera;
	uniform matrix CameraToWorld;
	uniform matrix WorldToRaster;
};

void ComputeSurfaceIntersection(HitInfo hitInfo, float3 wo, out Interaction interaction)
{
	interaction = (Interaction)0;
	int3 triAddr = hitInfo.triAddr;
	float2 uv = hitInfo.baryCoord;
	MeshInstance meshInst = MeshInstances[hitInfo.meshInstanceId];
	int vertexIndex0 = triAddr.x;//WoopTriangleIndices[triAddr];
	int vertexIndex1 = triAddr.y;//WoopTriangleIndices[triAddr + 1];
	int vertexIndex2 = triAddr.z;//WoopTriangleIndices[triAddr + 2];
	Vertex vertex0 = Vertices[vertexIndex0];
	Vertex vertex1 = Vertices[vertexIndex1];
	Vertex vertex2 = Vertices[vertexIndex2];
	const float3 v0 = vertex0.position;
	const float3 v1 = vertex1.position;
	const float3 v2 = vertex2.position;
	float4 hitPos = float4(v0 * uv.x + v1 * uv.y + v2 * (1.0 - uv.x - uv.y), 1);
	float4x4 objectToWorld = meshInst.localToWorld;
	hitPos = mul(objectToWorld, hitPos);

	float3 p0 = mul(objectToWorld, float4(v0.xyz, 1.0)).xyz;
	float3 p1 = mul(objectToWorld, float4(v1.xyz, 1.0)).xyz;
	float3 p2 = mul(objectToWorld, float4(v2.xyz, 1.0)).xyz;
	float triAreaInWorld = length(cross(p0 - p1, p0 - p2)) * 0.5;

	float3 normal0 = vertex0.normal;
	float3 normal1 = vertex1.normal;
	float3 normal2 = vertex2.normal;

	float3 normal = normalize(normal0 * uv.x + normal1 * uv.y + normal2 * (1.0 - uv.x - uv.y));

	float3 worldNormal = normalize(mul(normal, (float3x3)meshInst.worldToLocal));

	const float2 uv0 = vertex0.uv;
	const float2 uv1 = vertex1.uv;
	const float2 uv2 = vertex2.uv;

	interaction.normal.xyz = worldNormal;
	interaction.p.xyz = hitPos.xyz;
	interaction.hitT = hitInfo.hitT;
	interaction.uv = uv0 * uv.x + uv1 * uv.y + uv2 * (1.0 - uv.x - uv.y);

	float3 dpdu = float3(1, 0, 0);
	float3 dpdv = float3(0, 1, 0);
	CoordinateSystem(worldNormal, dpdu, dpdv);
	interaction.tangent.xyz = normalize(dpdu.xyz);
	interaction.bitangent.xyz = normalize(cross(interaction.tangent.xyz, worldNormal));
	interaction.primArea = triAreaInWorld;
	//interaction.triangleIndex = (vertexIndex0 - meshInst.vertexOffsetStart) / 3;//hitInfo.triangleIndexInMesh;
	interaction.uvArea = length(cross(float3(uv2, 1) - float3(uv0, 1), float3(uv1, 1) - float3(uv0, 1)));

	float4 v0Screen = mul(WorldToRaster, float4(p0, 1));
	float4 v1Screen = mul(WorldToRaster, float4(p1, 1));
	float4 v2Screen = mul(WorldToRaster, float4(p2, 1));
	v0Screen /= v0Screen.w;
	v1Screen /= v1Screen.w;
	v2Screen /= v2Screen.w;
	interaction.screenSpaceArea = length(cross(v2Screen.xyz - v0Screen.xyz, v1Screen.xyz - v0Screen.xyz));
	interaction.wo.xyz = wo;
	interaction.materialID = meshInst.GetMaterialID();
	interaction.meshInstanceID = hitInfo.meshInstanceId;
}

#endif
