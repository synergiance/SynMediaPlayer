using System;
using Synergiance.MediaPlayer.Diagnostics;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components.Video;
using VRC.SDKBase;

namespace Synergiance.MediaPlayer {
	public enum VideoTypes {
		Video, Stream, LowLatency
	}
	[DefaultExecutionOrder(-30), UdonBehaviourSyncMode(BehaviourSyncMode.None)]
	public class VideoManager : DiagnosticBehaviour {
		// Settings
		[Range(1, 10)] [SerializeField] private int loadAttempts = 3; // Number of attempts at loading a video

		// Relay cache
		[SerializeField] private VideoRelay[] relays;
		private string[] videoNames; // Name of the relay video players
		private int[] relayHandles; // Handle the relay is currently bound to
		private bool[] relayIsSecondary; // True when this is the next video instead of the current

		// Video player handles
		[SerializeField] private PlayerManager playerManager; // Handle for video player manager, which drives our videos
		private int[] primaryHandles; // Primary relay assigned to handle
		private int[] secondaryHandles; // Secondary relay assigned to handle
		private float[] videoPlayerVolumes; // Volume the video player is set to

		// Misc handles
		[SerializeField] private DisplayManager displayManager; // Handle for display manager, which displays our videos
		[SerializeField] private UdonSharpBehaviour audioLink; // Reference to AudioLink to switch the audio source

		// Video links
		private VRCUrl[] primaryLinks; // Primary video link for handle
		private VRCUrl[] secondaryLinks; // Secondary video link for handle
		private int[] primaryVideoTypes; // Type of primary videos
		private int[] secondaryVideoTypes; // Type of secondary videos
		private int[] primaryLoadAttempts; // Number of load attempts for current primary video
		private int[] secondaryLoadAttempts; // Number of load attempts for current secondary video
		private float[] primaryVideoDurations; // Cache for the video duration of the primary video
		private float[] secondaryVideoDurations; // Cache for the video duration of the secondary video

		// Cross video tracking
		private float[] videoCrossFadeLengths; // Length of cross fade between videos
		private float[] videoCrossFadeBegin; // Begin time for each cross fade

		// Video Reference Points
		private float[] videoStartTimes; // Time video should have started
		private float[] nextVideoStartTimes; // Time the next video should start
		//private float[] videosPointA; // For AB Looping
		//private float[] videosPointB; // For AB Looping

		// Video states
		private bool[] isPlaying;

		// Video load queue
		private VRCUrl[] videosToLoad; // Queue of videos to load
		private bool[] videosPlayImmediately; // Whether video in queue should play as soon as its loaded
		private int[] videoRelayHandles; // What relay to use to play the video

		private const float VIDEO_LOAD_COOLDOWN = 5.25f;
		private float lastVideoLoadAttempt;

		private const int MAX_QUEUE_LENGTH = 64;
		private int videosInQueue;
		private int firstVideoInQueue;

		// Sync timing
		private const float PLAYER_CHECK_COOLDOWN = 0.1f;
		private float lastPlayerCheck = -PLAYER_CHECK_COOLDOWN;

		// Cache for untrusted check
		public bool BlockingUntrustedLinks { get; private set; }

		// Public Callback Variables
		[HideInInspector] public int relayIdentifier;
		[HideInInspector] public VideoError relayVideoError;

		// Behaviour integrity
		private bool initialized;
		private bool isValid;
		private bool hasPlayerHandles;

		// Diagnostic settings
		protected override string DebugName => "Video Manager";
		protected override string DebugColor => ColorToHtmlStringRGB(new Color(0.65f, 0.5f, 0.1f));

		void Start() {
			Initialize();
		}

		private void Initialize() {
			if (initialized) return;
			Log("Initialize!");
			InitialSetUp();
			initialized = true;
		}

