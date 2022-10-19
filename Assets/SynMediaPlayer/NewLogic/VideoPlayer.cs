using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace Synergiance.MediaPlayer {
	/// <summary>
	/// The type of media a video can be. This is currently unused, as enums are
	/// unsupported in UdonSharp as of writing this.
	/// </summary>
	public enum MediaType {
		Video, Stream, Music, MusicStream
	}

	public enum ResyncMode {
		Normal, Resync, CatchUp, WaitForSync, WaitForVideo, Seek, WaitForLoad,
		WaitToPlay, WaitToPause, WaitForData
	}

	/// <summary>
	/// Video player is the controlling class that makes videos play. Any video
	/// currently playing is attached to one of these. It keeps track of the
	/// time, whether its playing or paused, whether its a stream, locked, etc.
	/// It's the interface for where your play/pause calls go and will be the
	/// deciding factor on what to do with that information. This is written to
	/// run alongside other video player behaviours.
	/// </summary>
	[UdonBehaviourSyncMode(BehaviourSyncMode.Manual), DefaultExecutionOrder(-10)]
	public class VideoPlayer : VideoBehaviour {
		[SerializeField] private bool lockByDefault;
		[SerializeField] private PlayerManager playerManager;
		[SerializeField] private VideoQueue queue;
		[SerializeField] private string playerName = "Video Player";
		[Range(0, 1)] [SerializeField] private float volume = 0.55f;
		private PlaylistManager playlistManager;
		private VideoManager videoManager;

		#region Network Sync Variables
		private bool paused;
		[UdonSynced] private bool pausedSync;
		private float pauseTime;
		[UdonSynced] private float pauseTimeSync;
		private float beginTime;
		private int beginNetTime;
		[UdonSynced] private int beginNetTimeSync;
		private int mediaType;
		[UdonSynced] private int mediaTypeSync;
		private int syncIndex = -1;
		[UdonSynced] private int syncIndexSync;
		private bool isLocked;
		[UdonSynced] private bool isLockedSync;
		#endregion

		#region Behaviour Integrity Variables
		private bool initialized;
		private bool isValid;
		private UdonSharpBehaviour[] callbacks;
		private int identifier;
		#endregion

		#region Video Sync Variables
		private int syncMode;
		private const float ResyncThreshold = 5.0f;
		private const float ResyncCooldown = 30.0f;
		private const float ResyncDetect = 0.1f;
		private const float DriftTolerance = 0.5f;
		private const float DriftCooldown = 5.0f;
		private const float ColdSpoolTime = 1.0f;
		private const float HotSpoolTime = 0.25f;
		private const float SeekCooldown = 0.5f;
		private const float ResyncTimeout = 3.0f;
		private const float ReloadTimeout = 10.0f;
		private float drift;
		private float nextResync = -1;
		private float lastResync = -1;
		private float timeAtLastResync = -1;
		#endregion

		#region Video Playlist Data
		private int currentPlaylistType;
		private int currentPlaylist;
		private int currentVideo;
		private string preferredVariant;
		private string videoName;
		private string friendlyVideoName;
		#endregion

		private readonly string[] syncModeNames = {
			"Normal", "Resync", "Catch Up", "Wait For Sync", "Wait For Video",
			"Seek", "Wait For Load", "Wait To Play", "Wait To Pause", "Await Data"
		};

		#region Video Validation
		private readonly string[] videoHosts = {
			"drive.google.com", "twitter.com", "vimeo.com", "youku.com",
			"tiktok.com", "nicovideo.jp", "facebook.com", "vrcdn.video",
			"soundcloud.com", "youtu.be", "youtube.com", "www.youtube.com",
			"mixcloud.com"
		};

		private readonly string[] streamHosts = {
			"twitch.tv", "vrcdn.live", "youtu.be", "youtube.com",
			"www.youtube.com", "mixcloud.com"
		};

		private readonly string[] audioOnlyHosts = {
			"soundcloud.com", "mixcloud.com"
		};

		private readonly string[] videoProtocols = {
			"http", "https", "rtmp", "rtsp", "rtspt", "rtspu"
		};

		private readonly string[] lowLatencyProtocols = {
			"rtmp", "rtsp", "rtspt", "rtspu"
		};

		private const int LinkVideoBit = 0x1;
		private const int LinkStreamBit = 0x2;
		private const int LinkLowLatencyBit = 0x4;
		private const int LinkAudioOnlyBit = 0x8;
		#endregion

		#region Behaviour Debug Settings
		protected override string DebugName => "Video Player";
		protected override string DebugColor => ColorToHtmlStringRGB(new Color(0.25f, 0.65f, 0.1f));
		#endregion

		#region Synced Accessors
		/// <summary>
		/// Public interface for determining whether video player is locked or not.
		/// Hides the internal mechanism for locking and unlocking the video player.
		/// </summary>
		public bool IsLocked {
			get {
				Initialize();
				return !isValid || isLocked;
			}
			private set {
				if (isLocked == isLockedSync == value) return;
				// TODO: Permission check and sync
				isLocked = value;
				Log($"{(isLocked ? "Locking" : "Unlocking")} the player");
				//CallCallbacks(isLocked ? "_SecurityLocked" : "_SecurityUnlocked");
				Sync();
			}
		}

		public bool Paused {
			get => paused;
			private set {
				if (paused == pausedSync == value) return;
				if (!CheckValidAndAccess(value ? "pause" : "unpause")) return;
				pausedSync = value;
				Log((value ? "Pausing" : "Playing") + " the video");
				Sync();
			}
		}
		#endregion

		#region Synced Variables
		private float PauseTime {
			get => pauseTime;
			set {
				if (Mathf.Abs(value - pauseTime) + Mathf.Abs(value - pauseTimeSync) < 0.1) return;
				pauseTimeSync = value;
				Log("Setting paused time to " + value);
				Sync();
			}
		}

		private int BeginNetTime {
			get => beginNetTime;
			set {
				if (beginNetTimeSync == value && beginNetTime == value) return;
				beginNetTimeSync = value;
				Log("Setting begin net time to " + value);
				Sync();
			}
		}

		private int MediaType {
			get => mediaType;
			set {
				if (mediaType == value && mediaTypeSync == value) return;
				mediaTypeSync = value;
				Log("Setting media type to: " + value);
				Sync();
			}
		}

		private int SyncIndex {
			get => syncIndex;
			set {
				if (syncIndex == value && syncIndexSync == value) return;
				syncIndexSync = value;
				Log("Updating sync index to: " + value);
				Sync();
			}
		}
		#endregion

		#region Accessors
		public bool IsReady { private set; get; }
		public bool UnlockedOrHasAccess => !IsLocked || securityManager.HasAccess;
		public float CurrentTime => IsReady ? paused ? pauseTime : Time.time - beginTime : 0;
		public float RawTime => IsReady ? playerManager._GetTime(identifier) : 0;
		public bool Playing => !paused;
		public float Duration => GetDuration();
		public float Volume => volume;
		public int CurrentPlaylistType => currentPlaylistType;
		public int CurrentPlaylist => currentPlaylist;
		public int CurrentVideo => currentVideo;

		public string PreferredVariant {
			get => preferredVariant;
			set {
				preferredVariant = value;
				// TODO: Switch to preferred variant
			}
		}
		#endregion

		#region Initialization
		private void Start() {
			Initialize();
		}

		private void Initialize() {
			if (initialized) return;
			Log("Initialize!");
			CheckValid();
			initialized = true;
		}

		private void CheckValid() {
			if (playerManager == null) {
				LogError("Player manager is missing!");
				return;
			}

			identifier = playerManager._RegisterVideoPlayer(this, playerName);

			if (identifier < 0) {
				LogError("Failed to register video player!");
				return;
			}

			videoManager = playerManager.GetVideoManager();
			if (videoManager == null) {
				LogError("Video manager is missing!");
				return;
			}

			playlistManager = playerManager.GetPlaylistManager();
			if (playlistManager == null) {
				LogError("Playlist manager is missing!");
				return;
			}

			if (queue == null) {
				LogError("Queue is missing!");
				return;
			}

			Log("Successfully validated!");

			isLocked = lockByDefault && securityManager.HasSecurity;
			isValid = true;
		}
		#endregion

		#region Public Methods
		public void _LoadVideo(VRCUrl _link, bool _playImmediately) {
			if (!CheckValidAndAccess("load")) return;
			if (_link == null || string.IsNullOrWhiteSpace(_link.ToString())) {
				LogError("Link cannot be blank!");
				return;
			}
			currentPlaylist = -1;
			currentVideo = -1;
			currentPlaylistType = -1;
			LoadInternal(_link, _playImmediately);
		}

		public void _LoadFromPlaylist(int _listType, int _listId, int _videoIdx, bool _playImmediately, string _variant = null) {
			if (!CheckValidAndAccess("load")) return;

			bool foundVideo = playlistManager._GetVideo(_listType, _listId, _videoIdx, _variant,
				out string foundVideoName, out string foundVideoShortName, out VRCUrl foundVideoLink);

			if (!foundVideo) {
				LogError("Could not find video in playlist!");
				return;
			}

			currentPlaylistType = _listType;
			currentPlaylist = _listId;
			currentVideo = _videoIdx;
			preferredVariant = _variant;
			videoName = foundVideoName;
			friendlyVideoName = foundVideoShortName;

			LoadInternal(foundVideoLink, _playImmediately);
		}

		public void _Play() {
			if (!CheckValidAndAccess("play")) return;

			Log("Playing");
			Paused = false;
			playerManager._PlayVideo(identifier);
		}

		public void _Pause() {
			if (!CheckValidAndAccess("pause")) return;

			Log("Pausing");
			Paused = true;
			playerManager._PauseVideo(identifier);
		}

		public void _Stop() {
			if (!CheckValidAndAccess("stop")) return;

			Log("Stopping");
			Paused = true;
			// TODO: Set time to 0
			playerManager._StopVideo(identifier);
		}

		private bool CheckValidAndAccess(string _action) {
			if (!isValid) {
				LogError($"Cannot {_action}, invalid!");
				return false;
			}

			if (!UnlockedOrHasAccess) {
				LogWarning($"Cannot {_action}, not allowed to interact with video player!");
				return false;
			}

			return true;
		}

		public void _Lock() {
			if (!securityManager.HasSecurity) {
				Log("No security, cannot lock!");
				return;
			}
			if (isLocked) {
				Log("Already locked!");
				return;
			}
			if (!securityManager.HasAccess) {
				LogWarning("You don't have access to lock the player!");
				return;
			}
			IsLocked = true;
		}

		public void _Unlock() {
			if (!securityManager.HasSecurity) {
				Log("No security, already unlocked!");
				return;
			}
			if (!isLocked) {
				Log("Already unlocked!");
				return;
			}
			if (!securityManager.HasAccess) {
				LogWarning("You don't have access to unlock the player!");
				return;
			}
			IsLocked = false;
		}

		public void _QueueIndexUpdate() {
			if (syncIndex != queue.SyncIndex) {
				//
				return;
			}
			VRCUrl link = queue.CurrentVideo;
		}
		#endregion

		private float GetDuration() {
			Initialize();
			if (!isValid) return -1;
			// TODO: Implementation
			// Determine whether a video is loaded
			//
			return -1;
		}

		private void LoadInternal(VRCUrl _link, bool _playImmediately) {
			bool linkValid = CheckLink(_link, true, out int linkType, out string error);
			if (!linkValid) {
				LogError("Cannot load link: " + error);
				return;
			}

			queue._SetCurrentVideo(_link);
			syncIndex++;
			Sync();
		}

		private void PlayInternal() {
			//
		}

		private void PauseInternal() {
			//
		}

		private void StopInternal() {
			//
		}

		private bool CheckLink(VRCUrl _link, bool _checkType, out int _linkType, out string _error) {
			_linkType = 0;
			_error = null;
			if (_link == null || string.IsNullOrWhiteSpace(_link.ToString())) {
				_error = "Blank Link";
				return false;
			}

			string linkStr = _link.ToString();
			int colonPos = linkStr.IndexOf("://", StringComparison.Ordinal);
			if (colonPos < 0 || linkStr.Length < colonPos + 5) {
				_error = "Malformed Link";
				return false;
			}

			int prefixLength = linkStr.IndexOf('/', colonPos + 3);
			if (prefixLength < 1) {
				_error = "Malformed Link";
				return false;
			}

			string videoProtocol = linkStr.Substring(0, colonPos).ToLower();
			if (Array.IndexOf(videoProtocols, videoProtocol) < 0) {
				_error = "Unsupported Protocol: " + videoProtocol;
				return false;
			}

			if (!_checkType) return true;

			string videoHost = linkStr.Substring(colonPos + 3, prefixLength - 3 - colonPos);
			Log($"Detected Protocol: {videoProtocol}\nDetected Host: {videoHost}");

			if (Array.IndexOf(videoHosts, videoHost) >= 0) _linkType |= LinkVideoBit;
			if (Array.IndexOf(streamHosts, videoHost) >= 0) _linkType |= LinkStreamBit;
			if (Array.IndexOf(audioOnlyHosts, videoHost) >= 0) _linkType |= LinkAudioOnlyBit;
			if (Array.IndexOf(lowLatencyProtocols, videoProtocol) >= 0) _linkType |= LinkStreamBit | LinkLowLatencyBit;

			return true;
		}

		#region Video Sync
		public void _UpdateSync() {
			Log("Update Sync");
			if (!IsReady || (paused && syncMode == 0)) return;
			drift = RawTime - CurrentTime;
			switch (syncMode) {
				case 1: // Resync
					UpdateResync();
					break;
				case 2: // Catch Up
					UpdateCatchUp();
					break;
				case 3: // Wait for sync
					UpdateWaitSync();
					break;
				case 4: // Wait for video to resume
					UpdateWaitVideo();
					break;
				case 5: // Seek mode
					UpdateSeek();
					break;
				case 6: // Wait for video to load
					UpdateWaitLoad();
					break;
				case 7: // Wait for time to play video
					UpdateWaitPlay();
					break;
				case 8: // Wait for time to pause video
					UpdateWaitPause();
					break;
				case 9: // Wait for new data
					UpdateWaitData();
					break;
				default: // Default to normal
					UpdateNormal();
					break;
			}
		}

		private void UpdateNormal() {
			if (Mathf.Abs(drift) > ResyncThreshold) {
				syncMode = 1;
				UpdateResync();
			}

			if (drift > DriftTolerance) {
				syncMode = 3;
				UpdateWaitSync();
			}

			if (drift < -DriftTolerance) {
				syncMode = 2;
				UpdateCatchUp();
			}
		}

		// ReSharper disable Unity.PerformanceAnalysis
		private void UpdateCatchUp() {
			if (nextResync > Time.time) return;

			if (Mathf.Abs(RawTime - timeAtLastResync) < ResyncDetect) {
				if (Time.time - lastResync < ResyncTimeout) return;
				SetSyncMode(4);
				return;
			}

			if (drift > -DriftTolerance) {
				SetSyncMode(0, true);
				return;
			}

			ResyncTo(Time.time - beginTime + HotSpoolTime, DriftCooldown);
		}

		private void UpdateWaitSync() {
			if (nextResync > Time.time) return;

			playerManager._GetPlaying(identifier);

			if (drift < DriftTolerance) {
				SetSyncMode(0, true);
				return;
			}

			if (drift > HotSpoolTime) {
				EnsurePlaying(false);
				return;
			}

			ResyncTo(Time.time - beginTime + HotSpoolTime, DriftCooldown);
		}

		// ReSharper disable Unity.PerformanceAnalysis
		private void UpdateResync() {
			if (nextResync > Time.time) return;
			if (CheckResync()) return;
			if (CheckDrift(ResyncThreshold)) return;

			ResyncTo(Time.time - beginTime + ColdSpoolTime, ResyncCooldown);
		}

		private void UpdateWaitVideo() {
			if (CheckResync()) return;
			if (Time.time - lastResync < ReloadTimeout) return;

			// TODO: Initiate reload
		}

		private void UpdateWaitLoad() {
			// TODO: Wait for video to load
		}

		// ReSharper disable Unity.PerformanceAnalysis
		private void UpdateSeek() {
			if (SeekCooldown > Time.time - lastResync) return;
			if (CheckResync()) return;
			if (CheckDrift(ResyncThreshold)) return;

			ResyncTo(Time.time - beginTime + ColdSpoolTime, ResyncCooldown);
		}

		private void UpdateWaitPlay() {
			float rawTime = RawTime;
			float currentTime = CurrentTime;

			if (rawTime > currentTime + HotSpoolTime) return;

			if (rawTime < currentTime) {
				playerManager._PlayVideo(identifier);
				ResyncTo(currentTime + HotSpoolTime, DriftCooldown);
				SetSyncMode(2, true);
				return;
			}

			playerManager._PlayVideo(identifier);
			ResyncTo(currentTime + HotSpoolTime, DriftCooldown);
			SetSyncMode(3);
		}

		private void UpdateWaitPause() {
			if (RawTime < PauseTime) return;
			playerManager._PauseVideo(identifier);
			SetSyncMode(0);
		}

		/// <summary>
		/// This mode will be active after video player automatically loops,
		/// goes on to the next video, or anything else that needs to await a
		/// resync of data before a new reference can be acquired from the
		/// current owner.
		/// </summary>
		private void UpdateWaitData() {
			//
		}

		// ReSharper disable Unity.PerformanceAnalysis
		private bool CheckResync() {
			if (Mathf.Abs(RawTime - timeAtLastResync) > ResyncDetect) return false;
			if (Time.time - lastResync < ResyncTimeout) return true;
			SetSyncMode(4);
			return true;
		}

		// ReSharper disable Unity.PerformanceAnalysis
		private bool CheckDrift(float _threshold) {
			if (Mathf.Abs(drift) > _threshold) return false;
			SetSyncMode(0, true);
			return true;
		}

		private bool EnsurePlaying(bool _playing) {
			if (playerManager._GetPlaying(identifier) == _playing) return true;
			if (_playing) playerManager._PlayVideo(identifier);
			else playerManager._PauseVideo(identifier);
			return false;
		}

		private void SetSyncMode(int _mode, bool _callNormal = false) {
			string modeName = _mode >= syncModeNames.Length || _mode < 0 ? "Unknown" : syncModeNames[_mode];
			Log($"Setting sync mode to: {modeName} ({_mode})");
			syncMode = _mode;
			if (!_callNormal) return;
			UpdateNormal();
		}

		private void ResyncTo(float _time, float _cooldown) {
			Log("Resync to " + _time.ToString("N2"));
			playerManager._SeekTo(identifier, _time);
			playerManager._PlayVideo(identifier);
			lastResync = Time.time;
			timeAtLastResync = RawTime;
			nextResync = lastResync + _cooldown;
		}
		#endregion

		#region Network Sync
		private void Sync() {
			Log("Sync!");
			if (IsEditor) {
				ApplySyncData();
				return;
			}
			Networking.SetOwner(Networking.LocalPlayer, gameObject);
			RequestSerialization();
		}

		public override void OnDeserialization() {
			Log("On Deserialization");
			ApplySyncData();
		}

		public override void OnPostSerialization(SerializationResult _result) {
			if (!_result.success) {
				LogWarning("Failed to serialize, byte count:" + _result.byteCount);
				return;
			}
			Log($"Successfully serialized {_result.byteCount} bytes");
			ApplySyncData();
		}

		private void ApplySyncData() {
			// Check what's changed
			bool cPaused = paused != pausedSync;
			bool cBeginNetTime = beginNetTime != beginNetTimeSync;
			bool cPauseTime = Math.Abs(pauseTime - pauseTimeSync) > 0.1f;
			bool cLocked = isLocked != isLockedSync;
			bool cMediaType = mediaType != mediaTypeSync;
			bool cSyncIndex = syncIndex != syncIndexSync;

			// Copy changed properties
			paused = pausedSync;
			beginNetTime = beginNetTimeSync;
			pauseTime = pauseTimeSync;
			isLocked = isLockedSync;
			mediaType = mediaTypeSync;
			syncIndex = syncIndexSync;

			// TODO: Determine what to do when variables change
		}
		#endregion

		#region Callbacks
		private void CallCallbacks(string _message) {
			if (callbacks == null) return;
			Log($"Calling callbacks with method \"{_message}\"");
			foreach (UdonSharpBehaviour callback in callbacks)
				callback.SendCustomEvent(_message);
		}
		#endregion
	}
}
