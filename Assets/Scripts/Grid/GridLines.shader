Shader "PlantRoguelike/GridLines"
{
    Properties
    {
        _LineColor     ("Line Color",     Color) = (1,1,1,0.4)
        _LineThickness ("Line Thickness", Range(0.001, 0.2)) = 0.04
        _GridDim       ("Grid Dim (w,h)", Vector) = (32, 32, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _LineColor;
                float  _LineThickness;
                float4 _GridDim;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // Convert UV (0..1) to cell-space coordinates.
                float2 cellCoord = IN.uv * _GridDim.xy;
                float2 f = frac(cellCoord);

                // Distance to nearest cell border (0 or 1).
                float2 dist = min(f, 1.0 - f);

                // Anti-aliased line via screen-space derivative.
                float2 aa = fwidth(cellCoord);
                float2 line2D = 1.0 - smoothstep(_LineThickness * 0.5 - aa,
                                                 _LineThickness * 0.5 + aa,
                                                 dist);

                float lineMask = max(line2D.x, line2D.y);

                half4 col = _LineColor;
                col.a *= lineMask;
                return col;
            }
            ENDHLSL
        }
    }
}