		private void InitialSetUp() {
			if (relays == null || relays.Length < 1) {
				LogError("No relays set!");
				return;
			}

			if (displayManager == null) {
				LogError("Display manager missing!");
				return;
			}

			Log("Initial Set Up");

			displayManager._Initialize(this);

			videosToLoad = new VRCUrl[MAX_QUEUE_LENGTH];
			videosPlayImmediately = new bool[MAX_QUEUE_LENGTH];
			videoRelayHandles = new int[MAX_QUEUE_LENGTH];

			videoNames = new string[relays.Length];
			relayHandles = new int[relays.Length];
			relayIsSecondary = new bool[relays.Length];

			Log("Initializing video relays");
			for (int i = 0; i < relays.Length; i++) {
				videoNames[i] = relays[i].InitializeRelay(this, i);
				relayHandles[i] = -1;
				relayIsSecondary[i] = false;
				if (videoNames[i] == null)
					LogWarning($"Video player {i} isn't initialized!");
				else
					Log($"Video player {i} ({videoNames[i]}) is now initialized!");
			}

			if (loadAttempts < 1) loadAttempts = 1;
			if (loadAttempts > 10) loadAttempts = 10;

			isValid = true;
		}

		/// <summary>
		/// Interface for creating new bindings to match up with the player
		/// manager.
		/// </summary>
		/// <returns>True on success, false if anything's wrong.</returns>
		public bool _ResizeVideoPlayerArray() {
			Initialize();
			if (!isValid) {
				LogError("Invalid!");
				return false;
			}

			int prevLength = primaryHandles != null ? primaryHandles.Length : 0;
			int newLength = playerManager.NumVideoPlayers;
			Log($"Resizing video player handle references from {prevLength} to {newLength}");

			if (newLength <= prevLength) {
				LogError("This should never appear. Video player list will always expand.");
				return false;
			}

			// Check to see whether handles arrays have been initialized
			if (prevLength == 0) {
				Log("Creating handle arrays of size " + newLength);
				primaryHandles = new int[newLength];
				secondaryHandles = new int[newLength];
				primaryLinks = new VRCUrl[newLength];
				secondaryLinks = new VRCUrl[newLength];
				primaryLoadAttempts = new int[newLength];
				secondaryLoadAttempts = new int[newLength];
				primaryVideoTypes = new int[newLength];
				secondaryVideoTypes = new int[newLength];
				primaryVideoDurations = new float[newLength];
				secondaryVideoDurations = new float[newLength];
				hasPlayerHandles = true;
			} else {
				Log($"Expanding handle arrays from {prevLength} to {newLength}");
				ExpandIntArray(ref primaryHandles, newLength);
				ExpandIntArray(ref secondaryHandles, newLength);
				ExpandIntArray(ref primaryLoadAttempts, newLength);
				ExpandIntArray(ref secondaryLoadAttempts, newLength);
				ExpandIntArray(ref primaryVideoTypes, newLength);
				ExpandIntArray(ref secondaryVideoTypes, newLength);
				ExpandLinkArray(ref primaryLinks, newLength);
				ExpandLinkArray(ref secondaryLinks, newLength);
				ExpandFloatArray(ref primaryVideoDurations, newLength);
				ExpandFloatArray(ref secondaryVideoDurations, newLength);
			}

			Log("Initializing new handles");
			for (int i = prevLength; i < newLength; i++) {
				primaryHandles[i] = -1;
				secondaryHandles[i] = -1;
				primaryLinks[i] = VRCUrl.Empty;
				secondaryLinks[i] = VRCUrl.Empty;
				primaryLoadAttempts[i] = -1;
				secondaryLoadAttempts[i] = -1;
				primaryVideoTypes[i] = -1;
				secondaryVideoTypes[i] = -1;
				primaryVideoDurations[i] = -1;
				secondaryVideoDurations[i] = -1;
				string playerName = playerManager.GetVideoPlayerName(i);
				if (string.IsNullOrWhiteSpace(playerName))
					LogWarning("Video player name is null!");
				Log($"Adding video player {playerName} to display manager");
				displayManager._AddSource(playerName);
			}

			return true;
		}

		private void ExpandIntArray(ref int[] _array, int _newLength) {
			int[] tmpArray = new int[_newLength];
			Array.Copy(_array, tmpArray, _array.Length);
			_array = tmpArray;
		}

