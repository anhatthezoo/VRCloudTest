Shader "Unlit/CubemapReflection_CG"
{
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"}
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldRefl : TEXCOORD0; 
            };

            samplerCUBE _CurrentSkybox;
            samplerCUBE _HistorySkybox;
            float _BlendFactor;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);

                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 worldViewDir = normalize(UnityWorldSpaceViewDir(worldPos));
                float3 worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldRefl = reflect(-worldViewDir, worldNormal);

                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                const float3 flipZ = float3(1.0, 1.0, -1.0);
                float4 currentColor = texCUBE(_CurrentSkybox, i.worldRefl * flipZ);
                float4 historyColor = texCUBE(_HistorySkybox, i.worldRefl * flipZ);
                float4 col = lerp(historyColor, currentColor, _BlendFactor);

                return col;
            }
            ENDHLSL
        }
    }
}