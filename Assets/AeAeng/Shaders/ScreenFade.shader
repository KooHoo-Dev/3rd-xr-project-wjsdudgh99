// 화면 페이드 (잠들기 / 판 사이 암전). 카메라에 붙은 작은 구 안쪽에 그린다.
// 모든 것 위에 그려지도록 ZTest Always + Overlay 큐. 파티 문구/자막(TMP)은 이보다 뒤 큐(4001)라서 검은 화면 위에 보임.
// Quest 싱글 패스 인스턴스드 스테레오 매크로 포함 (안 넣으면 한쪽 눈에만 그려짐).
Shader "AeAeng/ScreenFade"
{
    Properties
    {
        _Color ("Color", Color) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Overlay" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ScreenFade"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

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

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return _Color;
            }
            ENDHLSL
        }
    }
}
