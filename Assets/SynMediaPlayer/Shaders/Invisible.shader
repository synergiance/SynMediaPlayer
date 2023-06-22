Shader "Synergiance/Utility/Invisible" {
	Properties {
		_MainTex ("Texture", 2D) = "white" {}
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 100

		Blend Zero One

		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			Texture2D _MainTex;
			float4 vert (float4 vertex : POSITION) : SV_POSITION { return 0; }
			fixed4 frag (float4 vertex : SV_POSITION) : SV_Target { return 0; }
			ENDCG
		}
	}
}
