Shader "Custom/DrawFaces"
{
	Properties
	{
	}
	Category
	{
		SubShader
		{
			Tags { "RenderType" = "Opaque" }
			Pass
			{
				CGPROGRAM
				#pragma vertex VS
				#pragma geometry GS
				#pragma fragment PS
				#pragma enable_d3d11_debug_symbols
				#pragma target 5.0

				struct VoxelData
				{
					uint3 _Position;
					uint _FaceFlag;
				};

				StructuredBuffer<VoxelData> _Verts;
	
				//STRUCTS
				struct VS_OUTPUT
				{
					float4 position : SV_POSITION;
					float3 normal : NORMAL;
					uint idx : SV_InstanceID;
				};
	
				struct GS_INPUT
				{
					float4 position : SV_POSITION;
					float3 normal : NORMAL;
				};
	
				//VERTEX SHADER
				VS_OUTPUT VS(uint instanceId : SV_InstanceID)
				{
					VS_OUTPUT output;
					output.position = float4((float)_Verts[instanceId]._Position.x, (float)_Verts[instanceId]._Position.y, (float)_Verts[instanceId]._Position.z, 1.0f);
					output.normal = float3(0, 0, 0);
					output.idx = instanceId;
					return output;
				}
	
				void CreateVertex(inout TriangleStream<GS_INPUT> triStream, float3 pos, float3 normal)
				{
					//Step 1. Create a GS_DATA object
					GS_INPUT output;
					output.position = UnityObjectToClipPos(pos);
					output.normal = normal;
					triStream.Append(output);
				}
	
				// AND (&)
				//https://stackoverflow.com/a/48271395
				uint AND_BIT(uint a, uint b)
				{
					uint d = 0x80000000; //adapted to 32-bit
					uint result = 0;
					while (d > 0)
					{
						if (a >= d && b >= d) result += d;
						if (a >= d) a -= d;
						if (b >= d) b -= d;
						d /= 2;
					}
					return result;
				}

				void CreateCube(inout TriangleStream<GS_INPUT> triStream, float width, float3 pos, float3 normal, uint idx)
				{
					float halfVoxelSize = width / 2.0f;
					float3 triangleCenter = pos;

					uint FLAG = _Verts[idx]._FaceFlag;

					//Front Face
					normal = float3(0.0f, 0.0f, -1.0f);
					if (AND_BIT(FLAG, 1) == 1)
					{
						CreateVertex(triStream, float3(triangleCenter.x - halfVoxelSize, triangleCenter.y - halfVoxelSize, triangleCenter.z + halfVoxelSize), normal);
						CreateVertex(triStream, float3(triangleCenter.x + halfVoxelSize, triangleCenter.y - halfVoxelSize, triangleCenter.z + halfVoxelSize), normal);
						CreateVertex(triStream, float3(triangleCenter.x - halfVoxelSize, triangleCenter.y + halfVoxelSize, triangleCenter.z + halfVoxelSize), normal);
						CreateVertex(triStream, float3(triangleCenter.x + halfVoxelSize, triangleCenter.y + halfVoxelSize, triangleCenter.z + halfVoxelSize), normal);

						triStream.RestartStrip();
					}

					//back Face
					normal = float3(0.0f, 0.0f, 1.0f);
					if (AND_BIT(FLAG, 2) == 2)
					{
						CreateVertex(triStream, float3(triangleCenter.x + halfVoxelSize, triangleCenter.y - halfVoxelSize, triangleCenter.z - halfVoxelSize), normal);
						CreateVertex(triStream, float3(triangleCenter.x - halfVoxelSize, triangleCenter.y - halfVoxelSize, triangleCenter.z - halfVoxelSize), normal);
						CreateVertex(triStream, float3(triangleCenter.x + halfVoxelSize, triangleCenter.y + halfVoxelSize, triangleCenter.z - halfVoxelSize), normal);
						CreateVertex(triStream, float3(triangleCenter.x - halfVoxelSize, triangleCenter.y + halfVoxelSize, triangleCenter.z - halfVoxelSize), normal);

						triStream.RestartStrip();
					}

					//Left Face
					normal = float3(1.0f, 0.0f, 0.0f);
					if (AND_BIT(FLAG, 4) == 4)
					{
						CreateVertex(triStream, float3(triangleCenter.x - halfVoxelSize, triangleCenter.y - halfVoxelSize, triangleCenter.z - halfVoxelSize), normal);
						CreateVertex(triStream, float3(triangleCenter.x - halfVoxelSize, triangleCenter.y - halfVoxelSize, triangleCenter.z + halfVoxelSize), normal);
						CreateVertex(triStream, float3(triangleCenter.x - halfVoxelSize, triangleCenter.y + halfVoxelSize, triangleCenter.z - halfVoxelSize), normal);
						CreateVertex(triStream, float3(triangleCenter.x - halfVoxelSize, triangleCenter.y + halfVoxelSize, triangleCenter.z + halfVoxelSize), normal);

						triStream.RestartStrip();
					}

					//Right Face
					normal = float3(-1.0f, 0.0f, 0.0f);
					if (AND_BIT(FLAG, 8) == 8)
					{
						CreateVertex(triStream, float3(triangleCenter.x + halfVoxelSize, triangleCenter.y + halfVoxelSize, triangleCenter.z - halfVoxelSize), normal);
						CreateVertex(triStream, float3(triangleCenter.x + halfVoxelSize, triangleCenter.y + halfVoxelSize, triangleCenter.z + halfVoxelSize), normal);
						CreateVertex(triStream, float3(triangleCenter.x + halfVoxelSize, triangleCenter.y - halfVoxelSize, triangleCenter.z - halfVoxelSize), normal);
						CreateVertex(triStream, float3(triangleCenter.x + halfVoxelSize, triangleCenter.y - halfVoxelSize, triangleCenter.z + halfVoxelSize), normal);

						triStream.RestartStrip();
					}

					//Top Face
					normal = float3(0.0f, -1.0f, 0.0f);
					if (AND_BIT(FLAG, 16) == 16)
					{
						CreateVertex(triStream, float3(triangleCenter.x - halfVoxelSize, triangleCenter.y + halfVoxelSize, triangleCenter.z + halfVoxelSize), normal);
						CreateVertex(triStream, float3(triangleCenter.x + halfVoxelSize, triangleCenter.y + halfVoxelSize, triangleCenter.z + halfVoxelSize), normal);
						CreateVertex(triStream, float3(triangleCenter.x - halfVoxelSize, triangleCenter.y + halfVoxelSize, triangleCenter.z - halfVoxelSize), normal);
						CreateVertex(triStream, float3(triangleCenter.x + halfVoxelSize, triangleCenter.y + halfVoxelSize, triangleCenter.z - halfVoxelSize), normal);

						triStream.RestartStrip();
					}

					//Bottom Face
					normal = float3(0.0f, 1.0f, 0.0f);
					if (AND_BIT(FLAG, 32) == 32)
					{
						CreateVertex(triStream, float3(triangleCenter.x - halfVoxelSize, triangleCenter.y - halfVoxelSize, triangleCenter.z - halfVoxelSize), normal);
						CreateVertex(triStream, float3(triangleCenter.x + halfVoxelSize, triangleCenter.y - halfVoxelSize, triangleCenter.z - halfVoxelSize), normal);
						CreateVertex(triStream, float3(triangleCenter.x - halfVoxelSize, triangleCenter.y - halfVoxelSize, triangleCenter.z + halfVoxelSize), normal);
						CreateVertex(triStream, float3(triangleCenter.x + halfVoxelSize, triangleCenter.y - halfVoxelSize, triangleCenter.z + halfVoxelSize), normal);

						triStream.RestartStrip();
					}
				}

				//GEOMEtrY SHADER
				[maxvertexcount(24)]
				void GS(point VS_OUTPUT input[1], inout TriangleStream<GS_INPUT> triStream)
				{
					//ignore zero
					if (input[0].position.x == 0 && input[0].position.y == 0 && input[0].position.z == 0)
					{
						return;
					}

					CreateCube(triStream, 1.0f, input[0].position, input[0].normal, input[0].idx);
				}
	
				//PIXEL/FRAGMENT SHADER
				fixed4 PS(GS_INPUT input) : SV_Target
				{
					float4 color = float4(1.0f, 1.0f, 1.0f, 1.0f);

					//HalfLambert Diffuse :)
					float diffuseStrength = dot(input.normal, -normalize(_WorldSpaceLightPos0.xyz));
					diffuseStrength = diffuseStrength * 0.5 + 0.5;
					diffuseStrength = saturate(diffuseStrength);
					color = color * diffuseStrength;

					return color;
					//return fixed4(1.0f, 0.0f, 0.0f, 1.0f);
				}

				ENDCG
			}
		}
	}
    FallBack "Diffuse"
}