		private void ExpandFloatArray(ref float[] _array, int _newLength) {
			float[] tmpArray = new float[_newLength];
			Array.Copy(_array, tmpArray, _array.Length);
			_array = tmpArray;
		}

		private void ExpandLinkArray(ref VRCUrl[] _array, int _newLength) {
			VRCUrl[] tmpArray = new VRCUrl[_newLength];
			Array.Copy(_array, tmpArray, _array.Length);
			_array = tmpArray;
		}

		private void Update() {
			if (!isValid) return;
			if (!Networking.IsNetworkSettled) return;
			CheckPlayers();
		}

		private void CheckPlayers() {
			if (!hasPlayerHandles) return;
			if (lastPlayerCheck + PLAYER_CHECK_COOLDOWN > Time.time) return;
			for (int i = 0; i < primaryHandles.Length; i++) {
				if (primaryHandles[i] < 0) continue;
				//
			}
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
			if (_handle < 0 || primaryHandles == null || _handle >= primaryHandles.Length) {
				LogError("Invalid handle!");
				return false;
			}

			int videoType = _videoType;
			if (videoType < 0 || videoType > 2) {
				LogWarning("Invalid video type! Defaulting to video");
				videoType = 0;
			}
			
			Log($"Load video of type {_videoType} for handle {_handle} and link \"{_videoLink}\" {(_playImmediately ? " to play immediately" : "")}");

			primaryLinks[_handle] = _videoLink;
			primaryVideoTypes[_handle] = videoType;

			int relay = GetOrBindRelayAtHandle(_handle);
			// TODO: If no valid relay just make sure to attempt to bind again
			if (relay < 0) return false;

			relays[relay]._Stop();
			QueueVideo(_videoLink, _playImmediately, relay);
			if (videosInQueue == 1) AttemptLoadNext();
			return true;
		}

		public bool _LoadNextVideo(VRCUrl _videoLink, int _videoType, int _handle) {
			if (_handle < 0 || primaryHandles == null || _handle >= primaryHandles.Length) {
				LogError("Invalid handle!");
				return false;
			}

			int videoType = _videoType;
			if (videoType < 0 || videoType > 2) {
				LogWarning("Invalid video type! Defaulting to video");
				videoType = 0;
			}

			secondaryLinks[_handle] = _videoLink;
			secondaryVideoTypes[_handle] = videoType;
			// TODO: Queue up a video
			return true;
		}

		private void QueueVideo(VRCUrl _link, bool _playImmediately, int _relay) {
			if (_link == null) {
				LogError("Link cannot be null!");
				return;
			}
			if (videosInQueue >= MAX_QUEUE_LENGTH) {
				LogError($"Cannot queue video \"{_link}\"!");
				return;
			}
			if (_relay < 0 || _relay >= relays.Length) {
				LogError("Relay index out of bounds!");
				return;
			}

			int pushSlot = (firstVideoInQueue + videosInQueue++) % MAX_QUEUE_LENGTH;
			videosToLoad[pushSlot] = _link;
			videosPlayImmediately[pushSlot] = _playImmediately;
			videoRelayHandles[pushSlot] = _relay;
			Log($"Video loaded into slot {pushSlot}, there are now {videosInQueue} videos in the queue.");
		}

		private void AttemptLoadNext() {
			Log("Attempting to load next video");
			if (lastVideoLoadAttempt + VIDEO_LOAD_COOLDOWN > Time.time + float.Epsilon) {
				LogWarning("Cooldown not reached!");
				return;
			}
			if (videosInQueue <= 0) {
				LogError("No videos to load!");
				return;
			}
			// Pop from the queue
			VRCUrl videoLink = videosToLoad[firstVideoInQueue];
			bool playImmediately = videosPlayImmediately[firstVideoInQueue];
			int relayHandle = videoRelayHandles[firstVideoInQueue];
			firstVideoInQueue = (firstVideoInQueue + 1) % MAX_QUEUE_LENGTH;
			videosInQueue--;
			Log($"{(playImmediately ? "Playing" : "Loading")} video \"{videoLink}\" with relay {relayHandle}");
			LoadVideoInternal(videoLink, playImmediately, relayHandle);
			if (videosInQueue < 1) {
				Log("No more videos in queue");
				return;
			}
			Log($"{videosInQueue} more video{(videosInQueue == 1 ? "" : "s")} in queue");
			SendCustomEventDelayedSeconds("AttemptLoadNext", VIDEO_LOAD_COOLDOWN - float.Epsilon);
		}

