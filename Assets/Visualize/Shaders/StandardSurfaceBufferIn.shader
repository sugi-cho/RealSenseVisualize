Shader "Custom/StandardSurfaceBufferIn" {
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_Metallic ("Metallic", Range(0,1)) = 0.0
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200

		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard addshadow vertex:vert

		// Use shader model 3.0 target, to get nicer looking lighting
		#pragma target 3.0

		sampler2D _MainTex;

		struct Input {
			float2 uv_MainTex;
			float isValid;
		};

#ifdef SHADER_API_D3D11
		uniform StructuredBuffer<float3> _Vertex;
#endif
		half _Glossiness;
		half _Metallic;
		fixed4 _Color;

		// Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
		// See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
		// #pragma instancing_options assumeuniformscaling
		UNITY_INSTANCING_BUFFER_START(Props)
			// put more per-instance properties here
		UNITY_INSTANCING_BUFFER_END(Props)

		struct appdata {
			float4 vertex : POSITION;
			float4 tangent : TANGENT;
			float3 normal : NORMAL;
			float4 texcoord : TEXCOORD0;
			float4 texcoord1 : TEXCOORD1;
			float4 texcoord2 : TEXCOORD2;
			float4 texcoord3 : TEXCOORD3;
			fixed4 color : COLOR;
			UNITY_VERTEX_INPUT_INSTANCE_ID
			uint vId : SV_VertexID;
		};

		void vert(inout appdata v, out Input o) {
#ifdef SHADER_API_D3D11
			float3 pos = _Vertex[v.vId];
			v.vertex.xyz = pos;
			v.vertex.y *= -1;

			float3 dx = normalize(_Vertex[v.vId + 3] - _Vertex[v.vId - 3]);
			float3 dy = normalize(_Vertex[v.vId - 640 * 3] - _Vertex[v.vId + 640 * 3]);
			//Depthのアーティファクトを均すため、大雑把な法線の計算
			v.normal = cross(dx, dy);

			o = (Input)0;
			o.isValid = 0 < length(pos);
#endif
		}

		void surf (Input IN, inout SurfaceOutputStandard o) {
			if (IN.isValid < 0.99)
				discard;

			// Albedo comes from a texture tinted by color
			fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
			o.Albedo = c.rgb;
			// Metallic and smoothness come from slider variables
			o.Metallic = _Metallic;
			o.Smoothness = _Glossiness;
			o.Alpha = c.a;
		}
		ENDCG
	}
	FallBack "Diffuse"
}
