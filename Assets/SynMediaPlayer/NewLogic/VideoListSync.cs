
using Synergiance.MediaPlayer.Diagnostics;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Synergiance.MediaPlayer {
	/// <summary>
	/// The Video List Sync contains a synced list of videos, and allows
	/// selective access to it.
	/// </summary>
	[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
	public class VideoListSync : DiagnosticBehaviour {
		[UdonSynced] private VRCUrl[] videos;

		private bool initialized;
		public int Length => videos != null ? videos.Length : -1;

		protected override string DebugName => "Video Queue";
		protected override string DebugColor => ColorToHtmlStringRGB(new Color(0.65f, 0.60f, 0.35f));

		/// <summary>
		/// Initializes the behaviour with the video array allocated to the
		/// size of <paramref name="_length"/>.
		/// </summary>
		/// <param name="_length">Number of videos this will be able to contain</param>
		public void _Initialize(int _length) {
			if (initialized) return;

			// TODO: Figure out how to tell if we're initializing the world

			if (videos != null && videos.Length > 0) {
				Log("Already received synced list");
				initialized = true;
				return;
			}

			if (_length <= 0) {
				LogError("Cannot initialize a negative or zero size array");
				return;
			}

			videos = new VRCUrl[_length];
			initialized = true;
			Sync();
		}

		public VRCUrl _GetVideo(int _idx) {
			if (!initialized) {
				LogError("Not initialized!");
				return VRCUrl.Empty;
			}
			if (_idx < 0 || _idx >= videos.Length) return VRCUrl.Empty;
			return videos[_idx];
		}

		public void _SetVideo(int _idx, VRCUrl _video) {
			if (!initialized) {
				LogError("Not initialized!");
				return;
			}
			videos[_idx] = _video;
			Sync();
		}

		private void Sync() {
			Log("Sync!");
			// TODO: Actually sync
		}
	}
}