		private void LoadVideoInternal(VRCUrl _link, bool _playImmediately, int _relay) {
			relays[_relay]._Load(_link, _playImmediately);
			lastVideoLoadAttempt = Time.time;
		}

		/// <summary>
		/// Play a given video player
		/// </summary>
		/// <param name="_handle">Handle of the video player associated with
		///     the video.</param>
		/// <returns>True on success.</returns>
		public bool _Play(int _handle) {
			int relay = GetPrimaryRelayAtHandle(_handle);
			if (relay < 0) return false;
			Log($"Playing video relay {relay} using handle {_handle}");
			return relays[relay]._Play();
		}

		public bool _PlayNext(int _handle) {
			int relay = GetSecondaryRelayAtHandle(_handle);
			if (relay < 0) return false;
			Log($"Playing next video relay {relay} using handle {_handle}");
			SwapRelayToPrimary(_handle);
			return relays[relay]._Play();
		}

		/// <summary>
		/// Pause a given video player
		/// </summary>
		/// <param name="_handle">Handle of the video player associated with
		///     the video.</param>
		/// <returns>True on success.</returns>
		public bool _Pause(int _handle) {
			int relay = GetPrimaryRelayAtHandle(_handle);
			if (relay < 0) return false;
			Log($"Pausing video relay {relay} using handle {_handle}");
			return relays[relay]._Pause();
		}

		/// <summary>
		/// Stop a given video player
		/// </summary>
		/// <param name="_handle">Handle of the video player associated with
		///     the video.</param>
		/// <returns>True on success.</returns>
		public bool _Stop(int _handle) {
			int relay = GetPrimaryRelayAtHandle(_handle);
			if (relay < 0) return false;
			Log($"Stopping video relay {relay} using handle {_handle}");
			return relays[relay]._Stop();
		}

		public bool _SetTime(int _handle, float _time) {
			int relay = GetPrimaryRelayAtHandle(_handle);
			if (relay < 0) return false;
			Log($"Setting time to {_time} on video relay {relay} using handle {_handle}");
			relays[relay].Time = _time;
			return true;
		}

		public float _GetTime(int _handle) {
			int relay = GetPrimaryRelayAtHandle(_handle);
			if (relay < 0) return -1;
			Log($"Getting time from video relay {relay} using handle {_handle}");
			return relays[relay].Time;
		}

		/// <summary>
		/// Gets the duration of the current or next video for a given video
		/// player
		/// </summary>
		/// <param name="_handle">Video player to use</param>
		/// <param name="_secondary">Use this if you want the secondary video duration</param>
		/// <returns>Duration of the video, or -1 if there's a problem</returns>
		public float _GetDuration(int _handle, bool _secondary = false) {
			int relay = _secondary ? GetSecondaryRelayAtHandle(_handle) : GetPrimaryRelayAtHandle(_handle);
			if (relay < 0) return -1;
			Log($"Getting duration from video relay {relay} using handle {_handle}");
			return relays[relay].Duration;
		}

		/// <summary>
		/// This is called when there is a change to audio on the display side.
		/// The video manager will grab the new updated audio template if the
		/// video player is bound and update all relevant relays.
		/// </summary>
		/// <param name="_handle">ID of the video player that's getting updated</param>
		public void _GetUpdatedAudioTemplate(int _handle) {
			if (!isValid) {
				LogError("Not Initialized!");
				return;
			}

			if (_handle < 0 || _handle >= primaryHandles.Length) {
				LogError("Video player at that ID does not exist!");
				return;
			}

			int relay = GetPrimaryRelayAtHandle(_handle);
			if (relay < 0) {
				LogWarning($"Video player {_handle} not bound!");
				return;
			}

			Log("Getting updated audio template for video player " + _handle);
			bool hasAudio = displayManager._GetAudioTemplate(_handle, out AudioSource[] sources, out float volume);
			UpdateRelayAudio(relay, sources, volume, hasAudio);

			relay = GetSecondaryRelayAtHandle(_handle);
			if (relay < 0) {
				Log("No secondary relay bound to video player " + _handle);
				return;
			}
			UpdateRelayAudio(relay, sources, volume, hasAudio);
		}

