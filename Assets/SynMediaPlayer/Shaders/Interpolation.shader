Shader "Utility/Interpolation" {
	Properties {
		[NoScaleOffset] _UnityTex ("Unity Render Texture", 2D) = "black" {}
		[NoScaleOffset] _GaplessTex ("Gapless Render Texture", 2D) = "black" {}
		[NoScaleOffset] _StreamTex ("Stream Texture", 2D) = "black" {}
		[NoScaleOffset] _LowLatencyTex ("Low Latecy Texture", 2D) = "black" {}
		_UnityContrib ("Unity Contrib", Range(0,1)) = 1
		_GaplessContrib ("Gapless Contrib", Range(0,1)) = 0
		_StreamContrib ("Stream Contrib", Range(0,1)) = 0
		_LowLatencyContrib ("Low Latency Contrib", Range(0,1)) = 0
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass {
			CGPROGRAM
			#include "UnityCustomRenderTexture.cginc"
			#pragma vertex CustomRenderTextureVertexShader
			#pragma fragment frag

			SamplerState sampler_UnityTex;
			Texture2D _UnityTex;
			Texture2D _GaplessTex;
			Texture2D _StreamTex;
			Texture2D _LowLatencyTex;

			float _UnityContrib;
			float _GaplessContrib;
			float _StreamContrib;
			float _LowLatencyContrib;

			fixed4 frag (v2f_customrendertexture i) : SV_Target {
				float fac = min(1, 1 / min(0.00001, _UnityContrib + _StreamContrib + _LowLatencyContrib + _GaplessContrib));
				float2 uv = i.globalTexcoord.xy;
				float4 col = float4(0,0,0,1);
				col.rgb += _UnityTex.Sample(sampler_UnityTex, uv) * fac * _UnityContrib;
				col.rgb += _GaplessTex.Sample(sampler_UnityTex, uv) * fac * _GaplessContrib;
				col.rgb += _StreamTex.Sample(sampler_UnityTex, uv) * fac * _StreamContrib;
				col.rgb += _LowLatencyTex.Sample(sampler_UnityTex, uv) * fac * _LowLatencyContrib;
				return col;
			}
			ENDCG
		}
	}
}
