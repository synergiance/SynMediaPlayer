﻿using System;
using Synergiance.MediaPlayer.Diagnostics;
using UnityEngine;
using VRC.SDK3.Components.Video;
using VRC.SDKBase;

namespace Synergiance.MediaPlayer {
	public enum VideoTypes {
		Video, Stream, LowLatency
	}
	public class VideoManager : DiagnosticBehaviour {
		[SerializeField] private VideoRelay[] relays;
		private string[] videoNames; // Name of the relay video players
		private int[] relayHandles; // Handle the relay is currently bound to
		private bool[] relayIsSecondary; // True when this is the next video instead of the current

		private VideoPlayer[] videoPlayers; // Video players assigned to each handle
		private int[] primaryHandles; // Primary relay assigned to handle
		private int[] secondaryHandles; // Secondary relay assigned to handle
		private VRCUrl[] primaryLinks; // Primary video link for handle
		private VRCUrl[] secondaryLinks; // Secondary video link for handle

		private VRCUrl[] videosToLoad; // Queue of videos to load
		private bool[] videosPlayImmediately; // Whether video in queue should play as soon as its loaded
		private int[] videoRelayHandles; // What relay to use to play the video

		// Public Callback Variables
		[HideInInspector] public int relayIdentifier;
		[HideInInspector] public VideoError relayVideoError;

		private bool initialized;
		private bool isValid;

		private const int MAX_QUEUE_LENGTH = 64;
		private int videosInQueue;
		private int firstVideoInQueue;

		protected override string DebugName => "Video Manager";
		protected override string DebugColor => ColorToHtmlStringRGB(new Color(0.65f, 0.5f, 0.1f));

		void Start() {
			Initialize();
		}

		private void Initialize() {
			if (initialized) return;
			InitialSetUp();
			initialized = true;
		}

		private void InitialSetUp() {
			if (relays == null || relays.Length < 1) {
				LogError("No relays set!");
				return;
			}

			videosToLoad = new VRCUrl[MAX_QUEUE_LENGTH];
			videosPlayImmediately = new bool[MAX_QUEUE_LENGTH];
			videoRelayHandles = new int[MAX_QUEUE_LENGTH];

			videoNames = new string[relays.Length];
			relayHandles = new int[relays.Length];
			relayIsSecondary = new bool[relays.Length];

			for (int i = 0; i < relays.Length; i++) {
				videoNames[i] = relays[i].InitializeRelay(this, i);
				relayHandles[i] = -1;
				relayIsSecondary[i] = false;
				if (videoNames[i] == null)
					LogWarning($"Video player {i} isn't initialized!");
				else
					Log($"Video player {i} ({videoNames[i]}) is now initialized!");
			}

			isValid = true;
		}

		/// <summary>
		/// Interface for binding a player to a new handle, so the video player
		/// can play and control videos.
		/// </summary>
		/// <param name="_player">The requesting video player, which will be
		/// saved to an array of players.</param>
		/// <returns>The handle for performing actions on a video.</returns>
		public int _RequestVideoHandle(VideoPlayer _player) {
			Initialize();

			// Check to see whether handles arrays have been initialized
			if (videoPlayers == null || videoPlayers.Length == 0) {
				Log("Creating handle arrays, registering video player at index 0");
				videoPlayers = new VideoPlayer[1];
				primaryHandles = new int[1];
				secondaryHandles = new int[1];
				primaryLinks = new VRCUrl[1];
				secondaryLinks = new VRCUrl[1];
				InsertPlayerIntoNewHandle(_player, 0);
				return 0;
			}

			// Search through video handles to see if player is already registered
			for (int i = 0; i < videoPlayers.Length; i++) {
				if (_player != videoPlayers[i]) continue;
				LogWarning($"Video player already registered at index {i}");
				return i;
			}

			// Expand arrays by 1 to accomodate a new video player
			int index = videoPlayers.Length;
			Log($"Registering video player at index {index}");
			VideoPlayer[] tempPlayers = new VideoPlayer[index + 1];
			Array.Copy(videoPlayers, tempPlayers, index);
			videoPlayers = tempPlayers;
			int[] tempHandles = new int[index + 1];
			Array.Copy(primaryHandles, tempHandles, index);
			primaryHandles = tempHandles;
			tempHandles = new int[index + 1];
			Array.Copy(secondaryHandles, tempHandles, index);
			secondaryHandles = tempHandles;
			InsertPlayerIntoNewHandle(_player, index);
			return index;
		}

		private void InsertPlayerIntoNewHandle(VideoPlayer _player, int _handle) {
			videoPlayers[_handle] = _player;
			primaryHandles[_handle] = -1;
			secondaryHandles[_handle] = -1;
			primaryLinks[_handle] = VRCUrl.Empty;
			secondaryLinks[_handle] = VRCUrl.Empty;
		}