		private int GetPrimaryRelayAtHandle(int _handle) {
			if (!isValid) return -1;
			if (_handle < 0 || _handle > primaryHandles.Length) {
				LogError("Handle index out of bounds!");
				return -1;
			}
			int relay = primaryHandles[_handle];
			if (relay < 0) {
				LogError("Handle not bound!");
				return -1;
			}
			return relay;
		}

		private int GetSecondaryRelayAtHandle(int _handle) {
			if (!isValid) return -1;
			if (_handle < 0 || _handle > primaryHandles.Length) {
				LogError("Handle index out of bounds!");
				return -1;
			}
			int relay = secondaryHandles[_handle];
			if (relay < 0) {
				LogError("Handle not bound!");
				return -1;
			}
			return relay;
		}

		private int GetOrBindRelayAtHandle(int _handle, bool _secondary = false) {
			int relay = _secondary ? secondaryHandles[_handle] : primaryHandles[_handle];
			int videoType = _secondary ? secondaryVideoTypes[_handle] : primaryVideoTypes[_handle];
			if (relay < 0) {
				Log("Handle unbound, searching for compatible relay");
				relay = GetAndBindCompatibleRelay(videoType, _handle, _secondary);
			} else if (relays[relay].VideoType != videoType) {
				Log("Video type mismatch, searching for compatible relay");
				UnbindRelay(relay);
				relay = GetAndBindCompatibleRelay(videoType, _handle, _secondary);
			}
			return relay;
		}

