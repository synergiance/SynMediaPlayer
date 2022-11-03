using System;
using Synergiance.MediaPlayer.Diagnostics;
using UnityEngine;
using VRC.SDK3.Components.Video;
using VRC.SDKBase;

namespace Synergiance.MediaPlayer {
	[DefaultExecutionOrder(-20)]
	public class PlayerManager : DiagnosticBehaviour {
		[SerializeField] private PlaylistManager playlistManager;
		[SerializeField] private VideoManager videoManager;
		private VideoPlayer[] videoPlayers;
		private string[] videoPlayerNames;
		private int[][] playerControllerBinds;
		private int[] controllerPlayerBinds;
		private string[] videoControllerPreferred;
		private VideoController[] videoControllers;

		// Sync timing
		private const float CheckCooldown = 0.1f;
		private float lastCheck = -CheckCooldown;

		public int NumVideoPlayers => hasVideoPlayers ? videoPlayers.Length : 0;

		protected override string DebugName => "Player Manager";
		protected override string DebugColor => ColorToHtmlStringRGB(new Color(0.15f, 0.5f, 0.1f));

		private bool initialized;
		private bool isValid;

		private bool hasVideoPlayers;

		void Start() {
			Initialize();
		}

		// ReSharper disable Unity.PerformanceAnalysis
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

		public string GetVideoPlayerName(int _id) {
			if (!ValidateId(_id)) return null;
			return videoPlayerNames[_id];
		}

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

			if (hasVideoPlayers) {
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

			if (!hasVideoPlayers || videoPlayers.Length == 0) {
				Log("Creating video player arrays");
				videoPlayers = new VideoPlayer[1];
				videoPlayerNames = new string[1];
				playerControllerBinds = new int[1][];
				hasVideoPlayers = true;
			} else {
				Log("Expanding video player arrays");
				VideoPlayer[] temp = new VideoPlayer[videoPlayers.Length + 1];
				string[] tempStr = new string[temp.Length];
				int[][] tempBinds = new int[temp.Length][];
				Array.Copy(videoPlayers, temp, videoPlayers.Length);
				Array.Copy(videoPlayerNames, tempStr, videoPlayers.Length);
				Array.Copy(playerControllerBinds, tempBinds, videoPlayers.Length);
				videoPlayers = temp;
				videoPlayerNames = tempStr;
				playerControllerBinds = tempBinds;
			}

			int videoPlayerId = videoPlayers.Length - 1;
			Log("Adding video player at index " + videoPlayerId);
			videoPlayers[videoPlayerId] = _videoPlayer;
			videoPlayerNames[videoPlayerId] = _name;
			playerControllerBinds[videoPlayerId] = null;

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
				controllerPlayerBinds = new int[1];
			} else {
				VideoController[] tmpControllers = new VideoController[numControllers + 1];
				string[] tmpDefaults = new string[numControllers + 1];
				int[] tmpBinds = new int[numControllers + 1];
				// ReSharper disable once AssignNullToNotNullAttribute
				Array.Copy(videoControllers, tmpControllers, numControllers);
				Array.Copy(videoControllerPreferred, tmpDefaults, numControllers);
				Array.Copy(controllerPlayerBinds, tmpBinds, numControllers);
				videoControllers = tmpControllers;
				videoControllerPreferred = tmpDefaults;
				controllerPlayerBinds = tmpBinds;
			}

			Log("Registering controller with ID " + numControllers);
			videoControllers[numControllers] = _controller;
			videoControllerPreferred[numControllers] = _defaultSource;
			controllerPlayerBinds[numControllers] = -1;

			Log("Searching for default source ID");
			for (int i = 0; i < videoPlayerNames.Length; i++) {
				if (!string.Equals(_defaultSource, videoPlayerNames[i])) continue;
				Log("Found default source ID for controller " + numControllers);
				_controller._SetDefaultSourceId(i);
				break;
			}

			return numControllers;
		}

		public bool _SwitchControllerSource(int _newSource, int _id) {
			Initialize();
			if (!isValid || videoControllers == null || _id < 0 || _id >= videoControllers.Length) {
				LogWarning("Invalid controller id");
				return false;
			}

			if (!ValidateId(_newSource)) return false;

			int oldSource = controllerPlayerBinds[_id];

			if (_newSource == oldSource) {
				LogWarning("Already using this source!");
				return true;
			}

			Log($"Switching source on controller {_id} from {oldSource} to {_newSource}");

			if (oldSource >= 0) for (int i = 0; i < playerControllerBinds[oldSource].Length; i++) {
				if (playerControllerBinds[oldSource][i] != _id) continue;
				Log($"Removing controller {_id} from player controller binds at {oldSource},{i}");
				playerControllerBinds[oldSource][i] = -1;
				break;
			}

			controllerPlayerBinds[_id] = _newSource;

			int numBinds = playerControllerBinds[_newSource] == null ? 0 : playerControllerBinds.Length;
			for (int i = 0; i < numBinds; i++) {
				if (playerControllerBinds[_newSource][i] >= 0) continue;
				Log($"Adding controller {_id} to player controller binds at {_newSource},{i}");
				playerControllerBinds[_newSource][i] = _id;
				return true;
			}

			Log($"Expanding player controller binds array at {_newSource}");
			int[] tmpBinds = new int[numBinds + 1];
			if (numBinds > 0) Array.Copy(playerControllerBinds[_newSource], tmpBinds, numBinds);
			Log($"Adding controller {_id} to player controller binds at {_newSource},{numBinds}");
			tmpBinds[numBinds] = _id;
			playerControllerBinds[_newSource] = tmpBinds;

			return true;
		}

		// ReSharper disable Unity.PerformanceAnalysis
		private bool ValidateId(int _id) {
			Initialize();
			if (isValid && hasVideoPlayers && _id >= 0 && _id < videoPlayers.Length) return true;
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

		private void Update() {
			if (!isValid || !hasVideoPlayers) return;
			if (lastCheck + CheckCooldown > Time.time) return;
			Log("Checking video players");
			foreach (VideoPlayer videoPlayer in videoPlayers)
				videoPlayer._UpdateSync();
		}

		/// <summary>
		/// Relay a video player error to the specified video player
		/// </summary>
		/// <param name="_id">ID of the video player we'd like to relay the event to.</param>
		/// <param name="_error">The video error that occurred</param>
		/// <returns>True if successful</returns>
		public bool _RelayError(int _id, MediaError _error) {
			Log($"Received video error {_error} for video player {_id}");
			if (!ValidateId(_id)) return false;
			videoPlayers[_id]._RelayVideoError(_error);
			return true;
		}
	}
}
