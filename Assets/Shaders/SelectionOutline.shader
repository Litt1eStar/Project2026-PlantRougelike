Shader "PlantRoguelike/SelectionOutline"
{
    Properties
    {
        _OutlineColor("Outline Color", Color) = (1, 0.85, 0.2, 1)
        _OutlineWidth("Outline Width", Range(0, 0.5)) = 0.03
    }
    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry+1"
        }

        Pass
        {
            Name "Outline"
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float  _OutlineWidth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings o;
                float3 n = normalize(input.normalOS);
                float3 displaced = input.positionOS.xyz + n * _OutlineWidth;
                o.positionCS = TransformObjectToHClip(displaced);
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
