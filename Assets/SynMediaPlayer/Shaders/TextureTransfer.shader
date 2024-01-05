Shader "Synergiance/Utility/TextureTransfer" {
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader {
        LOD 100

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float4 vert (float4 vertex : POSITION) : SV_POSITION { return 0; }

            fixed4 frag (float4 vertex : SV_POSITION) : SV_Target { return 0; }
            ENDCG
        }
    }
}