		private int GetAndBindCompatibleRelay(int _videoType, int _handle, bool _secondary = false) {
			int relay = GetCompatibleRelay(_videoType);
			if (relay < 0) {
				LogError("No unbound compatible relays!");
				return -1;
			}
			Log($"Found unbound relay: {relay}");
			if (!BindRelayToHandle(_handle, relay, _secondary)) {
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
			if (_handle < 0 || primaryHandles == null || _handle >= primaryHandles.Length) {
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
			FindAndUpdateRelayAudio(_relay, _handle);
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
			relays[_relay]._Stop();
		}

		private void FindAndUpdateRelayAudio(int _relay, int _handle) {
			Log($"Searching for template for handle {_handle} to apply to relay {_relay}");

			bool hasAudio = displayManager._GetAudioTemplate(_handle, out AudioSource[] sources, out float volume);
			UpdateRelayAudio(_relay, sources, volume, hasAudio);
		}

		private void UpdateRelayAudio(int _relay, AudioSource[] _templateAudio, float _templateVolume, bool _hasAudio) {
			Log($"{(_hasAudio ? "Setting audio template" : "Muting audio")} on relay {_relay}");
			if (_hasAudio) relays[_relay]._SetAudioTemplate(_templateAudio, _templateVolume);
			else relays[_relay]._NullAudioTemplate();
		}

		private bool HandleHasQueue(int _handle) {
			return secondaryHandles[_handle] >= 0;
		}

		private int GetHandleFromRelayId(int _id) {
			if (!isValid || _id < 0 || _id >= relays.Length) return -1;
			return relayHandles[_id];
		}

		private void SwapRelayToPrimary(int _handle) {
			int oldPrimary = primaryHandles[_handle];
			int newPrimary = secondaryHandles[_handle];
			relays[oldPrimary]._Stop();
			relayHandles[oldPrimary] = -1;
			relayIsSecondary[newPrimary] = false;
			primaryHandles[_handle] = newPrimary;
			primaryLinks[_handle] = secondaryLinks[_handle];
		}

		private void SendRelayEvent(string _eventName, int _relay) {
			// TODO: Send event
		}

		// Relay callbacks
		public void _RelayVideoEnd(int _id) {
			Initialize();
			int handle = GetHandleFromRelayId(_id);
			if (handle < 0) {
				Log($"Ignoring Video End callback from relay {_id}");
				return;
			}
			int nextRelay;
			relays[_id]._Stop();
			if (!HandleHasQueue(handle)) {
				Log($"Video End on handle {handle} with no queued video.");
				SendRelayEvent("_RelayVideoEnd", _id);
				return;
			}
			_PlayNext(handle);
			SendRelayEvent("_RelayVideoNext", _id);
		}

		public void _RelayVideoReady(int _id) {
			int handle = GetHandleFromRelayId(_id);
			if (handle < 0) {
				Log($"Ignoring Video Ready callback from relay {_id}");
				return;
			}

			bool secondary = relayIsSecondary[_id];
			if (secondary) secondaryVideoDurations[handle] = relays[_id].Duration;
			else primaryVideoDurations[handle] = relays[_id].Duration;
			// TODO: What to do when this video is ready
			SendRelayEvent(secondary ? "_RelayVideoQueueReady" : "_RelayVideoReady", _id);
		}

		public void _RelayVideoError(int _id, VideoError _err) {
			if (!isValid || relayHandles[_id] < 0) {
				Log($"Ignoring Video Error callback from relay {_id}");
				return;
			}

			switch (_err) {
				case VideoError.AccessDenied:
					BlockingUntrustedLinks = true;
					LogError("Allow Untrusted URLs needs to be enabled to load this video!");
					break;
				case VideoError.RateLimited:
					LogWarning("Rate limited! This means you are using a separate video object.");
					// TODO: Requeue a rate limited video
					return;
				case VideoError.InvalidURL:
					LogError("This video could not load because the URL is malformed!\nMake sure you're typing the URL correctly.");
					break;
				case VideoError.Unknown:
					LogError("This video could not load because it could not resolve in Youtube-DL!\nMake sure you're typing the URL correctly.");
					break;
				case VideoError.PlayerError:
					LogError("Unsupported format or corrupt video file!");
					break;
			}

			SendRelayEvent(relayIsSecondary[_id] ? "_RelayVideoQueueError" : "_RelayVideoError", _id);
		}

		public void _RelayVideoPlay(int _id) {
			if (!isValid || relayHandles[_id] < 0) {
				Log($"Ignoring Video Play callback from relay {_id}");
				return;
			}
			// TODO: What to do when video plays
			if (relayIsSecondary[_id]) return;
			SendRelayEvent("_RelayVideoPlay", _id);
		}

		public void _RelayVideoStart(int _id) {
			if (!isValid || relayHandles[_id] < 0) {
				Log($"Ignoring Video Start callback from relay {_id}");
				return;
			}
			// TODO: What to do when video starts
			if (relayIsSecondary[_id]) return;
			SendRelayEvent("_RelayVideoStart", _id);
		}

		public void _RelayVideoLoop(int _id) {
			if (!isValid || relayHandles[_id] < 0) {
				Log($"Ignoring Video Loop callback from relay {_id}");
				return;
			}
			// TODO: What to do when video loops
			if (relayIsSecondary[_id]) return;
			SendRelayEvent("_RelayVideoLoop", _id);
		}

		public void _RelayVideoPause(int _id) {
			if (!isValid || relayHandles[_id] < 0) {
				Log($"Ignoring Video Pause callback from relay {_id}");
				return;
			}
			// TODO: What to do when video pauses
			if (relayIsSecondary[_id]) return;
			SendRelayEvent("_RelayVideoPause", _id);
		}

		public void _RelayVideoTextureChange(int _id, Texture _texture) {
			if (!isValid || relayHandles[_id] < 0) {
				Log("Ignoring texture change since relay is unbound");
				return;
			}

			displayManager._SetVideoTexture(relayHandles[_id], _texture, relayIsSecondary[_id] ? 1 : 0);
		}
	}
}
