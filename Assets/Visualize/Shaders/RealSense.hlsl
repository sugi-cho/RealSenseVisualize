#include "UnityCG.cginc"
#include "UnityGBuffer.cginc"
#include "Quaternion.cginc"
#include "Random.cginc"
#include "SimplexNoise3D.cginc"

struct voxelParticle
{
    float3 vert;
    float3 pos;
    float3 vel;
    float3 dir;
    float4 prop;
    float t;
    float size;
};

struct appdata
{
	float4 vertex : POSITION;
	float3 normal : NORMAL;
	float2 uv : TEXCOORD0;
	uint vIdx : SV_VertexID;
};

struct v2f
{
	float4 vertex : SV_POSITION;
	float2 uv : TEXCOORD0;
	float3 wPos : TEXCOORD1;
    float3 prePos : TEXCOORD2;
	uint vIdx : TEXCOORD3;
	float3 bary : TEXCOORD4;
	float3 normal : NORMAL;
};

RWStructuredBuffer<voxelParticle> _ParticleBuffer;
StructuredBuffer<voxelParticle> _VoxelBuffer;
StructuredBuffer<int> _IndicesBuffer;
StructuredBuffer<float3> _VertBuffer;
StructuredBuffer<float3> _PrevBuffer;
sampler2D _MainTex;
float4 _MainTex_ST;

half4 _Color, _Spec, _Line, _Col0, _Col1;
float _EdgeThreshold, _GSize, _Smooth;
			
v2f vert(appdata v)
{
    v2f o = (v2f) 0;
    o.vIdx = v.vIdx;

    return o;
}

float edgeLength(float3 v0, float3 v1, float3 v2) {
	float l = distance(v0, v1);
	l = max(l, distance(v1, v2));
	l = max(l, distance(v2, v0));
	return l;
}

float3 dfNoise(float3 pos)
{
    float3 grad = snoise_grad(pos);
    float3 noise = snoise(pos);
    float3 divFree = cross(grad, noise);
    return divFree;
}

void cube(float3 center,float3 dir, float size, v2f o, inout TriangleStream<v2f> triStream)
{
    float3 normals[6] = {
        float3(-1, 0, 0), float3( 1, 0, 0),
        float3( 0,-1, 0), float3( 0, 1, 0),
        float3( 0, 0,-1), float3( 0, 0, 1),
    };
    float3 rights[6] =
    {
        float3( 0, 0,-1), float3( 0, 0, 1),
        float3(-1, 0, 0), float3( 1, 0, 0),
        float3( 0,-1, 0), float3( 0, 1, 0),
    };

    float4 q = fromToRotation(normals[0], dir);

    for (int i = 0; i < 6; i++)
    {
        float3 normal = rotateWithQuaternion(normals[i], q);
        float3 right = rotateWithQuaternion(rights[i], q);
        float3 up = cross(normal, right);
        float3 p =  size * normal;

        float3 pos = p + size * (-right - up);
        o.wPos = mul(unity_ObjectToWorld, float4(pos + center, 1)).xyz;
        o.normal = UnityObjectToWorldNormal(normal);
        o.vertex = UnityWorldToClipPos(o.wPos);
        triStream.Append(o);

        pos = p + size * (right - up);
        o.wPos = mul(unity_ObjectToWorld, float4(pos + center, 1)).xyz;
        o.normal = UnityObjectToWorldNormal(normal);
        o.vertex = UnityWorldToClipPos(o.wPos);
        triStream.Append(o);

        pos = p + size * (-right + up);
        o.wPos = mul(unity_ObjectToWorld, float4(pos + center, 1)).xyz;
        o.normal = UnityObjectToWorldNormal(normal);
        o.vertex = UnityWorldToClipPos(o.wPos);
        triStream.Append(o);

        pos = p + size * (right + up);
        o.wPos = mul(unity_ObjectToWorld, float4(pos + center, 1)).xyz;
        o.normal = UnityObjectToWorldNormal(normal);
        o.vertex = UnityWorldToClipPos(o.wPos);
        triStream.Append(o);
        triStream.RestartStrip();
    }

}

[maxvertexcount(24)]
void geom(point v2f input[1], inout TriangleStream<v2f> triStream)
{
    uint idx = input[0].vIdx;
    voxelParticle p = _VoxelBuffer[idx];

    float3 center = p.pos;
    float3 toDir = normalize(center - _WorldSpaceCameraPos);
    float3 axis = cross(p.dir, toDir) + float3(0, 0.543, 0);
    float size = p.size * 0.2;
    size *= p.prop.y ? 1 : saturate(p.t * 0.1);
    size *= saturate(1.0 - p.prop.y * pow(p.t * 0.2, 2.0));
    size *= saturate(1.0 - p.prop.z * p.t * .5);

    cube(center, p.dir, size, input[0], triStream);
}
		
void frag (
    v2f i,
    out half4 outGBuffer0 : SV_Target0,
    out half4 outGBuffer1 : SV_Target1,
    out half4 outGBuffer2 : SV_Target2,
    out half4 outEmission : SV_Target3)
{
    voxelParticle p = _VoxelBuffer[i.vIdx];
    
    half3 view = UnityWorldSpaceViewDir(i.wPos);
    half dst = length(view);

    float gsize = lerp(_GSize, _GSize * 0.1,dst * 0.1);
	half3 d = fwidth(frac(i.wPos*gsize));
    half3 a3 = smoothstep(half3(0, 0, 0), d * 0.5, frac(i.wPos * gsize));
	half w = 1.0 - min(min(a3.x, a3.y), a3.z);
    
    UnityStandardData data;

    float diff = length(i.prePos - i.wPos);

    data.diffuseColor = _Color;
    data.occlusion = 1;
    data.specularColor = _Spec;
    data.smoothness = _Smooth;
    data.normalWorld = i.normal;

    UnityStandardDataToGbuffer(data, outGBuffer0, outGBuffer1, outGBuffer2);

    half dstFade = saturate(1 - (dst - 1) * 0.5);
    float t = saturate(p.t);
    outEmission = lerp(w * dstFade * _Line, lerp(_Col0, _Col1, t * t), p.prop.z);
}

float4x4 _World2Handcam;
float _FocusDst;

half4 frag_forward(v2f i) : SV_Target
{
    voxelParticle p = _VoxelBuffer[i.vIdx];
    float t = saturate(p.t);

    half3 lDir = normalize(_WorldSpaceLightPos0.xyz - i.wPos);
    half lit = max(0, dot(lDir, i.normal));
    half4 c = _Color * lit;
    half4 e = lerp(_Col0, _Col1, t * t) * p.prop.z;
    
    half3 vPos = mul(_World2Handcam, half4(i.wPos, 1)).xyz;
    half d = abs(_FocusDst - abs(vPos.z));
    half4 focus = lerp(1.0, half4(1, 0.25, 0.25, 1), d);

    return (c + e) * focus;
}

half4 shadow_cast(v2f i):SV_Target
{
    return 0;
}
