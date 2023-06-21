using System;
using Synergiance.MediaPlayer.Diagnostics;
using Synergiance.MediaPlayer.Interfaces;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components.Video;
using VRC.SDKBase;

namespace Synergiance.MediaPlayer {
	/// <summary>
	/// Type of video we're trying to load
	/// </summary>
	public enum VideoType {
		Video, Stream, LowLatency
	}

	/// <summary>
	/// Events sent back from and between SMP behaviours
	/// </summary>
	public enum CallbackEvent {
		MediaLoading, MediaReady, MediaStart, MediaEnd, MediaNext, MediaLoop, MediaPlay, MediaPause, QueueMediaLoading,
		QueueMediaReady, PlayerLocked, PlayerUnlocked, PlayerInitialized, GainedPermissions, PlayerError
	}

	/// <summary>
	/// Possible errors with media or media players
	/// </summary>
	public enum MediaError {
		RateLimited, UntrustedLink, UntrustedQueueLink, InvalidLink, InvalidQueueLink, LoadingError, LoadingErrorQueue,
		Unknown, Uninitialized, Invalid, OutOfRange, NoMedia, Internal, Success, NoError
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
		private SMPMediaController[] mediaControllers; // Media controllers control our video player handles
		private int[] primaryHandles; // Primary relay assigned to handle
		private int[] secondaryHandles; // Secondary relay assigned to handle
		private float[] videoPlayerVolumes; // Volume the video player is set to
		private bool[] videoPlayerMute; // Mute state of the video player
		private bool[] videoPlayerLoop; // Loop state of the video player
		private bool[] videoPlayerResync; // Automatic AV Resync state of the video player
		private const int VideoArrayIncrement = 8;
		private int numVideoPlayers;

		// Misc handles
		[SerializeField] private DisplayManager displayManager; // Handle for display manager, which displays our videos
		[SerializeField] private UdonSharpBehaviour audioLink; // Reference to AudioLink to switch the audio source

		// Video links
		private VRCUrl[] primaryLinks; // Primary video link for handle
		private VRCUrl[] secondaryLinks; // Secondary video link for handle
		private VideoType[] primaryVideoTypes; // Type of primary videos
		private VideoType[] secondaryVideoTypes; // Type of secondary videos
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
		private bool[] isPreroll;

		// Video load queue
		private VRCUrl[] videosToLoad; // Queue of videos to load
		private bool[] videosPlayImmediately; // Whether video in queue should play as soon as its loaded
		private int[] videoRelayHandles; // What relay to use to play the video

		private const float VideoLoadCooldown = 5.25f;
		private float lastVideoLoadAttempt;

		private const int MaxQueueLength = 64;
		private int videosInQueue;
		private int firstVideoInQueue;

		// Sync timing
		private const float PlayerCheckCooldown = 0.1f;
		private float lastPlayerCheck = -PlayerCheckCooldown;

		// Cache for untrusted check
		public bool BlockingUntrustedLinks { get; private set; }

		// Public Callback Variables
		[HideInInspector] public int relayIdentifier;
		[HideInInspector] public VideoError relayVideoError;
		[HideInInspector] public MediaError lastError = MediaError.Unknown;

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

			videosToLoad = new VRCUrl[MaxQueueLength];
			videosPlayImmediately = new bool[MaxQueueLength];
			videoRelayHandles = new int[MaxQueueLength];

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

		public int _Register(SMPMediaController _mediaController, string _name) {
			if ((primaryHandles == null || numVideoPlayers >= primaryHandles.Length) &&
			    !ResizeVideoPlayerArray(numVideoPlayers + VideoArrayIncrement)) {
				LogWarning("Unable to resize video array!");
				return -1;
			}

			Log("Registering media controller");
			mediaControllers[numVideoPlayers] = _mediaController;
			if (string.IsNullOrWhiteSpace(_name))
				LogWarning("Media controller name is null!");
			Log($"Adding media controller \"{_name}\" to display manager");
			displayManager._AddSource(_name);
			int id = numVideoPlayers++;
			return id;
		}

