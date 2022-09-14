using Synergiance.MediaPlayer.Diagnostics;
using UnityEngine;

namespace Synergiance.MediaPlayer {
	public enum TextureType {
		Primary, Secondary, Overlay
	}
	public class VideoDisplay : DiagnosticBehaviour {
		[SerializeField] private Material videoMaterial;
		[SerializeField] private Renderer videoRenderer;
		[SerializeField] private VideoController videoController;
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
		[SerializeField] private string defaultVideoPlayer;
		[SerializeField] private bool disableSwitchingSource;

		protected override string DebugName => "Video Display";
		protected override string DebugColor => ColorToHtmlStringRGB(new Color(0.5f, 0.1f, 0.65f));

		private bool initialized;
		private bool isValid;

		private float secondaryWeight;
		private float overlayWeight;

		private int currentId = -1;
		private int defaultId = -1;
		private int identifier;

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

			displayManager._RegisterDisplay(this, defaultVideoPlayer);

			// TODO: Link up to video controller

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
		public bool _GetAudioTemplate(out AudioSource[] _sources, out float _volume) {
			if (!isValid) {
				LogError("Display invalid!");
				_sources = null;
				_volume = 0;
				return false;
			}
			_sources = audioSources;
			_volume = relativeVolume;
			return true;
		}

		/// <summary>
		/// A run once setter of the default ID
		/// </summary>
		/// <param name="_id">The ID to use as default</param>
		public void _SetDefaultSourceId(int _id) {
			if (defaultId >= 0) {
				LogWarning("Default ID already set!");
				return;
			}

			if (_id < 0) {
				LogError("Cannot set default ID to less than 0!");
				return;
			}

			Log($"Setting default ID to {_id} for \"{defaultVideoPlayer}\"");
			defaultId = _id;

			if (currentId >= 0) return;

			currentId = defaultId;
			_SwitchSource(defaultId);
		}

		public bool _SwitchSource(int _source) {
			if (disableSwitchingSource) return false;
			if (!isValid) return false;
			displayManager._SwitchSource(identifier, _source);
			// TODO: If linked, switch source on video controls
			return true;
		}
	}
}
