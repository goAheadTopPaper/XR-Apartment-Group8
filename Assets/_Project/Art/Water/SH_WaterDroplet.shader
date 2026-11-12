Shader "XR Apartment/Water Droplet"
{
    Properties
    {
        _Color("Color", Color) = (0.65,0.84,0.9,0.65)
        _MainTex("Texture", 2D) = "white" {}
        _ShapeMask("Round droplet", Range(0, 1)) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination blend", Float) = 10
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha [_DstBlend]
        ZWrite Off
        Cull Off
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            half4 _Color;
            sampler2D _MainTex;
            half _ShapeMask;
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            v2f vert(appdata input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }
            fixed4 frag(v2f input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 color = input.color * tex2D(_MainTex, input.uv);
                float2 radial = input.uv * 2 - 1;
                color.a *= lerp(1, saturate(1 - dot(radial, radial)), _ShapeMask);
                return color;
            }
            ENDCG
        }
    }
}
