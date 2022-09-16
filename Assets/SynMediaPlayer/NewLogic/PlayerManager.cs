using System;
using Synergiance.MediaPlayer.Diagnostics;
using UnityEngine;
using VRC.SDK3.Components.Video;

namespace Synergiance.MediaPlayer {
	[DefaultExecutionOrder(-20)]
	public class PlayerManager : DiagnosticBehaviour {
		[SerializeField] private PlaylistManager playlistManager;
		[SerializeField] private VideoManager videoManager;
		private VideoPlayer[] videoPlayers;
		private string[] videoPlayerNames;

		public int NumVideoPlayers => videoPlayers != null ? videoPlayers.Length : 0;

		protected override string DebugName => "Player Manager";
		protected override string DebugColor => ColorToHtmlStringRGB(new Color(0.15f, 0.5f, 0.1f));

		private bool initialized;
		private bool isValid;

		void Start() {
			Initialize();
		}

		private void Initialize() {
			if (initialized) return;
			CheckValid();
			initialized = true;
		}

		private void CheckValid() {
			if (videoManager == null) {
				LogError("Video Manager missing!");
				return;
			}

			if (playlistManager == null) {
				LogError("Playlist Manager Missing!");
				return;
			}

			isValid = true;
		}

		public PlaylistManager GetPlaylistManager() { return playlistManager; }
		public VideoManager GetVideoManager() { return videoManager; }

		public int _RegisterVideoPlayer(VideoPlayer _videoPlayer, string _name) {
			Initialize();
			if (!isValid) {
				LogError("Invalid player manager!");
				return -1;
			}

			if (string.IsNullOrWhiteSpace(_name)) {
				LogError("Must specify a name to register!");
				return -1;
			}

			if (_videoPlayer == null) {
				LogError("Cannot register null video player!");
				return -1;
			}

			if (videoPlayers != null) {
				int foundIndex = -1;
				for (int i = 0; i < videoPlayers.Length; i++) {
					if (videoPlayers[i] == _videoPlayer) {
						LogWarning("Already registered this video player!");
						foundIndex = i;
						continue;
					}

					if (string.Equals(_name, videoPlayerNames[i])) {
						LogError("Duplicate name found!");
						return -1;
					}
				}
				if (foundIndex >= 0)
					return foundIndex;
			}

			if (videoPlayers == null || videoPlayers.Length == 0) {
				videoPlayers = new VideoPlayer[1];
				videoPlayerNames = new string[1];
			} else {
				VideoPlayer[] temp = new VideoPlayer[videoPlayers.Length + 1];
				string[] tempStr = new string[temp.Length];
				Array.Copy(videoPlayers, temp, videoPlayers.Length);
				Array.Copy(videoPlayerNames, tempStr, videoPlayers.Length);
				videoPlayers = temp;
				videoPlayerNames = tempStr;
			}

			videoManager._ResizeVideoPlayerArray();

			int videoPlayerId = videoPlayers.Length - 1;
			videoPlayers[videoPlayerId] = _videoPlayer;
			videoPlayerNames[videoPlayerId] = _name;
			return videoPlayerId;
		}

		private bool ValidateId(int _id) {
			if (isValid && _id >= 0 && videoPlayers != null && _id < videoPlayers.Length) return true;
			LogWarning("Invalid video player ID!");
			return false;
		}

		public bool _RelayEvent(int _id, string _event) {
			Log($"Received video event \"{_event}\" for video player {_id}");
			if (!ValidateId(_id)) return false;
			videoPlayers[_id].SendCustomEvent(_event);
			return true;
		}

		public bool _RelayError(int _id, VideoError _error) {
			Log($"Received video error {_error} for video player {_id}");
			if (!ValidateId(_id)) return false;
			videoPlayers[_id]._RelayVideoError(_error);
			return true;
		}
	}
}
