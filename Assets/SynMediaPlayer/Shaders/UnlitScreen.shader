Shader "Synergiance/Screens/Unlit Screen" {
	Properties {
		[NoScaleOffset] _MainTex ("Texture", 2D) = "white" {}
		_Emission ("Emission Scale", Float) = 1
		_ApplyGamma ("Gamma", Range(0,1)) = 0
		_NativeRatio ("Native Ratio", Float) = 1.77778
		_Ratio ("Ratio", Float) = 1.77778
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200

		Pass {
			Name "Unlit"
			Tags { "LightMode"="Always" }
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			// make fog work
			#pragma multi_compile_fog

			#include "UnityCG.cginc"
			#include "AspectSample.cginc"

			struct appdata {
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f {
				float2 uv : TEXCOORD0;
				UNITY_FOG_COORDS(1)
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;
			float _ApplyGamma;
			float _Emission;
			float _Ratio;
			float _NativeRatio;

			v2f vert (appdata v) {
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				UNITY_TRANSFER_FOG(o,o.vertex);
				return o;
			}

			fixed4 frag (v2f i) : SV_Target {
				// sample the texture
				fixed4 col = sampleAspect(_MainTex, i.uv, _Ratio, _NativeRatio);
				col.rgb = lerp(col.rgb, pow(col.rgb,2.2), _ApplyGamma) * _Emission;
				col.a = 1;
				// apply fog
				UNITY_APPLY_FOG(i.fogCoord, col);
				return col;
			}
			ENDCG
		}
	}
	CustomEditor "EmissiveScreen3DGui"
}
