using Synergiance.MediaPlayer.Diagnostics;
using UnityEngine;

namespace Synergiance.MediaPlayer {
	public class PlayerManager : DiagnosticBehaviour {
		[SerializeField] private PlaylistManager playlistManager;
		[SerializeField] private VideoManager videoManager;
		private VideoPlayer[] videoPlayers;
		private string[] videoPlayerNames;

		protected override string DebugName => "Player Manager";
		protected override string DebugColor => ColorToHtmlStringRGB(new Color(0.15f, 0.5f, 0.1f));

		void Start() {}

		public PlaylistManager GetPlaylistManager() { return playlistManager; }
		public VideoManager GetVideoManager() { return videoManager; }
	}
}
