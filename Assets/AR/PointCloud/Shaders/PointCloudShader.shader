Shader "AR/PointCloudShader"
{
    Properties
    {
        _PointSize("Point size", Range(0, 0.2)) = 0.004
    }
    SubShader
    {
		Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }    //! 쉐이더 타입을 Transparent로 변경, Render Queue도 같이
		Blend SrcAlpha OneMinusSrcAlpha    //! Blending 옵션 설정
        //Tags { "RenderType"="Opaque" }
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma geometry geom

            #include "UnityCG.cginc"

            float _PointSize;

            struct v2g
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            struct g2f
            {
                fixed4 vertex : SV_POSITION;
                float4 color : COLOR;
				float2 uv : TEXCOORD0;
            };

            v2g vert (appdata_full v)
            {
                v2g o;
                o.vertex = mul(unity_ObjectToWorld, v.vertex);
                o.color = v.color;
                return o;
            }

            [maxvertexcount(4)]
            void geom (point v2g p[1], inout TriangleStream<g2f> triStream)
            {
                float3 up = float3(0, 1, 0);
                float3 look = _WorldSpaceCameraPos - p[0].vertex;
                look.y = 0;
                look = normalize(look);
                float3 right = cross(up, look);

                float halfSize = 0.5f * _PointSize;

				g2f out1;
				out1.vertex = p[0].vertex;
				out1.color = p[0].color;
				out1.uv = float2(1.0f, -1.0f);
				out1.vertex = float4(p[0].vertex + halfSize * right - halfSize * up, 1.0f);
				out1.vertex = UnityObjectToClipPos(out1.vertex);

				g2f out2;
				out2.vertex = p[0].vertex;
				out2.color = p[0].color;
				out2.uv = float2(1.0f, 1.0f);
				out2.vertex = float4(p[0].vertex + halfSize * right + halfSize * up, 1.0f);
				out2.vertex = UnityObjectToClipPos(out2.vertex);

				g2f out3;
				out3.vertex = p[0].vertex;
				out3.color = p[0].color;
				out3.uv = float2(-1.0f, -1.0f);
				out3.vertex = float4(p[0].vertex - halfSize * right - halfSize * up, 1.0f);
				out3.vertex = UnityObjectToClipPos(out3.vertex);

				g2f out4;
				out4.vertex = p[0].vertex;
				out4.color = p[0].color;
				out4.uv = float2(-1.0f, 1.0f);
				out4.vertex = float4(p[0].vertex - halfSize * right + halfSize * up, 1.0f);
				out4.vertex = UnityObjectToClipPos(out4.vertex);

				triStream.Append(out1);
				triStream.Append(out2);
				triStream.Append(out3);
				triStream.Append(out4);
            }

            fixed4 frag (g2f i) : SV_Target
            {
				if (i.uv.x*i.uv.x + i.uv.y*i.uv.y > 1) {
					discard;
				}
				if (i.color.a == 0) {
					discard;
				}
                return fixed4(GammaToLinearSpace(i.color.rgb), i.color.a);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
