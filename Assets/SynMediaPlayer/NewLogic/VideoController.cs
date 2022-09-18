using UnityEngine;

namespace Synergiance.MediaPlayer {
	/// <summary>
	/// Video controller base class (Probably base class)
	/// </summary>
	public class VideoController : VideoBehaviour {
		[SerializeField] private PlayerManager playerManager;
		[SerializeField] private string defaultPlayer;
		[SerializeField] private bool disableSwitchingSource;
		private VideoDisplay display;
		private bool displayLinked;
		private bool settingDisplay;
		private int defaultId = -1;
		private int currentId = -1;
		private int identifier;

		private bool initialized;
		private bool isValid;

		protected override string DebugName => "Video Controller";
		protected override string DebugColor => ColorToHtmlStringRGB(new Color(0.15f, 0.45f, 0.65f));

		void Start() {
			Initialize();
		}

		private void Initialize() {
			if (initialized) return;
			Log("Initialize!");
			SetUp();
			initialized = true;
		}

		private void SetUp() {
			if (isValid) {
				LogWarning("Breaking set up feedback loop!");
				return;
			}

			if (playerManager == null) {
				LogError("Player Manager missing!");
				return;
			}

			Log("Registering with video controller");
			identifier = playerManager._RegisterVideoController(this, defaultPlayer);
			if (identifier < 0) {
				LogError("Registration unsuccessful!");
				return;
			}

			if (defaultId >= 0) SwitchSourceInternal(defaultId);

			isValid = true;
		}

		/// <summary>
		/// Links a display to this controller
		/// </summary>
		/// <param name="_display">Display to link</param>
		/// <returns></returns>
		public bool _LinkDisplay(VideoDisplay _display) {
			if (_display == null) {
				LogError("Cannot link null display!");
				return false;
			}

			if (display != null) {
				LogError("Controller already linked!");
				return false;
			}

			Log("Linking display");
			display = _display;
			displayLinked = true;
			return true;
		}

		/// <summary>
		/// Callback to receive the ID of the default source, when found
		/// </summary>
		/// <param name="_id">ID of the default source</param>
		public void _SetDefaultSourceId(int _id) {
			if (_id < 0) {
				LogError("Invalid source!");
				return;
			}

			Log($"Received ID ({_id}) of default source");
			defaultId = _id;

			if (!initialized) return;

			SwitchSourceInternal(defaultId);
		}

		/// <summary>
		/// Switch to a different video player source
		/// </summary>
		/// <param name="_source">ID of the video player we want to switch to</param>
		/// <returns>True on success</returns>
		public bool _SwitchSource(int _source) {
			Initialize();

			if (settingDisplay) {
				Log("Breaking feedback loop");
				return false;
			}

			if (disableSwitchingSource) {
				LogWarning("Source switching disabled!");
				return false;
			}

			if (settingDisplay) {
				Log("Breaking feedback loop");
				return false;
			}

			if (displayLinked) {
				settingDisplay = true;
				display._SwitchSource(_source);
				settingDisplay = false;
			}

			return SwitchSourceInternal(_source);
		}

		private bool SwitchSourceInternal(int _source) {
			if (!isValid) {
				LogError("Controller is invalid!");
				return false;
			}

			if (_source == currentId) {
				LogWarning("Source already selected!");
				return true;
			}

			Log("Switching source to " + _source);

			if (!playerManager._SwitchControllerSource(_source, identifier)) {
				Log("Failed to bind!");
				return false;
			}

			Log("Successfully bound to " + _source);
			currentId = _source;

			return true;
		}

		public void _Play() {}
		public void _Pause() {}
		public void _Stop() {}
		public void _PlayPause() {}
		public void _PlayPauseStop() {}
	}
}
