Shader "StillHere/EyelidBlink"
{
    Properties
    {
        _EyelidColor ("Eyelid Color", Color) = (0.08, 0.02, 0.01, 1)
        _SkinColor ("Skin Tint", Color) = (0.15, 0.08, 0.06, 1)
        _BlinkAmount ("Blink Amount", Range(0, 1)) = 0
        _EyeWidth ("Eye Width", Range(0.1, 2)) = 0.7
        _EyeHeight ("Eye Height", Range(0.1, 1)) = 0.4
        _EdgeSoftness ("Edge Softness", Range(0.01, 0.3)) = 0.08
        _LashThickness ("Lash Line Thickness", Range(0, 0.05)) = 0.015
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Overlay+100"
            "RenderPipeline" = "UniversalPipeline"
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off
        
        Pass
        {
            Name "EyelidBlink"
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            
            CBUFFER_START(UnityPerMaterial)
                float4 _EyelidColor;
                float4 _SkinColor;
                float _BlinkAmount;
                float _EyeWidth;
                float _EyeHeight;
                float _EdgeSoftness;
                float _LashThickness;
            CBUFFER_END
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            
            float4 frag(Varyings input) : SV_Target
            {
                // When not blinking at all, return fully transparent
                if (_BlinkAmount < 0.001)
                {
                    return float4(0, 0, 0, 0);
                }
                
                float2 uv = input.uv;
                
                // Center the UV coordinates
                float2 centered = uv - 0.5;
                
                // Aspect ratio correction
                float aspectRatio = _ScreenParams.x / _ScreenParams.y;
                centered.x *= aspectRatio;
                
                // The eyelids come from top and bottom
                // Calculate vertical distance from center
                float absY = abs(centered.y);
                
                // Eyelid position: starts off-screen, moves toward center as blink increases
                // At blinkAmount=0, eyelids are far away (off screen)
                // At blinkAmount=1, eyelids meet at center (y=0)
                float maxEyelidDistance = _EyeHeight + 0.3; // Start position (off screen)
                float eyelidEdge = lerp(maxEyelidDistance, 0.0, _BlinkAmount);
                
                // Create almond/oval shape - eyelids are more curved in center, pointed at edges
                float xInfluence = abs(centered.x) / _EyeWidth;
                float curveShape = 1.0 - saturate(xInfluence * xInfluence); // Parabolic curve
                
                // The eyelid edge curves - higher in center, lower at sides
                float curvedEyelidEdge = eyelidEdge * (0.3 + 0.7 * curveShape);
                
                // How far is this pixel from the eyelid edge?
                // Positive = covered by eyelid, Negative = open (visible eye area)
                float distFromEdge = absY - curvedEyelidEdge;
                
                // Soft transition at eyelid edge
                float eyelidCoverage = smoothstep(-_EdgeSoftness, _EdgeSoftness * 0.5, distFromEdge);
                
                // Beyond the horizontal extent of the eye, it's all eyelid
                float horizontalCoverage = smoothstep(_EyeWidth * 0.8, _EyeWidth, abs(centered.x));
                eyelidCoverage = max(eyelidCoverage, horizontalCoverage * _BlinkAmount);
                
                // Lash line - darker at the very edge of the eyelid
                float lashLine = smoothstep(_EdgeSoftness * 0.3, 0.0, abs(distFromEdge));
                lashLine *= smoothstep(0.0, 0.5, _BlinkAmount); // Only visible when closing
                
                // Final alpha
                float finalAlpha = eyelidCoverage;
                
                // When nearly closed, ensure full coverage
                float closedBoost = smoothstep(0.9, 1.0, _BlinkAmount);
                finalAlpha = lerp(finalAlpha, 1.0, closedBoost);
                
                // Color
                float3 eyelidCol = lerp(_SkinColor.rgb, _EyelidColor.rgb, 0.5);
                
                // Darken toward center (depth effect)
                float depthDarkening = saturate(absY * 2.0);
                eyelidCol = lerp(eyelidCol * 0.6, eyelidCol, depthDarkening);
                
                // Lash line darkness
                eyelidCol = lerp(eyelidCol, float3(0.02, 0.01, 0.01), lashLine * 0.6);
                
                // When fully closed, go darker (like light through eyelids)
                eyelidCol = lerp(eyelidCol, _EyelidColor.rgb * 0.3, closedBoost);
                
                return float4(eyelidCol, finalAlpha);
            }
            ENDHLSL
        }
    }
    
    FallBack Off
}
