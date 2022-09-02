using UnityEngine;

namespace Synergiance.MediaPlayer {
	/// <summary>
	/// Video controller base class (Probably base class)
	/// </summary>
	public class VideoController : VideoBehaviour {
		[SerializeField] private PlayerManager playerManager;
		private bool initialized;

		void Start() {
			Initialize();
		}

		private void Initialize() {
			if (initialized) return;
			// Initialization stuff
			initialized = true;
		}

		public void _Play() {}
		public void _Pause() {}
		public void _Stop() {}
		public void _PlayPause() {}
		public void _PlayPauseStop() {}
	}
}