		/// <summary>
		/// Interface for creating new bindings to match up with the player
		/// manager.
		/// </summary>
		/// <returns>True on success, false if anything's wrong.</returns>
		private bool ResizeVideoPlayerArray(int _newLength) {
			Initialize();
			if (!isValid) {
				LogError("Invalid!");
				lastError = MediaError.Invalid;
				return false;
			}

			int prevLength = primaryHandles != null ? primaryHandles.Length : 0;
			Log($"Resizing video player handle references from {prevLength} to {_newLength}");

			if (_newLength <= prevLength) {
				LogError("This should never appear. Video player list will always expand.");
				lastError = MediaError.Internal;
				return false;
			}

			// Check to see whether handles arrays have been initialized
			if (prevLength == 0) {
				Log("Creating handle arrays of size " + _newLength);
				primaryHandles = new int[_newLength];
				secondaryHandles = new int[_newLength];
				primaryLinks = new VRCUrl[_newLength];
				secondaryLinks = new VRCUrl[_newLength];
				primaryLoadAttempts = new int[_newLength];
				secondaryLoadAttempts = new int[_newLength];
				primaryVideoTypes = new VideoType[_newLength];
				secondaryVideoTypes = new VideoType[_newLength];
				primaryVideoDurations = new float[_newLength];
				secondaryVideoDurations = new float[_newLength];
				mediaControllers = new SMPMediaController[_newLength];
				videoPlayerVolumes = new float[_newLength];
				videoPlayerMute = new bool[_newLength];
				videoPlayerLoop = new bool[_newLength];
				videoPlayerResync = new bool[_newLength];
				hasPlayerHandles = true;
			} else {
				Log($"Expanding handle arrays from {prevLength} to {_newLength}");
				ExpandIntArray(ref primaryHandles, _newLength);
				ExpandIntArray(ref secondaryHandles, _newLength);
				ExpandIntArray(ref primaryLoadAttempts, _newLength);
				ExpandIntArray(ref secondaryLoadAttempts, _newLength);
				ExpandVideoTypeArray(ref primaryVideoTypes, _newLength);
				ExpandVideoTypeArray(ref secondaryVideoTypes, _newLength);
				ExpandLinkArray(ref primaryLinks, _newLength);
				ExpandLinkArray(ref secondaryLinks, _newLength);
				ExpandFloatArray(ref primaryVideoDurations, _newLength);
				ExpandFloatArray(ref secondaryVideoDurations, _newLength);
				ExpandMediaControllerArray(ref mediaControllers, _newLength);
				ExpandFloatArray(ref videoPlayerVolumes, _newLength);
				ExpandBoolArray(ref videoPlayerLoop, _newLength);
				ExpandBoolArray(ref videoPlayerMute, _newLength);
				ExpandBoolArray(ref videoPlayerResync, _newLength);
			}

			Log("Initializing new handles");
			for (int i = prevLength; i < _newLength; i++) {
				primaryHandles[i] = -1;
				secondaryHandles[i] = -1;
				primaryLinks[i] = VRCUrl.Empty;
				secondaryLinks[i] = VRCUrl.Empty;
				primaryLoadAttempts[i] = -1;
				secondaryLoadAttempts[i] = -1;
				primaryVideoTypes[i] = VideoType.Video;
				secondaryVideoTypes[i] = VideoType.Video;
				primaryVideoDurations[i] = -1;
				secondaryVideoDurations[i] = -1;
				videoPlayerVolumes[i] = 1;
				videoPlayerMute[i] = false;
				videoPlayerLoop[i] = false;
				videoPlayerResync[i] = true;
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

		private void ExpandBoolArray(ref bool[] _array, int _newLength) {
			bool[] tmpArray = new bool[_newLength];
			Array.Copy(_array, tmpArray, _array.Length);
			_array = tmpArray;
		}

		private void ExpandLinkArray(ref VRCUrl[] _array, int _newLength) {
			VRCUrl[] tmpArray = new VRCUrl[_newLength];
			Array.Copy(_array, tmpArray, _array.Length);
			_array = tmpArray;
		}

		private void ExpandVideoTypeArray(ref VideoType[] _array, int _newLength) {
			VideoType[] tmpArray = new VideoType[_newLength];
			Array.Copy(_array, tmpArray, _array.Length);
			_array = tmpArray;
		}

		private void ExpandMediaControllerArray(ref SMPMediaController[] _array, int _newLength) {
			SMPMediaController[] tmpArray = new SMPMediaController[_newLength];
			Array.Copy(_array, tmpArray, _array.Length);
			_array = tmpArray;
		}

		// TODO: Make generic method akin to this
		private void ExpandArray(ref Array _array, int _newLength) {
			Array tmpArray = Array.CreateInstance(_array.GetType(), _newLength);
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
			if (lastPlayerCheck + PlayerCheckCooldown > Time.time) return;
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
		public bool _LoadVideo(VRCUrl _videoLink, VideoType _videoType, int _handle, bool _playImmediately = false) {
			if (_handle < 0 || primaryHandles == null || _handle >= primaryHandles.Length) {
				LogError("Invalid handle!");
				return false;
			}

			Log($"Load video of type {_videoType} for handle {_handle} and link \"{_videoLink}\" {(_playImmediately ? " to play immediately" : "")}");

			primaryLinks[_handle] = _videoLink;
			primaryVideoTypes[_handle] = _videoType;

			int relay = GetOrBindRelayAtHandle(_handle);
			// TODO: If no valid relay just make sure to attempt to bind again
			if (relay < 0) return false;

			relays[relay]._Stop();
			QueueVideo(_videoLink, _playImmediately, relay);
			if (videosInQueue == 1) AttemptLoadNext();
			return true;
		}

		public bool _LoadNextVideo(VRCUrl _videoLink, VideoType _videoType, int _handle) {
			if (_handle < 0 || primaryHandles == null || _handle >= primaryHandles.Length) {
				LogError("Invalid handle!");
				return false;
			}

			secondaryLinks[_handle] = _videoLink;
			secondaryVideoTypes[_handle] = _videoType;
			// TODO: Queue up a video
			return true;
		}

		private void QueueVideo(VRCUrl _link, bool _playImmediately, int _relay) {
			if (_link == null) {
				LogError("Link cannot be null!");
				return;
			}
			if (videosInQueue >= MaxQueueLength) {
				LogError($"Cannot queue video \"{_link}\"!");
				return;
			}
			if (_relay < 0 || _relay >= relays.Length) {
				LogError("Relay index out of bounds!");
				return;
			}

			int pushSlot = (firstVideoInQueue + videosInQueue++) % MaxQueueLength;
			videosToLoad[pushSlot] = _link;
			videosPlayImmediately[pushSlot] = _playImmediately;
			videoRelayHandles[pushSlot] = _relay;
			Log($"Video loaded into slot {pushSlot}, there are now {videosInQueue} videos in the queue.");
		}

		private void AttemptLoadNext() {
			Log("Attempting to load next video");
			if (lastVideoLoadAttempt + VideoLoadCooldown > Time.time + float.Epsilon) {
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
			firstVideoInQueue = (firstVideoInQueue + 1) % MaxQueueLength;
			videosInQueue--;
			Log($"{(playImmediately ? "Playing" : "Loading")} video \"{videoLink}\" with relay {relayHandle}");
			LoadVideoInternal(videoLink, playImmediately, relayHandle);
			if (videosInQueue < 1) {
				Log("No more videos in queue");
				return;
			}
			Log($"{videosInQueue} more video{(videosInQueue == 1 ? "" : "s")} in queue");
			SendCustomEventDelayedSeconds("AttemptLoadNext", VideoLoadCooldown - float.Epsilon);
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

		public bool _PrePlayNext(int _handle) {
			int relay = GetSecondaryRelayAtHandle(_handle);
			if (relay < 0) return false;
			Log($"Pre-playing next video relay {relay} using handle {_handle}");
			if (Mathf.Abs(relays[_handle].Time) > 0.01f) relays[_handle].Time = 0;
			return relays[relay]._Play();
		}

		public bool _PrerollNext(int _handle) {
			int relay = GetSecondaryRelayAtHandle(_handle);
			if (relay < 0) return false;
			Log($"Starting preroll on next video relay {relay} using handle {_handle}");
			if (Mathf.Abs(relays[_handle].Time) > 0.01f) relays[_handle].Time = 0;
			return relays[relay]._Play();
		}

		public bool _StopPrerollNext(int _handle) {
			int relay = GetSecondaryRelayAtHandle(_handle);
			if (relay < 0) return false;
			Log($"Stopping preroll on next video relay {relay} using handle {_handle}");
			relays[_handle].Time = 0;
			return relays[_handle]._Pause();
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

		public bool _SeekTo(int _handle, float _time) {
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

		public bool _GetPlaying(int _handle) {
			int relay = GetPrimaryRelayAtHandle(_handle);
			if (relay < 0) return false;
			Log($"Getting paused state from video relay {relay} using handle {_handle}");
			return relays[relay].IsPlaying;
		}

		public bool _SetVolume(int _handle, float _volume) {
			if (_handle < 0 || _handle >= numVideoPlayers) {
				lastError = MediaError.OutOfRange;
				return false;
			}

			int relay = GetPrimaryRelayAtHandle(_handle);
			if (relay >= 0) relays[relay].Volume = _volume;

			videoPlayerVolumes[_handle] = _volume;
			lastError = MediaError.Success;
			return true;
		}

		public float _GetVolume(int _handle) {
			if (_handle < 0 || _handle >= numVideoPlayers) {
				lastError = MediaError.OutOfRange;
				return -1;
			}

			lastError = MediaError.Success;
			return videoPlayerVolumes[_handle];
		}

		public bool _SetMute(int _handle, bool _mute) {
			if (_handle < 0 || _handle >= numVideoPlayers) {
				lastError = MediaError.OutOfRange;
				return false;
			}

			int relay = GetPrimaryRelayAtHandle(_handle);
			if (relay >= 0) relays[relay].Mute = _mute;

			videoPlayerMute[_handle] = _mute;
			lastError = MediaError.Success;
			return true;
		}

		public bool _GetMute(int _handle) {
			if (_handle < 0 || _handle >= numVideoPlayers) {
				lastError = MediaError.OutOfRange;
				return false;
			}

			lastError = MediaError.Success;
			return videoPlayerMute[_handle];
		}

		public bool _SetLoop(int _handle, bool _loop) {
			if (_handle < 0 || _handle >= numVideoPlayers) {
				lastError = MediaError.OutOfRange;
				return false;
			}

			int relay = GetPrimaryRelayAtHandle(_handle);
			if (relay >= 0) relays[relay].Loop = _loop;

			videoPlayerLoop[_handle] = _loop;
			lastError = MediaError.Success;
			return true;
		}

		public bool _GetLoop(int _handle) {
			if (_handle < 0 || _handle >= numVideoPlayers) {
				lastError = MediaError.OutOfRange;
				return false;
			}

			lastError = MediaError.Success;
			return videoPlayerLoop[_handle];
		}

		public bool _SetMediaResync(int _handle, bool _resync) {
			if (_handle < 0 || _handle >= numVideoPlayers) {
				lastError = MediaError.OutOfRange;
				return false;
			}

			int relay = GetPrimaryRelayAtHandle(_handle);
			if (relay >= 0) relays[relay].AutomaticResync = _resync;

			videoPlayerResync[_handle] = _resync;
			lastError = MediaError.Success;
			return true;
		}

		public bool _GetMediaResync(int _handle) {
			if (_handle < 0 || _handle >= numVideoPlayers) {
				lastError = MediaError.OutOfRange;
				return false;
			}

			lastError = MediaError.Success;
			return videoPlayerResync[_handle];
		}

		public bool _GetMediaReady(int _handle) {
			int relay = GetPrimaryRelayAtHandle(_handle);
			if (relay < 0) return false;
			return relays[relay].IsReady;
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
				lastError = MediaError.Uninitialized;
				return;
			}

			if (_handle < 0 || _handle >= primaryHandles.Length) {
				LogError("Video player at that ID does not exist!");
				lastError = MediaError.OutOfRange;
				return;
			}

			int relay = GetPrimaryRelayAtHandle(_handle);
			if (relay < 0) {
				LogWarning($"Video player {_handle} not bound!");
				lastError = MediaError.NoMedia;
				return;
			}

			Log("Getting updated audio template for video player " + _handle);
			bool hasAudio = displayManager._GetAudioTemplate(_handle, out AudioSource[] sources, out float volume);
			UpdateRelayAudio(relay, sources, volume, hasAudio);

			relay = GetSecondaryRelayAtHandle(_handle);
			if (relay < 0) {
				Log("No secondary relay bound to video player " + _handle);
				lastError = MediaError.NoMedia;
				return;
			}
			UpdateRelayAudio(relay, sources, volume, hasAudio);
		}

		public bool _HasVideo(int _handle) {
			if (!isValid) {
				lastError = MediaError.Invalid;
				return false;
			}

			if (_handle < 0 || _handle >= primaryHandles.Length) {
				LogError($"Handle index {_handle} out of bounds!");
				lastError = MediaError.OutOfRange;
				return false;
			}

			return primaryHandles[_handle] >= 0;
		}

		public bool _HasQueuedVideo(int _handle) {
			if (!isValid) {
				lastError = MediaError.Invalid;
				return false;
			}

			if (_handle < 0 || _handle >= secondaryHandles.Length) {
				LogError($"Handle index {_handle} out of bounds!");
				lastError = MediaError.OutOfRange;
				return false;
			}

			return secondaryHandles[_handle] >= 0;
		}

		// ReSharper disable Unity.PerformanceAnalysis
		private int GetPrimaryRelayAtHandle(int _handle) {
			if (!isValid) {
				lastError = MediaError.Invalid;
				return -1;
			}

			if (_handle < 0 || _handle >= primaryHandles.Length) {
				LogError($"Handle index {_handle} out of bounds!");
				lastError = MediaError.OutOfRange;
				return -1;
			}

			int relay = primaryHandles[_handle];
			if (relay < 0) {
				LogError($"Handle {_handle} not bound!");
				lastError = MediaError.NoMedia;
				return -1;
			}

			lastError = MediaError.NoError;
			return relay;
		}

		private int GetSecondaryRelayAtHandle(int _handle) {
			if (!isValid) {
				lastError = MediaError.Invalid;
				return -1;
			}

			if (_handle < 0 || _handle >= secondaryHandles.Length) {
				LogError("Handle index out of bounds!");
				lastError = MediaError.OutOfRange;
				return -1;
			}

			int relay = secondaryHandles[_handle];
			if (relay < 0) {
				LogError("Handle not bound!");
				lastError = MediaError.NoMedia;
				return -1;
			}

			lastError = MediaError.NoError;
			return relay;
		}

		private int GetOrBindRelayAtHandle(int _handle, bool _secondary = false) {
			int relay = _secondary ? secondaryHandles[_handle] : primaryHandles[_handle];
			VideoType videoType = _secondary ? secondaryVideoTypes[_handle] : primaryVideoTypes[_handle];
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

		private int GetAndBindCompatibleRelay(VideoType _videoType, int _handle, bool _secondary = false) {
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

		private int GetCompatibleRelay(VideoType _videoType) {
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
				lastError = MediaError.OutOfRange;
				return false;
			}

			if (_handle < 0 || primaryHandles == null || _handle >= primaryHandles.Length) {
				LogError("Handle out of bounds, cannot bind!");
				lastError = MediaError.OutOfRange;
				return false;
			}

			if (relayHandles[_relay] >= 0) {
				LogError("Relay still bound!");
				lastError = MediaError.Internal;
				return false;
			}

			if (_secondary) {
				if (secondaryHandles[_handle] >= 0) {
					LogError("Secondary handle still bound!");
					lastError = MediaError.Internal;
					return false;
				}
			} else {
				if (primaryHandles[_handle] >= 0) {
					LogError("Primary handle still bound!");
					lastError = MediaError.Internal;
					return false;
				}
			}

			lastError = MediaError.NoError;

			// Actual bind
			if (_secondary) secondaryHandles[_handle] = _relay;
			else primaryHandles[_handle] = _relay;
			relayHandles[_relay] = _handle;
			relayIsSecondary[_relay] = _secondary;
			FindAndUpdateRelayAudio(_relay, _handle);
			UpdateRelaySettings(_relay, _handle);
			Log($"Successfully bound relay {_relay} to{(_secondary ? " secondary" : "")} handle {_handle}");
			return true;
		}

		private int GetFirstUnboundRelay(int _startOffset = 0) {
			Log($"Getting first unbound relay{(_startOffset != 0 ? $" starting at {_startOffset}" : "")}");
			if (_startOffset >= relayHandles.Length) {
				LogError("Start offset out of bounds!");
				lastError = MediaError.OutOfRange;
				return -1;
			}

			for (int i = _startOffset; i < relayHandles.Length; i++) {
				if (relayHandles[i] >= 0) continue;
				Log($"Found unbound relay at index {i}");
				lastError = MediaError.NoError;
				return i;
			}

			Log("Found no unbound relay");
			lastError = MediaError.Unknown;
			return -1;
		}

		private void UnbindRelay(int _relay) {
			int handle = relayHandles[_relay];
			if (handle < 0) {
				LogWarning("Relay was not bound!");
				lastError = MediaError.Internal;
				return;
			}

			lastError = MediaError.NoError;

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

		private void UpdateRelaySettings(int _relay, int _handle) {
			relays[_relay].Loop = videoPlayerLoop[_handle];
			relays[_relay].Mute = videoPlayerMute[_handle];
			relays[_relay].Volume = videoPlayerVolumes[_handle];
			relays[_relay].AutomaticResync = videoPlayerResync[_handle];
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
			if (!isValid) {
				lastError = MediaError.Invalid;
				return -1;
			}

			if (_id < 0 || _id >= relays.Length) {
				lastError = MediaError.OutOfRange;
				return -1;
			}

			lastError = MediaError.NoError;
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

		private void SendRelayEvent(CallbackEvent _event, int _relay) {
			mediaControllers[_relay]._SendCallback(_event);
		}

		private void SendError(MediaError _error, int _relay) {
			mediaControllers[_relay]._SendError(_error);
		}

		#region Relay Callbacks
		private void MediaEnd(int _id) {
			Initialize();
			int handle = GetHandleFromRelayId(_id);
			if (handle < 0) {
				Log($"Ignoring Video End callback from relay {_id}");
				return;
			}

			relays[_id]._Stop();
			if (!HandleHasQueue(handle)) {
				Log($"Video End on handle {handle} with no queued video.");
				SendRelayEvent(CallbackEvent.MediaEnd, _id);
				lastError = MediaError.Unknown;
				return;
			}

			lastError = MediaError.NoError;
			if (!relays[secondaryHandles[handle]].IsPlaying) _PlayNext(handle);
			else SwapRelayToPrimary(handle);
			SendRelayEvent(CallbackEvent.MediaNext, _id);
		}

		private void MediaReady(int _id) {
			int handle = GetHandleFromRelayId(_id);
			if (handle < 0) {
				Log($"Ignoring Video Ready callback from relay {_id}");
				return;
			}

			bool secondary = relayIsSecondary[_id];
			if (secondary) secondaryVideoDurations[handle] = relays[_id].Duration;
			else primaryVideoDurations[handle] = relays[_id].Duration;
			// TODO: What to do when this video is ready
			SendRelayEvent(secondary ? CallbackEvent.QueueMediaReady : CallbackEvent.MediaReady, _id);
		}

		private void MediaLoading(int _id) {
			int handle = GetHandleFromRelayId(_id);
			if (handle < 0) {
				Log($"Ignoring Video Ready callback from relay {_id}");
				return;
			}

			bool secondary = relayIsSecondary[_id];
			SendRelayEvent(secondary ? CallbackEvent.QueueMediaLoading : CallbackEvent.MediaLoading, _id);
		}

		/// <summary>
		/// Event receiver for errors
		/// </summary>
		/// <param name="_id">ID of the video relay</param>
		/// <param name="_err">Error being sent</param>
		public void _RelayError(int _id, MediaError _err) {
			if (!isValid || relayHandles[_id] < 0) {
				Log($"Ignoring Video Error callback from relay {_id}");
				return;
			}

			bool isSecondary = relayIsSecondary[_id];
			lastError = _err;

			switch (_err) {
				case MediaError.UntrustedLink:
					BlockingUntrustedLinks = true;
					LogError("Allow Untrusted URLs needs to be enabled to load this video!");
					if (isSecondary) lastError = MediaError.UntrustedQueueLink;
					break;
				case MediaError.RateLimited:
					LogWarning("Rate limited! This means you are using a separate video object.");
					// TODO: Requeue a rate limited video
					return;
				case MediaError.InvalidLink:
					LogError("This video could not load because the URL is malformed!\nMake sure you're typing the URL correctly.");
					if (isSecondary) lastError = MediaError.InvalidQueueLink;
					break;
				case MediaError.Unknown:
					LogError("This video could not load because it could not resolve in Youtube-DL!\nMake sure you're typing the URL correctly.");
					break;
				case MediaError.LoadingError:
					LogError("Unsupported format or corrupt video file!");
					if (isSecondary) lastError = MediaError.LoadingErrorQueue;
					break;
				case MediaError.Invalid:
					LogError("VRChat just spat an unknown video error at us. Please update SMP, contact Synergiance, or contact VRChat");
					break;
			}

			SendError(lastError, relayHandles[_id]);
		}

		/// <summary>
		/// Event receiver for VideoRelay
		/// </summary>
		/// <param name="_event">Event being relayed</param>
		/// <param name="_id">ID of the VideoRelay</param>
		public void _RelayEvent(CallbackEvent _event, int _id) {
			switch (_event) {
				case CallbackEvent.MediaLoading:
					MediaLoading(_id);
					break;
				case CallbackEvent.MediaReady:
					MediaReady(_id);
					break;
				case CallbackEvent.MediaStart:
					MediaStart(_id);
					break;
				case CallbackEvent.MediaEnd:
					MediaEnd(_id);
					break;
				case CallbackEvent.MediaLoop:
					MediaLoop(_id);
					break;
				case CallbackEvent.MediaPlay:
					MediaPlay(_id);
					break;
				case CallbackEvent.MediaPause:
					MediaPause(_id);
					break;
				case CallbackEvent.PlayerError:
					LogWarning("Warning: Do not send error events to VideoManager!");
					break;
				default:
					LogWarning("Warning: Do not send random events to VideoManager!");
					break;
			}
		}

		private void MediaPlay(int _id) {
			if (!isValid || relayHandles[_id] < 0) {
				Log($"Ignoring Video Play callback from relay {_id}");
				return;
			}
			// TODO: What to do when video plays
			if (relayIsSecondary[_id]) return;
			SendRelayEvent(CallbackEvent.MediaPlay, relayHandles[_id]);
		}

		private void MediaStart(int _id) {
			if (!isValid || relayHandles[_id] < 0) {
				Log($"Ignoring Video Start callback from relay {_id}");
				return;
			}
			// TODO: What to do when video starts
			if (relayIsSecondary[_id]) return;
			SendRelayEvent(CallbackEvent.MediaStart, relayHandles[_id]);
		}

		private void MediaLoop(int _id) {
			if (!isValid || relayHandles[_id] < 0) {
				Log($"Ignoring Video Loop callback from relay {_id}");
				return;
			}
			// TODO: What to do when video loops
			if (relayIsSecondary[_id]) return;
			SendRelayEvent(CallbackEvent.MediaLoop, relayHandles[_id]);
		}

		private void MediaPause(int _id) {
			if (!isValid || relayHandles[_id] < 0) {
				Log($"Ignoring Video Pause callback from relay {_id}");
				return;
			}
			// TODO: What to do when video pauses
			if (relayIsSecondary[_id]) return;
			SendRelayEvent(CallbackEvent.MediaPause, relayHandles[_id]);
		}

		public void _RelayTextureChange(int _id, Texture _texture) {
			if (!isValid || relayHandles[_id] < 0) {
				Log("Ignoring texture change since relay is unbound");
				return;
			}

			displayManager._SetVideoTexture(relayHandles[_id], _texture, relayIsSecondary[_id] ? 1 : 0);
		}
		#endregion
	}
}
