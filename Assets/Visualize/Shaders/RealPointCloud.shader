Shader "Unlit/RealPointCloud"
{
	Properties
	{
		[NoScaleOffset]_MainTex("Texture", 2D) = "white" {}
		_EdgeThreshold("threshold", Float) = 0.1
		_GSize("grid size", Float) = 5
		_Color("color", Color) = (0.5,0.5,0.5,0.5)
		[HDR]_Spec("speclur color", Color) = (1,1,1,1)
		[HDR]_Line("line color", Color) = (1,1,1,1)
		_Smooth("smoothness", Float) = 0.0
		[HDR]_Col0("emission color0", Color) = (1,0,0,0)
		[HDR]_Col1("emission color1", Color) = (1,1,0,0)
	}
		SubShader
		{
			Tags { "RenderType" = "Opaque" }
			LOD 100

			Pass
			{
				Tags{ "LightMode" = "Deferred" }
				CGPROGRAM
				#pragma vertex vert
				#pragma geometry geom
				#pragma fragment frag

				#include "RealSense.hlsl"
				ENDCG
			}
			Pass
			{
				Tags{ "LightMode" = "ForwardBase" }
				CGPROGRAM
				#pragma vertex vert
				#pragma geometry geom
				#pragma fragment frag_forward

				#include "RealSense.hlsl"
				ENDCG
			}
			Pass
			{
				Tags{ "LightMode" = "ShadowCaster" }
				CGPROGRAM
				#pragma vertex vert
				#pragma geometry geom
				#pragma fragment shadow_cast

				#include "RealSense.hlsl"
				ENDCG
			}
		}
}