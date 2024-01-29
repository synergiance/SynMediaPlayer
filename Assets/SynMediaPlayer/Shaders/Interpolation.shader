Shader "Utility/Interpolation" {
	Properties {
		[NoScaleOffset] _PlaceholderTex ("Placeholder Texture", 2D) = "black" {}
		[NoScaleOffset] _UnityTex ("Unity Render Texture", 2D) = "black" {}
		[NoScaleOffset] _GaplessTex ("Gapless Render Texture", 2D) = "black" {}
		[NoScaleOffset] _StreamTex ("Stream Texture", 2D) = "black" {}
		[NoScaleOffset] _LowLatencyTex ("Low Latecy Texture", 2D) = "black" {}
		_UnityContrib ("Unity Contrib", Range(0,1)) = 1
		_GaplessContrib ("Gapless Contrib", Range(0,1)) = 0
		_StreamContrib ("Stream Contrib", Range(0,1)) = 0
		_LowLatencyContrib ("Low Latency Contrib", Range(0,1)) = 0
		_ShowPlaceholder ("Show Placeholder", Range(0,1)) = 1
		_StreamGamma ("Stream Gamma", Range(0, 1)) = 1
		[Toggle] _FlipStreamTextures("Flip Stream Textures", Float) = 1
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
			Texture2D _PlaceholderTex;

			float _UnityContrib;
			float _GaplessContrib;
			float _StreamContrib;
			float _LowLatencyContrib;
			float _ShowPlaceholder;

			float _StreamGamma;
			float _FlipStreamTextures;

			fixed4 frag (v2f_customrendertexture i) : SV_Target {
				float fac = min(1, 1 / min(0.00001, _UnityContrib + _StreamContrib + _LowLatencyContrib + _GaplessContrib));
				float2 uv = i.globalTexcoord.xy;
				float2 streamuv = _FlipStreamTextures > 0.5f ? float2(uv.x, 1 - uv.y) : uv;
				float4 col = float4(0,0,0,1);
				float4 contribs = float4(_UnityContrib, _GaplessContrib, _StreamContrib, _LowLatencyContrib);
				contribs *= fac;
				float streamGamma = saturate(_StreamGamma);
				col.rgb += _UnityTex.Sample(sampler_UnityTex, uv) * contribs.x;
				col.rgb += _GaplessTex.Sample(sampler_UnityTex, uv) * contribs.y;
				col.rgb += pow(_StreamTex.Sample(sampler_UnityTex, streamuv), lerp(1, 2.2, streamGamma)) * contribs.z;
				col.rgb += pow(_LowLatencyTex.Sample(sampler_UnityTex, streamuv), lerp(1, 2.2, streamGamma)) * contribs.w;
				col.rgb += _PlaceholderTex.Sample(sampler_UnityTex, uv) * (1 - dot(contribs, 1)) * _ShowPlaceholder;
				return col;
			}
			ENDCG
		}
	}
}
