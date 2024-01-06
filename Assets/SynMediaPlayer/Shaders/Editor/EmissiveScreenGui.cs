using UnityEditor;
using UnityEngine;

namespace Synergiance.MediaPlayer {
	public class EmissiveScreenGui : ShaderBase {
		protected override void DoMain() {
			MaterialProperty texProp = FindProperty("_MainTex");
			editor.TexturePropertySingleLine(MakeLabel(texProp), texProp);
			ShaderProperty("_Emission");
			ShaderProperty("_ApplyGamma");
			ShowPropertyIfExists("_NativeRatio");
			ShowPropertyIfExists("_Ratio");
			editor.LightmapEmissionProperty();
		}

		protected override void MaterialChanged(Material material) {
			material.globalIlluminationFlags &= ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
		}
	}
}
