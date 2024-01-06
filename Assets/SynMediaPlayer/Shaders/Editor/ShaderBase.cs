// Syn's Shader GUI Base
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Synergiance.MediaPlayer {
	public class ShaderBase : ShaderGUI {

		protected virtual string version => "1.0";

		protected Material target;
		protected MaterialEditor editor;
		protected MaterialProperty[] properties;

		public delegate void FoldoutFunc();
		public struct FoldoutItem {
			public string title;
			public bool enabled;
			public GUIContent display;
			public FoldoutFunc function;
			public void DisplayFoldout() {
				BoldFoldout(ref enabled, display == null ? MakeLabel(title) : display, function);
			}
		}

		protected static GUIContent staticLabel = new();
		#if UNITY_2017
		protected static ColorPickerHDRConfig emissionConfig = new ColorPickerHDRConfig(0f, 99f, 1f / 99f, 3f);
		#endif

		protected virtual bool hasGradient => false;
		protected Dictionary<string, Gradient> gradients;
		protected virtual bool hasString => false;
		protected Dictionary<string, string> strings;
		protected virtual bool hasFoldoutArray => false;
		protected Dictionary<string, List<FoldoutItem> > FoldoutArrays;

		protected static void SetKeyword(Material m, string keyword, bool state) {
			if (state) {
				m.EnableKeyword(keyword);
			} else {
				m.DisableKeyword(keyword);
			}
		}

		protected void SetKeyword(string keyword, bool state) {
			foreach (Material m in editor.targets) {
				SetKeyword(m, keyword, state);
			}
		}

		public static void WipeKeywords(Material m) {
			foreach (string keyword in m.shaderKeywords) SetKeyword(m, keyword, false);
		}

		protected static GUIContent MakeLabel(string text, string tooltip = null) {
			staticLabel.text = text;
			staticLabel.tooltip = tooltip;
			return staticLabel;
		}

		protected static GUIContent MakeLabel(MaterialProperty property, string tooltip = null) {
			staticLabel.text = property.displayName;
			staticLabel.tooltip = tooltip;
			return staticLabel;
		}

		static protected bool BoldFoldout(bool foldout, GUIContent content) {
			GUIStyle boldFoldoutStyle = new(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
			return EditorGUILayout.Foldout(foldout, content, boldFoldoutStyle);
		}

		static protected bool BoldFoldout(bool foldout, string content) {
			return BoldFoldout(foldout, MakeLabel(content));
		}

		static protected void BoldFoldout(ref bool foldout, GUIContent content, FoldoutFunc function) {
			if (foldout = BoldFoldout(foldout, content)) {
				function();
				EditorGUILayout.Space();
			}
		}

		static protected void BoldFoldout(ref bool foldout, string content, FoldoutFunc function) {
			BoldFoldout(ref foldout, MakeLabel(content), function);
		}

		protected void ShowFoldoutArray(string name) {
			foreach (FoldoutItem foldoutItem in FoldoutArrays[name]) foldoutItem.DisplayFoldout();
		}

		protected void AddFoldoutArray(string name, List<FoldoutItem> array) {
			FoldoutArrays.Add(name, array);
		}
		
		protected MaterialProperty FindProperty(string name, bool isMandatory = true) {
			return FindProperty(name, properties, isMandatory);
		}

		protected bool PropertyExists(string name) {
			return FindProperty(name, properties, false) != null;
		}

		protected void RecordAction(string label) {
			editor.RegisterPropertyChangeUndo(label);
		}

		protected void ShaderProperty(MaterialProperty prop, GUIContent label) {
			editor.ShaderProperty(prop, label);
		}

		protected void ShaderProperty(MaterialProperty prop, string display = null, string display2 = null) {
			ShaderProperty(prop, (display != null ? MakeLabel(display, display2) : MakeLabel(prop)));
		}
		
		protected void ShaderProperty(string enumName, string display = null, string display2 = null) {
			ShaderProperty(FindProperty(enumName), display, display2);
		}

		protected void ShowPropertyIfExists(string enumName, string display = null, string display2 = null) {
			MaterialProperty property = FindProperty(enumName, false);
			if (property != null) ShaderProperty(property, display, display2);
		}

		protected void Vec2Prop(string label, MaterialProperty prop1, MaterialProperty prop2) {
			Vec2Prop(MakeLabel(label), prop1, prop2);
		}

		protected void Vec2IntProp(string label, MaterialProperty prop1, MaterialProperty prop2) {
			Vec2IntProp(MakeLabel(label), prop1, prop2);
		}

		protected void Vec2Prop(GUIContent label, MaterialProperty prop1, MaterialProperty prop2) {
			EditorGUI.BeginChangeCheck();
			EditorGUI.showMixedValue = prop1.hasMixedValue || prop2.hasMixedValue;
			Vector2 vec2 = EditorGUILayout.Vector2Field(label, new Vector2(prop1.floatValue, prop2.floatValue));
			EditorGUI.showMixedValue = false;
			if (EditorGUI.EndChangeCheck()) {
				prop1.floatValue = vec2.x;
				prop2.floatValue = vec2.y;
			}
		}

		protected void Vec2IntProp(GUIContent label, MaterialProperty prop1, MaterialProperty prop2) {
			EditorGUI.BeginChangeCheck();
			EditorGUI.showMixedValue = prop1.hasMixedValue || prop2.hasMixedValue;
			Vector2 vec2 = EditorGUILayout.Vector2Field(label, new Vector2(prop1.floatValue, prop2.floatValue));
			EditorGUI.showMixedValue = false;
			if (EditorGUI.EndChangeCheck()) {
				prop1.floatValue = Mathf.Floor(vec2.x);
				prop2.floatValue = Mathf.Floor(vec2.y);
			}
		}

		protected void DoColorTexArea(string mapName, string colName, GUIContent label = null) {
			MaterialProperty map = FindProperty(mapName);
			MaterialProperty col = FindProperty(colName);
			editor.TexturePropertySingleLine(label != null ? label : MakeLabel(map), map, col);
		}

		protected void Vec3Prop(MaterialProperty prop, string display = null, string display2 = null) {
			Vec3Prop(display == null ? MakeLabel(prop) : MakeLabel(display, display2), prop);
		}

		protected void Vec3Prop(GUIContent label, MaterialProperty prop) {
			EditorGUI.BeginChangeCheck();
			EditorGUI.showMixedValue = prop.hasMixedValue;
			Vector3 vec3 = EditorGUILayout.Vector3Field(label, new Vector3(prop.vectorValue.x, prop.vectorValue.y, prop.vectorValue.z));
			EditorGUI.showMixedValue = false;
			if (EditorGUI.EndChangeCheck()) prop.vectorValue = new Vector4(vec3.x, vec3.y, vec3.z, prop.vectorValue.w);
		}

		protected void HDRColorTextureProperty(GUIContent label, MaterialProperty textureProp, MaterialProperty colorProperty, bool showAlpha) {
			#if UNITY_2017
			editor.TexturePropertyWithHDRColor(label, textureProp, colorProperty, emissionConfig, showAlpha);
			#else
			editor.TexturePropertyWithHDRColor(label, textureProp, colorProperty, showAlpha);
			#endif
		}

		protected void GradientProperty(GUIContent label, string prefix, bool hdr) {
			#if UNITY_2017
			EditorGUILayout.LabelField("Gradients not supported in this Unity version");
			#else
			if (!gradients.ContainsKey(prefix)) gradients.Add(prefix, ReadGradientFromMaterial(prefix));
			EditorGUI.BeginChangeCheck();
			gradients[prefix] = EditorGUILayout.GradientField(label, gradients[prefix], hdr, null);
			if (EditorGUI.EndChangeCheck()) WriteGradientToMaterial(prefix, gradients[prefix]);
			#endif
		}

		protected Gradient ReadGradientFromMaterial(string prefix) {
			Vector4[] alphas = new Vector4[8];
			Color[] colors = new Color[8];
			int numAlphas = 0;
			int numColors = 0;
			for (int c = 0; c < 8; c++) {
				alphas[c] = target.GetVector(prefix + "Alpha" + (c + 1));
				colors[c] = target.GetVector(prefix + "Color" + (c + 1));
				if (c > 0 && numAlphas == 0 && alphas[c].y == -1) numAlphas = c;
				if (c > 0 && numColors == 0 && alphas[c].z == -1) numColors = c;
			}
			Gradient gradient = new Gradient();
			GradientColorKey[] colorKeys = new GradientColorKey[numColors];
			GradientAlphaKey[] alphaKeys = new GradientAlphaKey[numAlphas];
			for (int c = 0; c < 8; c++) {
				if (c < numAlphas) {
					alphaKeys[c].alpha = alphas[c].x;
					alphaKeys[c].time = alphas[c].y;
				}
				if (c < numColors) {
					colorKeys[c].color = colors[c];
					colorKeys[c].time = alphas[c].z;
				}
			}
			gradient.SetKeys(colorKeys, alphaKeys);
			return gradient;
		}

		protected void WriteGradientToMaterial(string prefix, Gradient gradient) {
			Vector4 alpha = new Vector4();
			for (int c = 0; c < 8; c++) {
				alpha.x = c >= gradient.alphaKeys.Length ? 0 : gradient.alphaKeys[c].alpha;
				alpha.y = c >= gradient.alphaKeys.Length ? -1 : gradient.alphaKeys[c].time;
				alpha.z = c >= gradient.colorKeys.Length ? -1 : gradient.colorKeys[c].time;
				alpha.w = 0;
				target.SetVector(prefix + "Alpha" + (c + 1), alpha);
				target.SetColor(prefix + "Color" + (c + 1), c >= gradient.colorKeys.Length ? Color.black : gradient.colorKeys[c].color);
			}
		}

		protected void StringProperty(GUIContent label, string prefix, int length) {}

		protected string ReadStringFromMaterial(string prefix, int length) { return ""; }

		protected void WriteStringToMaterial(string prefix, int length, string str) {}

		protected static bool HasClamp(MaterialProperty prop) {
			if (prop.type != MaterialProperty.PropType.Texture) return false;
			
			foreach (Material m in prop.targets) {
				Texture tex = m.GetTexture(prop.name);
				if (tex != null && (tex.wrapModeU == TextureWrapMode.Clamp && tex.wrapModeV == TextureWrapMode.Clamp)) return true;
			}

			return false;
		}

		/*
		protected static void FixToonRampTexture(MaterialProperty prop) {
			foreach (Material m in prop.targets)
				m.GetTexture(prop.name).wrapMode = TextureWrapMode.Clamp;
		}
		*/

		protected void CheckForClamp(MaterialProperty prop) {
			if (!HasClamp(prop)) {
				EditorGUILayout.HelpBox("Set your texture's wrapping mode to clamp to get rid of glitches", MessageType.Warning);
				/*
				if (editor.HelpBoxWithButton(MakeLabel("This texture's wrap mode is not set to Clamp"), MakeLabel("Fix Now"))) {
					FixToonRampTexture(prop);
				}
				*/
			}
		}

		protected bool m_FirstTimeApply = true;
		
		public override void OnGUI(MaterialEditor editor, MaterialProperty[] properties) {
			this.target = editor.target as Material;
			this.editor = editor;
			this.properties = properties;

			// Make sure that needed setup (ie keywords/renderqueue) are set up if we're switching some existing
			// material to a standard shader.
			// Do this before any GUI code has been issued to prevent layout issues in subsequent GUILayout statements (case 780071)
			if (m_FirstTimeApply) {
				MaterialChanged((Material)editor.target);
				if (hasGradient) gradients = new Dictionary<string, Gradient>();
				if (hasString) strings = new Dictionary<string, string>();
				if (hasFoldoutArray) FoldoutArrays = new Dictionary<string, List<FoldoutItem> >();
				m_FirstTimeApply = false;
				InitializeInspector();
			}

			EditorGUI.BeginChangeCheck();
			DoMain();
			if (EditorGUI.EndChangeCheck()) {
				foreach (var obj in editor.targets) {
					MaterialChanged((Material)obj);
				}
			}
			EditorGUILayout.Space();
			GUILayout.Label("Version: " + version);
		}

		protected virtual void DoMain() {
			GUILayout.Label("Please add DoMain() to your inspector!");
		}

		protected virtual void MaterialChanged(Material material) {}

		protected virtual void InitializeInspector() {}
	}
}