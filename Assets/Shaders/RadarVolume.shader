Shader "HK132/RadarVolume"
{
    Properties
    {
        _Color ("Color", Color) = (0.3, 0.85, 1, 0.18)
        _EdgeColor ("Edge Color", Color) = (0.6, 1, 1, 0.95)
        _EdgeStart ("Edge Start", Range(0, 1)) = 0.28
        _EdgeEnd ("Edge End", Range(0, 1)) = 0.02
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Back
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            fixed4 _EdgeColor;
            float _EdgeStart;
            float _EdgeEnd;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
                float4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = _WorldSpaceCameraPos - worldPos;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.worldNormal);
                float3 v = normalize(i.viewDir);
                float ndotv = saturate(dot(n, v));
                float edge = smoothstep(_EdgeStart, _EdgeEnd, ndotv);
                float3 rgb = lerp(_Color.rgb, _EdgeColor.rgb, edge);
                // 正面按壳体衰减，避免球体大面积朝向相机时比锥体实一截。
                float shell = lerp(1.0, 0.28, ndotv);
                float alpha = lerp(_Color.a * shell, _EdgeColor.a, edge);
                return fixed4(rgb, saturate(alpha));
            }
            ENDCG
        }
    }
}
