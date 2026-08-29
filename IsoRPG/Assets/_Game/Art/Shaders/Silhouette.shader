// Силуэт персонажа сквозь препятствия.
//
// В изометрии камера жёстко привязана к углу, и обойти дерево, чтобы
// увидеть себя, игрок не может. Значит скрытый персонаж — это потерянное
// управление: не видно ни где ты, ни что с тобой происходит.
//
// Приём стандартный для жанра: второй проход рисует персонажа ровно там, где
// он ЗАКРЫТ геометрией. Достигается сравнением глубины наоборот — ZTest
// Greater пропускает только те пиксели, что дальше уже нарисованного.
//
// ZWrite Off обязателен: силуэт не должен попадать в буфер глубины, иначе он
// начнёт закрывать собой то, что нарисовано после него.

Shader "IsoRPG/Silhouette"
{
    Properties
    {
        _BaseColor ("Цвет силуэта", Color) = (0.35, 0.85, 0.55, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "Silhouette"

            ZTest Greater
            ZWrite Off
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
            CBUFFER_END

            Varyings vert (Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                return _BaseColor;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
