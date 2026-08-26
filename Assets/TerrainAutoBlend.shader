Shader "Custom/TerrainAutoBlend"
{
    Properties
    {
        // ---- Grama (3 variantes) ----
        _GrassA ("Grass A", 2D) = "white" {}
        _GrassB ("Grass B", 2D) = "white" {}
        _GrassC ("Grass C", 2D) = "white" {}

        // ---- Terra ----
        _Dirt ("Dirt", 2D) = "white" {}

        // ---- Pedra (2 variantes) ----
        _RockA ("Rock A", 2D) = "white" {}
        _RockB ("Rock B", 2D) = "white" {}

        // ---- Tiling ----
        _Tiling ("Tiling (todas texturas)", Float) = 8

        // ---- Controle de altura (em unidades do mundo, Y) ----
        _DirtMaxHeight ("Altura máxima da Terra", Float) = 5
        _DirtGrassBlend ("Suavização Terra->Grama", Float) = 3
        _GrassMaxHeight ("Altura máxima da Grama (acima disso só pedra)", Float) = 60
        _GrassRockBlend ("Suavização Grama->Pedra (altura)", Float) = 8

        // ---- Controle de inclinação (0 = plano, 1 = vertical) ----
        _SlopeThreshold ("Limite de inclinação p/ Pedra", Range(0,1)) = 0.45
        _SlopeBlend ("Suavização da inclinação", Range(0.01,1)) = 0.25

        // ---- Ruído para variar entre as 3 gramas e as 2 pedras ----
        _NoiseScaleGrass ("Escala do ruído (Grama)", Float) = 0.02
        _NoiseScaleRock ("Escala do ruído (Pedra)", Float) = 0.015
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_GrassA); SAMPLER(sampler_GrassA);
            TEXTURE2D(_GrassB); SAMPLER(sampler_GrassB);
            TEXTURE2D(_GrassC); SAMPLER(sampler_GrassC);
            TEXTURE2D(_Dirt);   SAMPLER(sampler_Dirt);
            TEXTURE2D(_RockA);  SAMPLER(sampler_RockA);
            TEXTURE2D(_RockB);  SAMPLER(sampler_RockB);

            float _Tiling;
            float _DirtMaxHeight;
            float _DirtGrassBlend;
            float _GrassMaxHeight;
            float _GrassRockBlend;
            float _SlopeThreshold;
            float _SlopeBlend;
            float _NoiseScaleGrass;
            float _NoiseScaleRock;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
            };

            // Hash/ruído simples baseado em posição do mundo (pseudo-aleatório suave)
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = hash(i);
                float b = hash(i + float2(1,0));
                float c = hash(i + float2(0,1));
                float d = hash(i + float2(1,1));
                float2 u = f*f*(3.0-2.0*f);
                return lerp(a,b,u.x) + (c-a)*u.y*(1.0-u.x) + (d-b)*u.x*u.y;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(IN.normalOS);
                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS  = posInputs.positionWS;
                OUT.normalWS    = normInputs.normalWS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 worldUV = IN.positionWS.xz / _Tiling;
                float3 normal = normalize(IN.normalWS);

                // ---------- 1) Escolhe entre as 3 gramas usando ruído de baixa frequência ----------
                float grassNoise = valueNoise(IN.positionWS.xz * _NoiseScaleGrass);
                half4 grassA = SAMPLE_TEXTURE2D(_GrassA, sampler_GrassA, worldUV);
                half4 grassB = SAMPLE_TEXTURE2D(_GrassB, sampler_GrassB, worldUV);
                half4 grassC = SAMPLE_TEXTURE2D(_GrassC, sampler_GrassC, worldUV);

                half4 grassMix = lerp(grassA, grassB, smoothstep(0.25, 0.5, grassNoise));
                grassMix = lerp(grassMix, grassC, smoothstep(0.6, 0.85, grassNoise));

                // ---------- 2) Escolhe entre as 2 pedras usando outro ruído ----------
                float rockNoise = valueNoise(IN.positionWS.xz * _NoiseScaleRock + 100.0);
                half4 rockA = SAMPLE_TEXTURE2D(_RockA, sampler_RockA, worldUV);
                half4 rockB = SAMPLE_TEXTURE2D(_RockB, sampler_RockB, worldUV);
                half4 rockMix = lerp(rockA, rockB, smoothstep(0.4, 0.6, rockNoise));

                // ---------- 3) Terra ----------
                half4 dirt = SAMPLE_TEXTURE2D(_Dirt, sampler_Dirt, worldUV);

                // ---------- 4) Blend por ALTURA: Terra -> Grama -> (limite superior de grama) ----------
                float height = IN.positionWS.y;
                float dirtToGrass = smoothstep(_DirtMaxHeight - _DirtGrassBlend, _DirtMaxHeight + _DirtGrassBlend, height);
                half4 heightMix = lerp(dirt, grassMix, dirtToGrass);

                float grassToRockByHeight = smoothstep(_GrassMaxHeight - _GrassRockBlend, _GrassMaxHeight + _GrassRockBlend, height);
                heightMix = lerp(heightMix, rockMix, grassToRockByHeight);

                // ---------- 5) Blend por INCLINAÇÃO: qualquer altura vira Pedra se for íngreme ----------
                float slope = 1.0 - normal.y; // 0 = plano, 1 = parede vertical
                float slopeFactor = smoothstep(_SlopeThreshold - _SlopeBlend, _SlopeThreshold + _SlopeBlend, slope);

                half4 finalColor = lerp(heightMix, rockMix, slopeFactor);

                // ---------- Iluminação básica (Lambert + luz principal URP) ----------
                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(normal, mainLight.direction));
                half3 lighting = mainLight.color * NdotL + unity_AmbientSky.rgb;

                finalColor.rgb *= lighting;
                return finalColor;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
