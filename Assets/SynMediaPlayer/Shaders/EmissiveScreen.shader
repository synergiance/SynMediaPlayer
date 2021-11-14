Shader "Synergiance/Screens/Emissive Screen" {
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

		CGPROGRAM
		#pragma surface surf Standard fullforwardshadows
		#pragma target 3.0
		#pragma shader_feature _EMISSION

		#include "AspectSample.cginc"

		sampler2D _MainTex;
		float _ApplyGamma;
		float _Emission;
		float _Ratio;
		float _NativeRatio;

		struct Input {
			float2 uv_MainTex;
		};

		void surf(Input IN, inout SurfaceOutputStandard o) {
			fixed4 emission = sampleAspect(_MainTex, IN.uv_MainTex, _Ratio, _NativeRatio);
			o.Albedo = 0;
			o.Alpha = emission.a;
			emission.rgb = pow(emission.rgb, lerp(1, 2.2, _ApplyGamma)) * _Emission;
			o.Emission = emission;
			o.Metallic = 0;
			o.Smoothness = 0;
		}
		ENDCG
	}
	CustomEditor "EmissiveScreen3DGui"
}