		/// <summary>
		/// Interface for requesting to enter a new video. It will look for a
		/// free relay to use for the new video, or just use the current relay
		/// if compatible.
		/// </summary>
		/// <param name="_videoLink">URL for the video to load</param>
		/// <param name="_videoType">The type of video that will be playing (0
		/// for video, 1 for stream, 2 for low latency)</param>
		/// <param name="_handle">The handle to use for this action.</param>
		/// <param name="_playImmediately">Set true to have video play
		/// as soon as it loads.</param>
		/// <returns>On success, returns true. If the currently bound relay is
		/// incompatible and there is no free compatible relay, this will fail
		/// and return false.</returns>
		public bool _LoadVideo(VRCUrl _videoLink, int _videoType, int _handle, bool _playImmediately = false) {
			if (_handle < 0 || videoPlayers == null || _handle >= videoPlayers.Length) {
				LogError("Invalid handle!");
				return false;
			}
			int relay = primaryHandles[_handle];
			if (relay < 0) {
				Log("Handle unbound, searching for compatible relay");
				relay = GetAndBindCompatibleRelay(_videoType, _handle);
				if (relay < 0) return false;
			} else if (relays[relay].VideoType != _videoType) {
				Log("Video type mismatch, searching for compatible relay");
				UnbindRelay(relay);
				relay = GetAndBindCompatibleRelay(_videoType, _handle);
				if (relay < 0) return false;
			}

			relays[relay]._Stop();
			// TODO: Put this into a queue
			// TODO: Actually load the video with the video relay
			return true;
		}

		private void LoadVideoInternal(VRCUrl _link, bool _playImmediately, int _relay) {
			relays[_relay]._Load(_link, _playImmediately);
		}

		public bool _Play(int _handle) {
			return true;
		}

		public bool _Pause(int _handle) {
			return true;
		}

		public bool _Stop(int _handle) {
			return true;
		}

		public bool _SetTime(int _handle, float _time) {
			return true;
		}

		public float _GetTime(int _handle) {
			return 0;
		}

		private int GetAndBindCompatibleRelay(int _videoType, int _handle) {
			int relay = GetCompatibleRelay(_videoType);
			if (relay < 0) {
				LogError("No unbound compatible relays!");
				return -1;
			}
			Log($"Found unbound relay: {relay}");
			if (!BindRelayToHandle(_handle, relay)) {
				LogError("Unable to bind!");
				return -1;
			}
			return relay;
		}

		private int GetCompatibleRelay(int _videoType) {
			int relay = GetFirstUnboundRelay();
			while (relay >= 0) {
				if (relays[relay].VideoType == _videoType)
					return relay;
				if (++relay >= relays.Length) break;
				relay = GetFirstUnboundRelay(relay);
			}
			return -1;
		}

		private bool BindRelayToHandle(int _handle, int _relay, bool _secondary = false) {
			if (_relay < 0 || _relay >= relays.Length) {
				LogError("Relay out of bounds, cannot bind!");
				return false;
			}
			if (_handle < 0 || videoPlayers == null || _handle >= videoPlayers.Length) {
				LogError("Handle out of bounds, cannot bind!");
				return false;
			}
			if (relayHandles[_relay] >= 0) {
				LogError("Relay still bound!");
				return false;
			}
			if (_secondary) {
				if (secondaryHandles[_handle] >= 0) {
					LogError("Secondary handle still bound!");
					return false;
				}
			} else {
				if (primaryHandles[_handle] >= 0) {
					LogError("Primary handle still bound!");
					return false;
				}
			}

			// Actual bind
			if (_secondary) secondaryHandles[_handle] = _relay;
			else primaryHandles[_handle] = _relay;
			relayHandles[_relay] = _handle;
			relayIsSecondary[_relay] = _secondary;
			Log($"Successfully bound relay {_relay} to{(_secondary ? " secondary" : "")} handle {_handle}");
			return true;
		}

		private int GetFirstUnboundRelay(int _startOffset = 0) {
			Log($"Getting first unbound relay{(_startOffset != 0 ? $" starting at {_startOffset}" : "")}");
			if (_startOffset >= relayHandles.Length) {
				LogError("Start offset out of bounds!");
				return -1;
			}
			for (int i = _startOffset; i < relayHandles.Length; i++) {
				if (relayHandles[i] >= 0) continue;
				Log($"Found unbound relay at index {i}");
				return i;
			}
			Log("Found no unbound relay");
			return -1;
		}

		private void UnbindRelay(int _relay) {
			int handle = relayHandles[_relay];
			if (handle < 0) {
				LogWarning("Relay was not bound!");
				return;
			}

			relayHandles[_relay] = -1;
			if (relayIsSecondary[_relay])
				secondaryHandles[handle] = -1;
			else
				primaryHandles[handle] = -1;
			relayIsSecondary[_relay] = false;
			// TODO: Stop video
		}

		private void SwapRelayToPrimary(int _handle) {
			//
		}

		private void ProcessRelayEvent(string _eventName) {
			//
		}

		// Relay callbacks
		public void _RelayVideoEnd() { ProcessRelayEvent("_RelayVideoEnd"); }
		public void _RelayVideoReady() { ProcessRelayEvent("_RelayVideoReady"); }
		public void _RelayVideoError() { ProcessRelayEvent("_RelayVideoError"); }
		public void _RelayVideoPlay() { ProcessRelayEvent("_RelayVideoPlay"); }
		public void _RelayVideoStart() { ProcessRelayEvent("_RelayVideoStart"); }
		public void _RelayVideoLoop() { ProcessRelayEvent("_RelayVideoLoop"); }
		public void _RelayVideoPause() { ProcessRelayEvent("_RelayVideoPause"); }
	}
}