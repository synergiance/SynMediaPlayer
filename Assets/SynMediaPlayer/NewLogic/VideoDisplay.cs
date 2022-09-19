using Synergiance.MediaPlayer.Diagnostics;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Synergiance.MediaPlayer {
	/// <summary>
	/// This is meant to be fed into the SetVideoTexture method on the
	/// Video Display.
	/// </summary>
	public enum TextureType {
		Primary, Secondary, Overlay
	}

	/// <summary>
	/// This component drives the display of a video player. It hooks up to
	/// video component and receives the relevant textures from the video
	/// manager. It can also be linked with a video controller, so that it can
	/// always hook up to the same video player as a video controller instance.
	/// </summary>
	[DefaultExecutionOrder(-5), UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
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
		[Range(0, 1)] [SerializeField] private float relativeVolume = 1f;
		[SerializeField] private Collider activeZone;
		[SerializeField] private float audioPriority;
		[SerializeField] private DisplayManager displayManager;
		[SerializeField] private string defaultVideoPlayer;
		[SerializeField] private bool disableSwitchingSource;

		protected override string DebugName => "Video Display";
		protected override string DebugColor => ColorToHtmlStringRGB(new Color(0.5f, 0.1f, 0.65f));

		private bool initialized;
		private bool isValid;

		private bool audioActive;
		private bool videoControllerLinked;
		private bool settingControllerSource;

		private float secondaryWeight;
		private float overlayWeight;

		private bool hasSecondaryTexProp;
		private bool hasOverlayTexProp;
		private bool hasSecondaryWeightProp;
		private bool hasOverlayWeightProp;

		private int currentId = -1;
		private int defaultId = -1;
		private int identifier = -1;

		/// <summary>
		/// Accessor for whether display is meant to have audio.
		/// </summary>
		public bool HasAudio { get; private set; }

		/// <summary>
		/// Accessor for whether local player is in the audio zone
		/// </summary>
		public bool AudioActive {
			get => audioActive;
			private set {
				audioActive = value;
				Initialize();
				if (!isValid) return;
				displayManager._AudioZoneActiveCallback(identifier);
			}
		}

		/// <summary>
		/// Interface for the secondary weight. Setting to 1 will display only
		/// the secondary texture, and setting to 0 will display only the
		/// primary texture.
		/// </summary>
		public float SecondaryWeight {
			get => secondaryWeight;
			set {
				Initialize();
				secondaryWeight = value;
				UpdateMaterialWeights();
			}
		}

		/// <summary>
		/// Interface for the overlay weight. This is the opacity of the
		/// overlay. 1 is fully opaque, and 0 is fully transparent.
		/// </summary>
		public float OverlayWeight {
			get => overlayWeight;
			set {
				Initialize();
				overlayWeight = value;
				UpdateMaterialWeights();
			}
		}

		void Start() {
			Initialize();
		}

		private void Initialize() {
			if (initialized) return;
			Log("Initialize!");
			CheckValid();
			initialized = true;
		}

		private void CheckValid() {
			if (isValid) {
				LogWarning("Breaking validation check loop!");
				return;
			}

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

			if (string.IsNullOrWhiteSpace(primaryTextureProperty)) {
				LogError("Must specify a primary texture property!");
				return;
			}

			if (!videoMaterial.HasProperty(primaryTextureProperty)) {
				LogError($"Primary texture property \"{primaryTextureProperty}\" does not exist on material!");
				return;
			}

			HasAudio = audioSources != null && audioSources.Length > 0;
			Log(HasAudio ? "Has audio" : "No audio detected.");
			// ReSharper disable once PossibleNullReferenceException
			if (HasAudio) foreach (AudioSource audioSource in audioSources) {
				audioSource.enabled = false;
			}

			Log(identifier < 0 ? "Registering display" : "Display already registered");
			if (identifier < 0) identifier = displayManager._RegisterDisplay(this, defaultVideoPlayer, audioPriority);
			if (identifier < 0) {
				LogError("Failed to register display!");
				return;
			}

			hasSecondaryTexProp = PropertyValidAndExists(secondaryTextureProperty);
			hasSecondaryWeightProp = hasSecondaryTexProp && PropertyValidAndExists(secondaryWeightProperty);
			hasOverlayTexProp = PropertyValidAndExists(overlayTextureProperty);
			hasOverlayWeightProp = hasOverlayTexProp && PropertyValidAndExists(overlayWeightProperty);
			string propLog = "Properties: ";
			propLog += " SecondTex: " + hasSecondaryTexProp;
			propLog += ", OverlayTex: " + hasOverlayTexProp;
			propLog += ", SecondWeight: " + hasSecondaryWeightProp;
			propLog += ", OverlayWeight: " + hasOverlayWeightProp;
			Log(propLog);

			if (videoController != null) {
				Log("Linking display");
				videoControllerLinked = videoController._LinkDisplay(this);
			}

			isValid = true;

			UpdateMaterialWeights();

			if (defaultId >= 0) {
				Log($"Switching to default source {defaultId}");
				SwitchSourceInternal(defaultId);
			}
		}

		private void UpdateMaterialWeights() {
			if (!isValid) return;

			Log("Update Material Weights");

			if (hasSecondaryWeightProp)
				videoMaterial.SetFloat(secondaryWeightProperty, secondaryWeight);

			if (hasOverlayWeightProp)
				videoMaterial.SetFloat(overlayWeightProperty, overlayWeight);
		}

		private bool PropertyValidAndExists(string _propertyName) {
			return !string.IsNullOrWhiteSpace(_propertyName)
			       && videoMaterial.HasProperty(_propertyName);
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
					if (!hasSecondaryTexProp) return false;
					texProp = secondaryTextureProperty;
					break;
				case 2:
					if (!hasOverlayTexProp) return false;
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
			if (!isValid || !HasAudio) {
				LogError(isValid ? "Display doesn't have audio!" : "Display invalid!");

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

			if (isValid && currentId < 0) {
				Log($"Switching to default source {defaultId}");
				SwitchSourceInternal(defaultId);
			}
		}

		/// <summary>
		/// Switches the source of the display to a different source.
		/// </summary>
		/// <param name="_source">ID of the new source</param>
		/// <returns>True on success, false if there's an error</returns>
		public bool _SwitchSource(int _source) {
			Initialize();

			if (settingControllerSource) {
				Log("Breaking feedback loop");
				return false;
			}

			if (disableSwitchingSource) {
				LogWarning("Source switching disabled!");
				return false;
			}

			SwitchSourceInternal(_source);

			if (videoControllerLinked) {
				settingControllerSource = true;
				videoController._SwitchSource(_source);
				settingControllerSource = false;
			}

			return true;
		}

		private bool SwitchSourceInternal(int _source) {
			if (!isValid) {
				LogError("Display is invalid!");
				return false;
			}

			if (_source == currentId) {
				LogWarning("Source already selected!");
				return true;
			}

			Log($"Switching source to {_source}");
			if (!displayManager._SwitchSource(identifier, _source)) {
				LogError("Couldn't switch source!");
				return false;
			}

			currentId = _source;

			return true;
		}

		public void _RelayEnter() {
			Log("Player entered zone");
			AudioActive = true;
		}

		public void _RelayExit() {
			Log("Player exited zone");
			AudioActive = false;
		}

		private bool ValidatePlayer(VRCPlayerApi _player) {
			return !Utilities.IsValid(_player) || _player.isLocal;
		}

		public override void OnPlayerTriggerEnter(VRCPlayerApi _player) {
			if (ValidatePlayer(_player)) _RelayEnter();
		}

		public override void OnPlayerTriggerExit(VRCPlayerApi _player) {
			if (ValidatePlayer(_player)) _RelayExit();
		}

		public override void OnPlayerCollisionEnter(VRCPlayerApi _player) {
			if (ValidatePlayer(_player)) _RelayEnter();
		}

		public override void OnPlayerCollisionExit(VRCPlayerApi _player) {
			if (ValidatePlayer(_player)) _RelayExit();
		}
	}
}
