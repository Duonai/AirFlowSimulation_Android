Shader "AR/Camera Fade-in Shader"
{
    Properties {
        _MainTex ("Main Texture", 2D) = "white" {}
        _BackgroundTexture ("Background Texture", 2D) = "white" {}
        _CameraVisibility("Visibility of Camera Image", Range(0.0, 1.0)) = 1.0
    }

    // For GLES3 or GLES2 on device
    SubShader
    {
        // Renders the background to depth map transition effect.
        Pass
        {
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // Whether or not to enable depth-guided anti-aliasing on this shader.
            #define APPLY_DGAA 1
            // Whether or not to use polynomial color optimization or depth-color texture.
            #define APPLY_POLYNOMIAL_COLOR 1

            #include "UnityCG.cginc"

            uniform sampler2D _BackgroundTexture;
            uniform float _CameraVisibility;

            struct v2f
            {
                float4 grabPos : TEXCOORD0;
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata_base v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.grabPos = ComputeGrabScreenPos(o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 color = tex2Dproj(_BackgroundTexture, i.grabPos);
                return lerp(fixed4(0.0, 0.0, 0.0, 1.0), color, _CameraVisibility);
            }
            ENDCG
        } // Shader: Background to Depth
    } // Subshader

    FallBack Off
}
