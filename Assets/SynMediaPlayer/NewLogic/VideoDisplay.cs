using Synergiance.MediaPlayer.Diagnostics;
using UnityEngine;

namespace Synergiance.MediaPlayer {
	public enum TextureType {
		Primary, Secondary, Overlay
	}
	public class VideoDisplay : DiagnosticBehaviour {
		[SerializeField] private Material videoMaterial;
		[SerializeField] private Renderer videoRenderer;
		[SerializeField] private int videoRendererIndex;
		[SerializeField] private string primaryTextureProperty = "_MainTex";
		[SerializeField] private string secondaryTextureProperty = "_SecondTex";
		[SerializeField] private string overlayTextureProperty = "_OverlayTex";
		[SerializeField] private string secondaryWeightProperty = "_SecondaryWeight";
		[SerializeField] private string overlayWeightProperty = "_OverlayWeight";
		[SerializeField] private AudioSource[] audioSources;
		[Range(0, 2)] [SerializeField] private float relativeVolume = 1;
		[SerializeField] private Collider activeZone;
		[SerializeField] private DisplayManager displayManager;

		protected override string DebugName => "Video Display";
		protected override string DebugColor => ColorToHtmlStringRGB(new Color(0.5f, 0.1f, 0.65f));

		private bool initialized;
		private bool isValid;

		private float secondaryWeight;
		private float overlayWeight;

		public float SecondaryWeight {
			get => secondaryWeight;
			set {
				secondaryWeight = value;
				UpdateMaterialWeights();
			}
		}

		public float OverlayWeight {
			get => overlayWeight;
			set {
				overlayWeight = value;
				UpdateMaterialWeights();
			}
		}

		void Start() {
			Initialize();
		}

		private void Initialize() {
			if (initialized) return;
			CheckValid();
			initialized = true;
		}

		private void CheckValid() {
			if (activeZone == null) {
				LogError("No zone assigned!");
				return;
			}

			if (activeZone.gameObject != gameObject) {
				LogError("Zone not on this game object!");
				return;
			}

			if (displayManager == null) {
				LogError("Display manager not found!");
				return;
			}

			if (audioSources == null || audioSources.Length == 0) {
				LogError("Audio sources missing!");
				return;
			}

			if (videoMaterial == null) {
				if (videoRenderer == null) {
					LogError("No output material!");
					return;
				}

				if (videoRendererIndex >= videoRenderer.materials.Length) {
					LogError("Invalid material index!");
					return;
				}

				videoMaterial = videoRenderer.materials[videoRendererIndex];
				if (videoMaterial == null) {
					LogError("Video material missing!");
					return;
				}
			}

			foreach (AudioSource audioSource in audioSources) {
				audioSource.enabled = false;
			}

			isValid = true;
		}

		private void UpdateMaterialWeights() {
			if (!isValid) return;
			videoMaterial.SetFloat(secondaryWeightProperty, secondaryWeight);
			videoMaterial.SetFloat(overlayWeightProperty, overlayWeight);
		}

		/// <summary>
		/// Sets video texture in display material
		/// </summary>
		/// <param name="_type">Integer representing texture type.
		/// 0 for primary, 1 for secondary, and 2 for overlay.</param>
		/// <param name="_texture">The texture to give to the display</param>
		/// <returns>True on success</returns>
		public bool _SetVideoTexture(int _type, Texture _texture) {
			if (!isValid) return false;
			string texProp;
			switch (_type) {
				case 0:
					texProp = primaryTextureProperty;
					break;
				case 1:
					texProp = secondaryTextureProperty;
					break;
				case 2:
					texProp = overlayTextureProperty;
					break;
				default:
					LogError("Invalid texture type!");
					return false;
			}
			videoMaterial.SetTexture(texProp, _texture);
			return true;
		}

		/// <summary>
		/// Gets the audio template from the display
		/// </summary>
		/// <param name="_sources">AudioSource array will be handed back here</param>
		/// <param name="_volume">Relative volume will be set here</param>
		/// <returns>True if valid</returns>
		public bool _GetAudioTemplate(ref AudioSource[] _sources, ref float _volume) {
			if (!isValid) return false;
			_sources = audioSources;
			_volume = relativeVolume;
			return true;
		}

		public bool _SwitchSource(int _source) {
			return true;
		}
	}
}
