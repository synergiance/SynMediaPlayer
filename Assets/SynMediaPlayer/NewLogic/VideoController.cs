using UnityEngine;

namespace Synergiance.MediaPlayer {
	/// <summary>
	/// Video controller base class (Probably base class)
	/// </summary>
	public class VideoController : VideoBehaviour {
		[SerializeField] private PlayerManager playerManager;
		private VideoDisplay display;
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
			if (playerManager == null) {
				LogError("Player Manager missing!");
				return;
			}

			// TODO: Register with player manager

			isValid = true;
		}

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
			return true;
		}

		public void _Play() {}
		public void _Pause() {}
		public void _Stop() {}
		public void _PlayPause() {}
		public void _PlayPauseStop() {}
	}
}
