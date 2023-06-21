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

	/// <summary>
	/// Modes of operation for an instance of a media player in SMP
	/// </summary>
	public enum PlayerState {
		Playing, Resync, CatchUp, WaitForSync, WaitForVideo, Seek, WaitForLoad,
		WaitToPlay, WaitToPause, WaitForData, Idle
	}

	/// <summary>
	/// Implied metadata generated about each link.
	/// <list type="table">
	///  <item>
	///   <term>Video</term>
	///   <description>
	///    Link can be a video with a fixed length and not live
	///   </description>
	///  </item>
	///  <item>
	///   <term>Stream</term>
	///   <description>
	///    Link can be a live broadcast
	///   </description>
	///  </item>
	///  <item>
	///   <term>LowLatency</term>
	///   <description>
	///    A low latency protocol was detected and we can skip the YouTube-DL
	///    link resolver
	///   </description>
	///  </item>
	///  <item>
	///   <term>AudioOnly</term>
	///   <description>
	///    Link is typically played without a picture
	///   </description>
	///  </item>
	/// </list>
	/// </summary>
	[Flags] public enum LinkType {
		None = 0x0, Video = 0x1, Stream = 0x2, LowLatency = 0x4, AudioOnly = 0x8
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

		#region Network Sync Variables
		private bool paused;
		[UdonSynced] private bool pausedSync;
		private float pauseTime;
		[UdonSynced] private float pauseTimeSync;
		private float beginTime;
		private int beginNetTime;
		[UdonSynced] private int beginNetTimeSync;
		private MediaType mediaType;
		[UdonSynced] private MediaType mediaTypeSync;
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
		private PlayerState playerState;
		private const float CheckCooldown = 0.1f;
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
		private const float BacktrackOnResume = 1.0f;
		private const float ResumeGracePeriod = 2.0f;
		private float drift;
		private float nextResync = -1;
		private float lastResync = -1;
		private float timeAtLastResync = -1;
		private float lastCheck = -CheckCooldown;
		#endregion

		#region Video Playlist Data
		private int currentPlaylistType;
		private int currentPlaylist;
		private int currentVideo;
		private string preferredVariant;
		#endregion

		#region Current Video Data
		private string currentVideoName;
		private string currentVideoFriendlyName;
		private VRCUrl currentVideoLink;
		private LinkType linkType;
		private bool playImmediately;
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

		private const int LinkVideoBit = (int)LinkType.Video;
		private const int LinkStreamBit = (int)LinkType.Stream;
		private const int LinkLowLatencyBit = (int)LinkType.LowLatency;
		private const int LinkAudioOnlyBit = (int)LinkType.AudioOnly;
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
				isLockedSync = value;
				Log($"{(value ? "Locking" : "Unlocking")} the player");
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

		public float CurrentTime {
			get => IsReady ? Paused ? PauseTime : Time.time - beginTime : 0;
			set {
				if (!IsReady || Math.Abs(value - (Paused ? PauseTime : Time.time - beginTime)) < 0.001f) return;
				if (!CheckValidAndAccess("seek")) return;
				Log("Seeking");
				TranslatedBeginNetTime = Time.time - value;
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

		private float TranslatedBeginNetTime {
			set {
				int differenceInMilliseconds = Mathf.FloorToInt((Time.time - value) * 1000);
				BeginNetTime = Networking.GetServerTimeInMilliseconds() - differenceInMilliseconds;
			}
			get {
				int differenceInMilliseconds = Networking.GetServerTimeInMilliseconds() - BeginNetTime;
				float translatedTime = Time.time - differenceInMilliseconds * 0.001f;
				if (differenceInMilliseconds > 0) translatedTime -= uint.MaxValue * 0.001f;
				return translatedTime;
			}
		}

		private MediaType MediaType {
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
		public float RawTime => IsReady ? MediaTime : 0;
		public bool Playing => !Paused;
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

			VideoManager = playerManager.GetVideoManager();
			if (VideoManager == null) {
				LogError("Video manager is missing!");
				return;
			}

			if (!RegisterWithVideoManager(playerName)) {
				LogError("Failed to register with video manager!");
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

			// This needs to be set to a reasonable state before data comes in
			isLocked = lockByDefault && securityManager.HasSecurity;
			playerState = PlayerState.Idle;
			isValid = true;
		}
		#endregion

		#region Public Methods
		/// <summary>
		/// Loads a video by the link
		/// </summary>
		/// <param name="_link">URL of the video</param>
		/// <param name="_playImmediately">Set to true to play as soon as video loads</param>
		public void _LoadVideo(VRCUrl _link, bool _playImmediately = false) {
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

		/// <summary>
		/// Loads a video from the playlists
		/// </summary>
		/// <param name="_listType">The type of playlist we're accessing, since they're accessed differently</param>
		/// <param name="_listId">ID of the playlist we're accessing</param>
		/// <param name="_videoIdx">Index of the video in the playlist</param>
		/// <param name="_variant">Variant of the video we'd like to load. Example: sub, dub</param>
		/// <param name="_playImmediately">Set to true to play as soon as video loads</param>
		public void _LoadFromPlaylist(int _listType, int _listId, int _videoIdx, string _variant = null, bool _playImmediately = false) {
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
			if (_variant != null) preferredVariant = _variant;
			currentVideoName = foundVideoName;
			currentVideoFriendlyName = foundVideoShortName;

			LoadInternal(foundVideoLink, _playImmediately);
		}

		public void _Play() {
			if (!CheckValidAndAccess("play")) return;
			if (!CheckHasVideo()) return;

			Log("Playing");
			Paused = false;
			PlayMedia();
		}

		public void _Pause() {
			if (!CheckValidAndAccess("pause")) return;
			if (!CheckHasVideo()) return;

			Log("Pausing");
			Paused = true;
			PauseMedia();
		}

		public void _Stop() {
			if (!CheckValidAndAccess("stop")) return;
			if (!CheckHasVideo()) return;

			Log("Stopping");
			Paused = true;
			// TODO: Set time to 0
			StopMedia();
		}

		private bool CheckHasVideo() {
			// TODO: Do this a little better
			bool hasVideo = VideoManager._HasVideo(MediaControllerId);
			if (!hasVideo) LogWarning("No video!");
			return hasVideo;
		}

		private bool CheckHasQueuedVideo() {
			// TODO: Do this a little better
			bool hasQueuedVideo = VideoManager._HasQueuedVideo(MediaControllerId);
			if (!hasQueuedVideo) LogWarning("No queued video!");
			return hasQueuedVideo;
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

			// Needs internal variable
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

			// Needs internal variable
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

		public void _CheckQueue() {
			Initialize();
			if (!isValid) return;

			Log("Checking queue");

			if (SyncIndex != queue.SyncIndex) {
				playerState = PlayerState.WaitForData;
				Log("Waiting for new queue video");
				return;
			}

			VRCUrl link = queue.CurrentVideo;
			if (Equals(link, currentVideoLink)) {
				if (playerState == PlayerState.WaitForData) {
					playerState = PlayerState.Playing;
					Log("Video already in player!");
				}
				return;
			}

			Log("New link pulled from queue: " + (link ?? VRCUrl.Empty).ToString());
			currentVideoLink = link;
			// ReSharper disable once LocalVariableHidesMember
			int linkType = (int)this.linkType;
			bool canBeVideo = (linkType | LinkVideoBit) > 0;
			bool isLowLatency = (linkType | LinkLowLatencyBit) > 0;
			VideoType videoType = canBeVideo ? VideoType.Video : isLowLatency ? VideoType.LowLatency : VideoType.Stream;
			LoadMedia(currentVideoLink, videoType, playImmediately);
			playerState = PlayerState.WaitForLoad;
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
			bool linkValid = CheckLink(_link, true, out linkType, out string error);
			if (!linkValid) {
				LogError("Cannot load link: " + error);
				return;
			}

			queue._SetCurrentVideo(_link);
			SyncIndex++;
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

		private bool CheckLink(VRCUrl _link, bool _checkType, out LinkType _linkType, out string _error) {
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

			// ReSharper disable once LocalVariableHidesMember
			int linkType = (int)LinkType.None;
			if (Array.IndexOf(videoHosts, videoHost) >= 0) linkType |= LinkVideoBit;
			if (Array.IndexOf(streamHosts, videoHost) >= 0) linkType |= LinkStreamBit;
			if (Array.IndexOf(audioOnlyHosts, videoHost) >= 0) linkType |= LinkAudioOnlyBit;
			if (Array.IndexOf(lowLatencyProtocols, videoProtocol) >= 0) linkType |= LinkStreamBit | LinkLowLatencyBit;
			_linkType = (LinkType)linkType;

			Log($"Link Type: {_linkType}");

			return true;
		}

		#region Video Sync
		private void Update() {
			_UpdateSync();
		}

		public void _UpdateSync() {
			if (!IsReady || (Paused && playerState == 0)) return;
			drift = RawTime - CurrentTime;
			switch (playerState) {
				case PlayerState.Resync:
					UpdateResync();
					break;
				case PlayerState.CatchUp:
					UpdateCatchUp();
					break;
				case PlayerState.WaitForSync:
					UpdateWaitSync();
					break;
				case PlayerState.WaitForVideo:
					UpdateWaitVideo();
					break;
				case PlayerState.Seek:
					UpdateSeek();
					break;
				case PlayerState.WaitForLoad:
					UpdateWaitLoad();
					break;
				case PlayerState.WaitToPlay:
					UpdateWaitPlay();
					break;
				case PlayerState.WaitToPause:
					UpdateWaitPause();
					break;
				case PlayerState.WaitForData:
					UpdateWaitData();
					break;
				case PlayerState.Playing:
					UpdatePlaying();
					break;
				case PlayerState.Idle:
					break;
				default:
					playerState = PlayerState.Playing;
					break;
			}
		}

		private bool WaitForCooldown() {
			if (lastCheck + CheckCooldown > Time.time) return true;
			lastCheck = Time.time;
			return false;
		}

		private void UpdatePlaying() {
			if (WaitForCooldown()) return;

			if (Mathf.Abs(drift) > ResyncThreshold) {
				playerState = PlayerState.Resync;
				UpdateResync();
			}

			if (drift > DriftTolerance) {
				playerState = PlayerState.WaitForSync;
				UpdateWaitSync();
			}

			if (drift < -DriftTolerance) {
				playerState = PlayerState.CatchUp;
				UpdateCatchUp();
			}
		}

		// ReSharper disable Unity.PerformanceAnalysis
		private void UpdateCatchUp() {
			if (nextResync > Time.time) return;

			if (Mathf.Abs(RawTime - timeAtLastResync) < ResyncDetect) {
				if (Time.time - lastResync < ResyncTimeout) return;
				SetSyncMode(PlayerState.WaitForVideo);
				return;
			}

			if (drift > -DriftTolerance) {
				SetSyncMode(PlayerState.Playing, true);
				return;
			}

			ResyncTo(Time.time - beginTime + HotSpoolTime, DriftCooldown);
		}

		private void UpdateWaitSync() {
			if (nextResync > Time.time) return;

			// TODO: Get Playing status and figure out what I wanted to do with it

			if (drift < DriftTolerance) {
				SetSyncMode(PlayerState.Playing, true);
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
			// TODO: Should we send a seek event?
		}

		private void UpdateWaitPlay() {
			float rawTime = RawTime;
			float currentTime = CurrentTime;

			if (rawTime > currentTime + HotSpoolTime) return;

			if (rawTime < currentTime) {
				PlayMedia();
				ResyncTo(currentTime + HotSpoolTime, DriftCooldown);
				SetSyncMode(PlayerState.CatchUp, true);
				return;
			}

			PlayMedia();
			ResyncTo(currentTime + HotSpoolTime, DriftCooldown);
			SetSyncMode(PlayerState.WaitForSync);
			// TODO: Send callback for play
		}

		private void UpdateWaitPause() {
			if (RawTime < PauseTime) return;
			PauseMedia();
			SetSyncMode(PlayerState.Playing);
			// TODO: Send callback for pause
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
			SetSyncMode(PlayerState.WaitForVideo);
			return true;
		}

		// ReSharper disable Unity.PerformanceAnalysis
		private bool CheckDrift(float _threshold) {
			if (Mathf.Abs(drift) > _threshold) return false;
			SetSyncMode(PlayerState.Playing, true);
			return true;
		}

		private bool EnsurePlaying(bool _playing) {
			if (Playing == _playing) return true;
			if (_playing) PlayMedia();
			else PauseMedia();
			return false;
		}

		private void SetSyncMode(PlayerState _mode, bool _callNormal = false) {
			Log($"Setting sync mode to: {GetResyncModeName(_mode)} ({(int)_mode})");
			playerState = _mode;
			if (!_callNormal) return;
			UpdatePlaying();
		}

		private string GetResyncModeName(PlayerState _mode) {
			return (int)_mode >= syncModeNames.Length ? "Unknown" : syncModeNames[(int)_mode];
		}

		private void ResyncTo(float _time, float _cooldown) {
			Log("Resync to " + _time.ToString("N2"));
			MediaTime = _time;
			PlayMedia();
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

		public override void OnDeserialization(DeserializationResult _result) {
			Log($"Received new data. Data was in flight for {_result.receiveTime - _result.sendTime:N2} seconds.");
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

			int updatedSyncVars = 0;
			if (cPaused) updatedSyncVars++;
			if (cBeginNetTime) updatedSyncVars++;
			if (cPauseTime) updatedSyncVars++;
			if (cLocked) updatedSyncVars++;
			if (cMediaType) updatedSyncVars++;
			if (cSyncIndex) updatedSyncVars++;

			if (updatedSyncVars == 0) {
				Log("Ignoring unchanged data");
				return;
			}

			if (DiagnosticLogEnabled) {
				string[] updatedSyncDataNames = new string[updatedSyncVars];
				int dataIndex = 0;
				if (cPaused) updatedSyncDataNames[dataIndex++] = "Pause";
				if (cBeginNetTime) updatedSyncDataNames[dataIndex++] = "Begin Net Time";
				if (cPauseTime) updatedSyncDataNames[dataIndex++] = "Pause Time";
				if (cLocked) updatedSyncDataNames[dataIndex++] = "Lock";
				if (cMediaType) updatedSyncDataNames[dataIndex++] = "Media Type";
				if (cSyncIndex) updatedSyncDataNames[dataIndex] = "Sync Index";

				Log($"Applying sync data: {string.Join(", ", updatedSyncDataNames)}");
			}

			// TODO: Determine what to do when variables change
			if (cSyncIndex) _CheckQueue();
			if (cPaused && playerState != PlayerState.WaitForData) {
				if (paused) {
					DirectPauseVideo();
				} else {
					DirectPlayVideo();
				}
			}

			if (cLocked) SendVideoCallback(isLocked ? CallbackEvent.PlayerLocked : CallbackEvent.PlayerUnlocked);

			if (cPauseTime && paused) {
				// Seek to a different position and respool video
				DirectSeekPaused();
			}

			if (cBeginNetTime && !cSyncIndex) {
				// Probably a seek
				DirectSeekVideo();
			}

			if (cMediaType) {
				// Change how we think about current media, possibly reload
				DirectChangeMediaType();
			}
		}
		#endregion

		#region Induced Direct Methods
		private void DirectPlayVideo() {
			// TODO: Play video
			playerState = PlayerState.WaitToPlay;
		}

		private void DirectPauseVideo() {
			PauseMedia();
		}

		private void DirectSeekPaused() {
			MediaTime = PauseTime;
		}

		private void DirectSeekVideo() {
			beginTime = TranslatedBeginNetTime;
			playerState = PlayerState.Seek;
		}

		private void DirectChangeMediaType() {
			// TODO: Determine whether we need to reload media
		}
		#endregion

		#region Callbacks
		private void CallCallbacks(string _message) {
			if (callbacks == null) return;
			Log($"Calling callbacks with method \"{_message}\"");
			foreach (UdonSharpBehaviour callback in callbacks)
				callback.SendCustomEvent(_message);
		}

		protected override void MediaLoading() {
			Log("Media Loading Callback");
		}

		protected override void QueuedMediaLoading() {
			Log("Queued Media Loading Callback");
		}

		protected override void MediaReady() {
			Log("Media Ready Callback");
		}

		protected override void QueuedMediaReady() {
			Log("Queued Media Ready Callback");
		}

		#endregion
	}
}
