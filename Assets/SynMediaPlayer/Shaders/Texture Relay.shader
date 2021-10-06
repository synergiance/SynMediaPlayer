Shader "Utility/Texture Relay" {
	Properties {
		[NoScaleOffset] _MainTex ("Main Color Texture", 2D) = "black" {}
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass {
			CGPROGRAM
			#include "UnityCustomRenderTexture.cginc"
			#pragma vertex CustomRenderTextureVertexShader
			#pragma fragment frag

			SamplerState sampler_MainTex;
			Texture2D _MainTex;

			fixed4 frag (v2f_customrendertexture i) : SV_Target {
				return _MainTex.Sample(sampler_MainTex, float2(i.globalTexcoord.x, 1 - i.globalTexcoord.y));
			}
			ENDCG
		}
	}
}
