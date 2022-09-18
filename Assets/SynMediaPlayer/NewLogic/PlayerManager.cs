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
		private int[][] videoControllerBinds;
		private string[] videoControllerPreferred;
		private VideoController[] videoControllers;

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
			Log("Initialize!");
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

			Log("Successfully validated!");

			isValid = true;
		}

		public PlaylistManager GetPlaylistManager() { return playlistManager; }
		public VideoManager GetVideoManager() { return videoManager; }

		/// <summary>
		/// Registers a video player with the player manager.
		/// </summary>
		/// <param name="_videoPlayer">The video player we'd like to register</param>
		/// <param name="_name">The name of the video player.</param>
		/// <returns>The ID we give to the video player, -1 on error</returns>
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

			Log($"Registering video player \"{_name}\"");

			if (videoPlayers == null || videoPlayers.Length == 0) {
				videoPlayers = new VideoPlayer[1];
				videoPlayerNames = new string[1];
				videoControllerBinds = new int[1][];
			} else {
				VideoPlayer[] temp = new VideoPlayer[videoPlayers.Length + 1];
				string[] tempStr = new string[temp.Length];
				int[][] tempBinds = new int[temp.Length][];
				Array.Copy(videoPlayers, temp, videoPlayers.Length);
				Array.Copy(videoPlayerNames, tempStr, videoPlayers.Length);
				Array.Copy(videoControllerBinds, tempBinds, videoPlayers.Length);
				videoPlayers = temp;
				videoPlayerNames = tempStr;
				videoControllerBinds = tempBinds;
			}

			videoManager._ResizeVideoPlayerArray();

			int videoPlayerId = videoPlayers.Length - 1;
			videoPlayers[videoPlayerId] = _videoPlayer;
			videoPlayerNames[videoPlayerId] = _name;
			videoControllerBinds[videoPlayerId] = new int[0];
			return videoPlayerId;
		}

		/// <summary>
		/// Registers a controller with the player manager.
		/// </summary>
		/// <param name="_controller">Controller to register</param>
		/// <param name="_defaultSource">Name of the default source</param>
		/// <returns>The ID we assign to the controller</returns>
		public int _RegisterVideoController(VideoController _controller, string _defaultSource) {
			if (_controller == null) {
				LogError("No registering null video controller!");
				return -1;
			}

			int numControllers = videoControllers == null ? 0 : videoControllers.Length;

			for (int i = 0; i < numControllers; i++) {
				// ReSharper disable once PossibleNullReferenceException
				if (_controller != videoControllers[i]) continue;
				LogWarning("Already registered this controller!");
				return i;
			}

			Log("Expanding arrays");
			if (numControllers == 0) {
				videoControllers = new VideoController[1];
				videoControllerPreferred = new string[1];
			} else {
				VideoController[] tmpControllers = new VideoController[numControllers + 1];
				string[] tmpDefaults = new string[numControllers + 1];
				// ReSharper disable once AssignNullToNotNullAttribute
				Array.Copy(videoControllers, tmpControllers, numControllers);
				Array.Copy(videoControllerPreferred, tmpDefaults, numControllers);
				videoControllers = tmpControllers;
				videoControllerPreferred = tmpDefaults;
			}

			Log("Registering controller with ID " + numControllers);
			videoControllers[numControllers] = _controller;
			videoControllerPreferred[numControllers] = _defaultSource;

			Log("Searching for default source ID");
			for (int i = 0; i < videoPlayerNames.Length; i++) {
				if (!string.Equals(_defaultSource, videoPlayerNames[i])) continue;
				Log("Found default source ID for controller " + numControllers);
				_controller._SetDefaultSourceId(i);
				break;
			}

			return numControllers;
		}

		private bool ValidateId(int _id) {
			if (isValid && _id >= 0 && videoPlayers != null && _id < videoPlayers.Length) return true;
			LogWarning("Invalid video player ID!");
			return false;
		}

		/// <summary>
		/// Relay an event to the specified video player
		/// </summary>
		/// <param name="_id">ID of the video player we'd like to relay the event to.</param>
		/// <param name="_event">Event we'd like to send to the video player</param>
		/// <returns>True if successful</returns>
		public bool _RelayEvent(int _id, string _event) {
			Log($"Received video event \"{_event}\" for video player {_id}");
			if (!ValidateId(_id)) return false;
			videoPlayers[_id].SendCustomEvent(_event);
			return true;
		}

		/// <summary>
		/// Relay a video player error to the specified video player
		/// </summary>
		/// <param name="_id">ID of the video player we'd like to relay the event to.</param>
		/// <param name="_error">The video error that occurred</param>
		/// <returns>True if successful</returns>
		public bool _RelayError(int _id, VideoError _error) {
			Log($"Received video error {_error} for video player {_id}");
			if (!ValidateId(_id)) return false;
			videoPlayers[_id]._RelayVideoError(_error);
			return true;
		}
	}
}
