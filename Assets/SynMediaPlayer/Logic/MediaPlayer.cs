
using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDK3.Components.Video;
using VRC.SDKBase;
using VRC.Udon;
using Synergiance.MediaPlayer.UI;
using VRC.Udon.Common.Interfaces;

namespace Synergiance.MediaPlayer {
	[DefaultExecutionOrder(100)]
	public class MediaPlayer : UdonSharpBehaviour {
		[Header("Objects")] // Objects and components
		[SerializeField]  private VideoInterpolator  mediaPlayers;                  // Object to reference all your different media players.  Needs to correspond with media select
		[SerializeField]  private SeekControl        seekBar;                       // Seek bar object.  Needs to be normalized
		[SerializeField]  private Text               statisticsText;                // Text for video stats, will read out video player status
		[SerializeField]  private Text               diagnosticsText;               // Text field for diagnosing video player

		[Header("Timings")] // Timings
		[SerializeField]  private float              syncPeriod = 1.0f;             // This is an internal value used in network to local time conversion.
		[SerializeField]  private float              loadTime = 5.0f;               // This is how much time the video player will expect media to take to load.  Recommended 5s
		[SerializeField]  private float              deviationTolerance = 0.5f;     // If video player somehow differs from the synced time by this amount it will resync locally. (Higher values cause fewer resyncs)
		[SerializeField]  private float              checkSyncEvery = 1.0f;         // This value keeps the time keep method from running too often.  Higher values could lead to errors, lower values could lead to high CPU usage.
		[SerializeField]  private float              videoOvershoot = 0.25f;        // This value helps the video player begin playing at the right spot after seeking.  I advice not to alter this.
		[SerializeField]  private float              resyncEvery = 5.0f;            // This value is how often video player compares local time reference to network time constant.
		[SerializeField]  private float              seekPeriod = 2.5f;             // This is how much time the video player will wait before resuming a video.  High values could cause others to skip.
		[SerializeField]  private float              pauseResyncFor = 2.0f;         // This value prevents the video player from being too aggressive on resync.  Lower values could cause issues on loading videos or seeking.
		[SerializeField]  private float              preloadNextVideoTime = 15.0f;  // How much time before the current video ends to start loading the next video to get ready for gapless switch.

		[Header("Settings")] // Settings
		[SerializeField]  private bool               enableDebug;                   // If toggled off, all info level messages are disabled
		[SerializeField]  private bool               verboseDebug;                  // If toggled on, low level verbose log messages are shown
		[SerializeField]  private bool               automaticRetry = true;         // If toggled on, video player will automatically retry on failed load
		[SerializeField]  private int                numberOfRetries = 3;           // Number of automatic retries to attempt
		[SerializeField]  private bool               allowRetryWhenLoaded;          // If toggled off, retry won't do anything if the video is successfully loaded
		[SerializeField]  private UdonSharpBehaviour callback;                      // Used for sending events
		[SerializeField]  private string             setStatusMethod;               // Method name for status update events
		[SerializeField]  private string             statusProperty = "statusText"; // Property name for status update events
		[SerializeField]  private bool               startActive = true;            // If toggled off, videos won't load or sync until locally set to active
		[SerializeField]  private bool               playOnNewVideo;                // Determines whether a new video will start playing when it loads
		
		[Header("Security")] // Security
		[SerializeField]  private bool               masterCanLock = true;          // If toggled on, instance master will be able to lock and unlock the player
		[SerializeField]  private bool               ownerCanLock = true;           // If toggled on, instance owner will be able to lock and unlock the player
		[SerializeField]  private string[]           moderators;                    // List of players who are always allowed to unlock and unlock the player
		[SerializeField]  private bool               lockByDefault;                 // If on, video player will be locked by default

		// Public Callback Variables
		[HideInInspector] public  int                relayIdentifier;               // Unused value for compatibility.
		[HideInInspector] public  VideoError         relayVideoError;               // Video error identified, copied from video player.
		[HideInInspector] public  float              seekVal;                       // Normalized seek bar value.  Used when seeking.

		// Sync Variables
		[UdonSynced]      private float              remoteTime;                    // Network time based reference for when video began.
		[UdonSynced]      private bool               remoteIsPlaying;               // Network sync boolean for whether video is playing.
		[UdonSynced]      private VRCUrl             remoteURL = VRCUrl.Empty;      // Network sync URL of a video.
		[UdonSynced]      private VRCUrl             remoteQueueURL = VRCUrl.Empty; // Network sync URL of queue video
		[UdonSynced]      private int                remotePlayerID;                // Network sync media type.
		[UdonSynced]      private bool               remoteLock;                    // Network sync player lock.
		[UdonSynced]      private bool               remoteLooping;                 // Network sync player lock.
		[UdonSynced]      private bool               remoteQueueNow;                // Network sync play queue video now
		[UdonSynced]      private float              remoteQueueTime;               // Network sync play queue video time
		                  private float              localTime;                     // Local variables are used for local use of network variables.
		                  private bool               localIsPlaying;                // Before use, they will be checked against for changes.
		                  private VRCUrl             localURL = VRCUrl.Empty;       // Upon change, the proper methods will be called.
		                  private VRCUrl             localQueueURL = VRCUrl.Empty;  //
		                  private int                localPlayerID;                 //
		                  private bool               localLock;                     //
		                  private bool               localLooping;                  //
		                  private bool               localQueueNow;                 //
		                  private float              localQueueTime;                //

		// Player Variables
		private float    startTime;                      // Local time reference for when video began.
		private float    pausedTime;                     // Video time at which a video is paused.
		private float    referencePlayhead;              // Calculated time point at which a video should be.
		private float    deviation;                      // Difference between video player time and reference time.
		private float    lastCheckTime;                  // Last time a video time has been checked against reference
		private float    lastResyncTime;                 // Last time local reference time was calculated from network reference time.
		private float    postResyncEndsAt;               // Stores the time at which we disable post resync mode
		private bool     isPlaying;                      // This determines whether a video should playing or paused.  This doesn't necessarily reflect the actual state of the video player, as it could be paused temporarily for a resync.
		private bool     isLowLatency;                   // Reference variable for whether we're using an AVPro player in Low Latency mode
		private bool     isStream;                       // Reference variable for whether we're playing a stream
		private VRCUrl   currentURL;                     // URL of currently loaded video
		private VRCUrl   queueURL;                       // URL of video set to load seemlessly after current
		private string   playerStatus      = "No Video"; // Player status string to print to status label
		private bool     urlValid;                       // Stores state of whether a video has been determined to be a valid video.
		private bool     playerReady;                    // Stores state of whether the video player is ready to play.
		private bool     isEditor;                       // Stores state of whether we're viewing world from the editor.  Disables AVPro if this is set.
		private bool     isLoading;                      // Stores state of when we're loading a video.
		private bool     playFromBeginning = true;       // Determines whether playing time should be reset to 0 when _Play is called.
		private float    playerTimeAtSeek;               // Stores video player time at time of seek to determine whether seek has occurred.
		private float    lastSeekTime;                   // Last time the video has been seeked.
		private bool     isSeeking;                      // Stores state of whether video player is in the process of seeking.
		private float    resyncPauseAt;                  // Stores time of last event that needs to disable resync.
		private float    playerTimeAtResync;             // Stores video player time at time of resync to determine whether resync has occurred.
		private float    lastSoftSyncTime;               // Last time the video player has compared network time constant to local time constant.
		private bool     waitForNextNetworkSync;         // Stores whether the resync routine should wait for the next network sync before adjusting.
		private bool     isResync;                       // Stores whether player is in the process of a resync.
		private bool     postResync;                     // Stores whether we're in the post resync state
		private bool     queueVideoReady;                // Stores state of queued video
		private bool     queueVideoLoading;              // Stores state of whether queued video is loading
		private bool     playQueueVideoNow;              // Stores whether we're trying to load queue video ASAP
		private float    playQueueVideoTime;             // Stores the time at which we will load the queue video
		private bool     isReloadingVideo;               // Stores state of whether the video is reloading.  Used for forcing the video to actually reload.
		private float    lastVideoLoadTime;              // Stores the last time a video was loaded, to prevent any issues loading new videos
		private int      retryCount;                     // Stores number of automatic retries that have happened
		private bool     isAutomaticRetry;               // Stores whether this is an automatic retry
		private bool     isPreparingForLoad;             // Stores whether we're preparing to enter a URL and to suppress other video loads
		private bool     newVideoLoading;                // Stores whether the video we're loading is a new video
		private bool     isWakingUp;                     // Stores whether the video player is initializing or coming out of inactive state
		private bool     isBlackingOut;                  // Stores local variable for whether we're blacking out the video player

		private bool     masterLock;                     // Stores state of whether the player is locked
		private bool     hasPermissions;                 // Cached value for whether the local user has permissions
		private bool     suppressSecurity;               // Stores whether we're suppressing security

		private bool     isLoggingDiagnostics;           // Stores whether we're taking diagnostic data
		private string[] diagnosticLog;                  // String to catch all our diagnostic data and output to the user
		private float    diagnosticEnd;                  // Time at which diagnostics will end
		private string   diagnosticStr;                  // String to contain diagnostic variables each capture
		private float    lastDiagnosticsUpdate;          // Stores time of last diagnostics display update
		private float    lastDiagnosticsLog;             // Stores time of last diagnostics log
		private int      currentDiagLog;                 // Stores index of current log
		private int      currentDiagUpdate;              // Stores how many updates we've added to the current log

		private bool     hasStatsText;                   // Cached value for whether statisticstext exists
		private bool     hasCallback;                    // Cached value for whether callback exists
		private bool     setStatusEnabled;               // Cached value for whether status can be set without error
		private bool     isActive;                       // Value for whether media player is active or not. Videos will only load/play/sync while the player is active
		private bool     initialized;                    // Value indicating whether this component has initialized or not.
		private bool     hasActivated;                   // Value for whether player has activated for the first time
		
		private float    lastActivePing;                 // Time of last ping for who's active
		private int      numActivePlayers;               // Number of active players
		private int      numActivePlayersTmp;            // Number of replies to ping so far
		private int      numReadyPlayers;                // Number of players who have the video loaded

		// Video Checking
		private string[] videoHosts          = {
			"drive.google.com", "twitter.com", "vimeo.com",
			"youku.com", "tiktok.com", "nicovideo.jp", "facebook.com"
		};
		private string[] videoStreamHosts    = { "youtu.be", "youtube.com", "www.youtube.com" };
		private string[] streamHosts         = { "twitch.tv" };
		private string[] musicHosts          = { "soundcloud.com" };
		private string[] videoProtocols      = { "http", "https", "rtmp", "rtsp", "rtspt", "rtspu", "file", "ftp", "gopher", "telnet", "data" };
		private string[] lowLatencyProtocols = { "rtsp", "rtspt", "rtspu" };

		private string debugPrefix         = "[<color=#DF004F>SynMediaPlayer</color>] ";

		private float diagnosticsUpdatePeriod = 0.1f; // Update period of diagnostic display
		private float diagnosticPeriod = 20;          // Period of diagnostic log
		private int   diagnosticUpdatesPerLog = 5;    // Number of logs per message output
		private float diagnosticDelay = 0.25f;        // Delay between diagnostic logs

		private float pingActiveEvery = 120.0f;       // How often to ping for who's active
		private float holdPingOpenFor = 5.0f;         // How long to wait after pinging to update player activity data

		private float activateAfter = 0.5f;           // Amount of time to wait after loading a world to activate player

		private float videoLoadCooldown = 5.5f;       // Minimum delay between attempted video loads

		private void Start() {
			Initialize();
		}

		private void Initialize() {
			if (initialized) return;
			isWakingUp = true;
			Log("Initializing", this);
			hasCallback = callback != null;
			hasStatsText = statisticsText != null;
			SetActiveInternal(false);
			masterLock = lockByDefault;
			setStatusEnabled = callback && !string.IsNullOrWhiteSpace(setStatusMethod) && !string.IsNullOrWhiteSpace(statusProperty);
			if (Networking.LocalPlayer == null) isEditor = true;
			hasPermissions = CheckPrivilegedInternal(Networking.LocalPlayer);
			if (Networking.IsOwner(gameObject)) SetLockState(masterLock);
			isBlackingOut = mediaPlayers.BlackOutPlayer;
			mediaPlayers.BlackOutPlayer = true;
			initialized = true;
			SetPlayerStatusText("Initializing");
			UpdateStatus();
			SendCustomEventDelayedSeconds("_Activate", activateAfter);
		}

		// Activate for the first time
		public void _Activate() {
			if (hasActivated) return;
			Log("First Activation", this);
			SetActiveInternal(startActive);
			if (masterLock && !isEditor && hasPermissions) VerifyProperOwnership();
			mediaPlayers.BlackOutPlayer = isBlackingOut;
			if (!isActive) {
				// Need to set inactive status here since the update method will be disabled
				SetPlayerStatusText("Inactive");
				UpdateStatus();
			} else {
				// Make sure initial status is shown
				SendStatusCallback();
			}
			CheckDeserializedData();
			if (hasCallback) callback.SendCustomEvent("_Activate");
			hasActivated = true;
		}

		private void Update() {
			if (isActive) {
				UpdateVideoPlayer();
				UpdateSeek();
			}
			UpdateStatus();
			UpdateDiagnostics();
		}

		// ---------------------- UI Methods ----------------------

		public int GetUrlId(string url, int currentID) {
			return CheckURL(url, currentID);
		}

		public void _LoadURL(VRCUrl url) {
			Initialize();
			if (!isActive) return;
			if (masterLock && !hasPermissions && !suppressSecurity) return;
			int playerID = mediaPlayers.ActivePlayerID;
			_LoadURLAs(url, playerID);
		}

		public int _LoadURLAs(VRCUrl url, int playerID) {
			Initialize();
			if (!isActive) return playerID;
			if (masterLock && !hasPermissions && !suppressSecurity) return playerID;
			Log("_Load", this);
			// Sanity Check URL
			string urlStr = url != null ? url.ToString() : "";
			if (!SanityCheckURL(urlStr)) return mediaPlayers.ActivePlayerID; 
			// Sync new values over the network
			int correctedID = isEditor ? 0 : CheckURL(urlStr, playerID);
			if (correctedID < 0) return mediaPlayers.ActivePlayerID;
			isPreparingForLoad = true; // Suppress reloads
			SwitchPlayer(correctedID);
			SetURL(url);
			isStream = mediaPlayers.IsStream;
			return correctedID;
		}

		public void _LoadQueueURL(VRCUrl url) {
			Initialize();
			LogVerbose("Load Queue URL", this);
			if (!isActive) return;
			if (masterLock && !hasPermissions && !suppressSecurity) return;
			Log("Load Queued", this);
			string urlStr = url != null ? url.ToString() : "";
			if (string.IsNullOrEmpty(urlStr)) {
				SetQueueURL(url);
				return;
			}
			if (!SanityCheckURL(urlStr)) return;
			SetQueueURL(url);
		}

		// Attempt to load video again
		public void _Retry() {
			Initialize();
			if (!isActive) return;
			Log("_Retry", this);
			if (!allowRetryWhenLoaded && urlValid) {
				Log("Video is already successfully loaded", this);
				return;
			}
			ReloadVideoInternal();
		}

		public void _PlayNext() {
			Initialize();
			if (!isActive) return;
			if (masterLock && !hasPermissions && !suppressSecurity) return;
			Log("_PlayNext", this);
			PlayQueueNow();
		}

		// Call _Play if paused and _Pause if playing
		public void _PlayPause() {
			Initialize();
			bool playing = !isPlaying;
			Log("_PlayPause: " + (playing ? "True" : "False"), this);
			if (playing) _Play();
			else _Pause();
		}

		// Play a the video if possible.
		public void _Play() {
			Initialize();
			if (!isActive) return;
			if (masterLock && !hasPermissions && !suppressSecurity) return;
			Log("_Play", this);
			SetPlaying(true);
			if (!playerReady) return;
			if (playFromBeginning) { // This variable is set from video stop or video end, reset it when used
				playFromBeginning = false;
				_SeekTo(0);
			}
		}

		// Pause a video if possible
		public void _Pause() {
			Initialize();
			if (!isActive) return;
			if (masterLock && !hasPermissions && !suppressSecurity) return;
			Log("_Pause", this);
			SetPlaying(false);
		}

		// Play a video from the beginning if possible
		public void _Start() {
			Initialize();
			if (!isActive) return;
			if (masterLock && !hasPermissions) return;
			Log("_Start", this);
			SetPlaying(true);
			if (playerReady) _SeekTo(0);
			else playFromBeginning = true;
		}

		// Stop a currently playing video and unload it
		public void _Stop() {
			Initialize();
			if (!isActive) return;
			if (masterLock && !hasPermissions && !suppressSecurity) return;
			Log("_Stop", this);
			SeekTo(0);
			SetPlaying(false);
			playFromBeginning = true;
			//SetURL(VRCUrl.Empty);
		}

		// Reinterpret master time to resync video
		public void _Resync() {
			Initialize();
			if (!isActive) return;
			Log("_Resync", this);
			if (!Networking.IsOwner(gameObject)) {
				SendCustomNetworkEvent(NetworkEventTarget.Owner, "Resync");
				return;
			}
			Resync();
			SoftResync();
			lastResyncTime += 5.0f; // This is a slight hack to force the video player to lay easy on the resyncs for a few seconds
		}

		// Set the volume on the video player
		public void _SetVolume(float volume) {
			Initialize();
			mediaPlayers.Volume = volume;
		}

		// Seek to a different position in a video if possible
		public void _Seek() {
			Initialize();
			if (!isActive) return;
			if (masterLock && !hasPermissions) return;
			SeekTo(seekVal * mediaPlayers.Duration); // seekVal is normalized so multiply it by media length
		}

		// Seek to a different position in a video if possible
		public void _SeekTo(float time) {
			Initialize();
			if (!isActive) return;
			if (masterLock && !hasPermissions) return;
			seekBar._SetVal(time / mediaPlayers.Duration);
			SeekTo(time);
		}

		// Set whether the video should loop when it finishes
		public void _SetLooping(bool loop) {
			Initialize();
			if (!isActive) return;
			if (masterLock && !hasPermissions && !suppressSecurity) return;
			else SetLooping(loop);
		}

		public void _SetActive(bool active) {
			Initialize();
			if (isActive == active) return;
			SetActiveInternal(active);
			Log(isActive ? "Activating" : "Deactivating", this);
			if (isActive) {
				isWakingUp = true;
				SetPlayerStatusText("No Video"); // Catch all for if nothing sets the status text
				CheckDeserializedData(); // Deserialization is paused and flushed while inactive
				// Unprivileged player should not retain ownership of player if player is locked
				if (masterLock && !isEditor && CheckPrivilegedInternal(Networking.LocalPlayer)) VerifyProperOwnership();
			} else {
				UnloadMediaAndFlushBuffers(); // Flush buffers and unload media
				if (Networking.IsOwner(gameObject)) ChooseNewOwner(); // We don't want ownership if inactive
			}
		}

		public void _Lock() {
			Initialize();
			SetLockState(true);
		}

		public void _Unlock() {
			Initialize();
			SetLockState(false);
		}

		public void _StartDiagnostics() {
			StartDiagnostics();
		}

		public void _CancelDiagnostics() {
			CancelDiagnostics();
		}

		// ---------------------- Accessors -----------------------
		
		public bool IsLocked => masterLock;
		public bool HasPermissions => hasPermissions;
		public bool AllowRetries => allowRetryWhenLoaded;
		public VRCUrl CurrentUrl => currentURL;
		public VRCUrl QueuedUrl => queueURL;
		public string PlayerStatus => playerStatus;
		public bool Loop => mediaPlayers.Loop;
		public float CurrentTime => isPlaying ? Time.time - startTime : pausedTime;
		public float Duration => playerReady ? mediaPlayers.Duration : 0;
		public float PreciseTime => mediaPlayers.Time;
		public bool Ready => playerReady;
		public float Volume => mediaPlayers.Volume;
		public int MediaType => isStream ? isLowLatency ? 2 : 1 : 0;
		public bool IsPlaying { get { Initialize(); return isPlaying; } }
		public bool IsSyncing => isSeeking || isResync || postResync;
		public bool IsLoggingDiagnostics => isLoggingDiagnostics;
		public bool Active => isActive;

		public string GetStatus() { return playerStatus; } // Deprecated
		public float GetTime() { return CurrentTime; } // Deprecated
		public float GetDuration() { return Duration; } // Deprecated
		public float GetTimePrecise() { return PreciseTime; } // Deprecated
		public bool GetIsPlaying() { return IsPlaying; } // Deprecated
		public bool GetIsReady() { return playerReady; } // Deprecated
		public VRCUrl GetCurrentURL() { return currentURL; } // Deprecated
		public float GetVolume() { return Volume; } // Deprecated
		public bool GetIsActive() { return isActive; } // Deprecated
		public bool GetIsLooping() { return Loop; } // Deprecated
		public int GetMediaType() { return MediaType; } // Deprecated
		public bool GetIsSyncing() { return IsSyncing; } // Deprecated
		public bool GetIsLoggingDiagnostics() { return isLoggingDiagnostics; } // Deprecated
		public bool CheckPrivileged(VRCPlayerApi vrcPlayer) { return CheckPrivilegedInternal(vrcPlayer); }

		// ------------------ External Utilities ------------------

		public void SetLastVideoLoadTime(float time) {
			// Sanity check unsafe time input to ensure we don't disable the video player unintentionally,
			// or cause undesired behaviour.
			if (time > lastVideoLoadTime && time <= Time.time) lastVideoLoadTime = time;
		}

		// ----------------- Internal UI Methods ------------------

		// Gets the status and the time of the current video player and
		// displays them in a text UI element
		private void UpdateStatus() {
			if (!hasStatsText) return;
			TimeSpan playingTime = TimeSpan.FromSeconds(mediaPlayers.Time);
			string timeStr = playingTime.ToString("G");
			timeStr = timeStr.Substring(0, timeStr.Length - 4);
			statisticsText.text = "Playback Time: " + timeStr + "\nStatus: " + playerStatus;
		}

		private void UpdateSeek() {
			if (isSeeking) {
				if (Time.time - lastSeekTime > seekPeriod) isSeeking = false;
				if (Mathf.Abs(mediaPlayers.Time - playerTimeAtSeek) < 0.5f) return;
			}
			if (!isPlaying) return;
			seekBar._SetVal(mediaPlayers.Time / Mathf.Max(0.1f, mediaPlayers.Duration));
		}

		// If anything is playing, unload it, flush buffers
		private void UnloadMediaAndFlushBuffers() {
			// These are some values that will trigger the required logic to
			// execute next time we check the deserialized data
			localTime = -1;
			localIsPlaying = false;
			localPlayerID = 0;
			localURL = VRCUrl.Empty;
			localQueueURL = VRCUrl.Empty;
			localQueueNow = false;
			localQueueTime = -1;
			// Stop and unload all media
			SetPlayingInternal(false);
			SetTimeInternal(0);
			StopInternal();
			mediaPlayers._Pause();
			// Ensure there's no queued media
			queueURL = VRCUrl.Empty;
			// Set status text one final time before activating again
			SetPlayerStatusText("Inactive");
			UpdateStatus();
		}

		// --------------------- Sync Methods ---------------------

		// Whenever a network sync happens, this method is called to give an opportunity to grab the data ASAP.
		public override void OnDeserialization() {
			if (!isActive) return;
			CheckDeserializedData();
		}

		// Extension of OnDeserialization to let it be called internally.  This method checks local copies of variables against remote ones.
		private void CheckDeserializedData() {
			waitForNextNetworkSync = false; // Cancel any waiting, this will 99.9% of the time be the correct value
			bool hasNewPlayerID = remotePlayerID != localPlayerID;
			// Cache local and remote strings so ToString() doesn't get called multiple times
			string localStr = localURL != null ? localURL.ToString() : "";
			string remoteStr = remoteURL != null ? remoteURL.ToString() : "";
			bool hasNewUrl = !string.Equals(localStr, remoteStr);
			// Cache next local and remote strings
			localStr = localQueueURL != null ? localQueueURL.ToString() : "";
			remoteStr = remoteQueueURL != null ? remoteQueueURL.ToString() : "";
			bool hasNewQueueUrl = !string.Equals(localStr, remoteStr);
			bool hasNewIsPlaying = remoteIsPlaying != localIsPlaying;
			bool hasNewTime = Mathf.Abs(remoteTime - localTime) > 0.1f;
			bool hasNewLock = remoteLock != localLock;
			bool hasNewLooping = remoteLooping != localLooping;
			bool hasNewQueueNow = remoteQueueNow != localQueueNow;
			bool hasNewQueueTime = Mathf.Abs(remoteQueueTime - localQueueTime) > 0.1f;
			if (hasNewPlayerID) { // Determines whether we swapped media type
				Log("Deserialization found new Media Player: " + mediaPlayers.GetPlayerName(remotePlayerID), this);
				localPlayerID = remotePlayerID;
				SetPlayerID(localPlayerID);
				SetPlayingInternal(localIsPlaying);
			}
			if (hasNewUrl) { // Load the new video if it has changed
				Log("Deserialization found new URL: " + remoteStr, this);
				Log("Old URL: " + localStr, this);
				localURL = remoteURL;
				if (hasNewTime) playFromBeginning = false;
				SetVideoURLFromLocal();
			}
			if (hasNewQueueUrl) { // Load the new video if it has changed
				newVideoLoading = !isWakingUp;
				Log("Deserialization found new queue URL: " + remoteStr, this);
				Log("Old URL: " + localStr, this);
				localQueueURL = remoteQueueURL;
				SetQueueVideoURLFromLocal();
			}
			if (hasNewIsPlaying) { // Update playing status if changed.
				Log("Deserialization found new playing status: " + (remoteIsPlaying ? "Playing" : "Paused"), this);
				SetPlayingInternal(localIsPlaying = remoteIsPlaying);
			}
			if (hasNewTime) { // Update local reference time if seek occurred
				Log("Deserialization found new seek position: " + remoteTime, this);
				localTime = remoteTime;
				SoftResync();
			}
			if (hasNewLock) {
				Log("Deserialization found lock status changed! Now " + (remoteLock ? "locked" : "unlocked"), this);
				localLock = remoteLock;
				SetLockStateInternal(localLock);
			}
			if (hasNewLooping) {
				Log("Deserialization found video is " + (remoteLooping ? "now" : "no longer") + " looping", this);
				localLooping = remoteLooping;
				SetLoopingInternal();
			}
			if (hasNewQueueNow) {
				Log("Queue Now network state changed from " + localQueueNow + " to " + remoteQueueNow, this);
				localQueueNow = remoteQueueNow;
				if (localQueueNow) PlayQueueNowInternal();
				else CancelQueueNowInternal();
			}
			if (hasNewQueueTime) {
				float newTime = CalcWithTime(localQueueTime = remoteQueueTime);
				Log("Deserialization found new sync queue video time: " + newTime, this);
				SetQueueVideoLoadTimeInternal(newTime);
			}
			isWakingUp = false;
		}

		private void SetVideoURLFromLocal() {
			Log("Decode URL: " + localURL, this);
			string localStr = localURL != null ? localURL.ToString() : "";
			if (string.IsNullOrWhiteSpace(localStr)) {
				localIsPlaying = false;
				localTime = 0;
				if (localPlayerID == 0) SeekInternal(localTime);
				StopInternal();
			}
			else {
				localTime = localIsPlaying ? CalcWithTime(-loadTime) : 0;
				SeekInternal(localPlayerID == 0 ? isReloadingVideo ? referencePlayhead : -loadTime : 0);
				LoadURLInternal(localURL);
			}
		}

		private void SetQueueVideoURLFromLocal() {
			Log("Decode Queue URL: " + localQueueURL, this);
			queueURL = localQueueURL;
		}

		private void SetPlayerID(int id) {
			Log("Set Media Player: " + mediaPlayers.GetPlayerName(id), this);
			SwitchVideoPlayerInternal(id);
			UpdateUICallback();
		}

		private void SeekTo(float newTime) {
			if (masterLock && !hasPermissions && !Networking.IsOwner(gameObject)) return;
			Log("Seek To: " + newTime, this);
			playerTimeAtSeek = mediaPlayers.Time; // Set a reference value to be able to timeout if seeking takes too long
			lastSeekTime = Time.time; // Video player will essentially pause on this time until it has finished seeking
			isSeeking = true;
			HardResync(newTime);
			if (hasCallback) callback.SendCustomEvent("_RelayVideoSeek");
		}

		private void SoftResync() {
			if (waitForNextNetworkSync) {
				lastResyncTime += resyncEvery * 0.25f;
				return;
			}
			LogVerbose("Soft Resync", this);
			// Make sure local time is in agreement with remote time, since it can change in certain conditions
			if (Mathf.Abs(remoteTime - localTime) > 0.1f) localTime = remoteTime;
			// Read local values and calculate local reference point
			float seekTime = localIsPlaying ? CalcWithTime(localTime) : localTime;
			lastResyncTime = Time.time;
			if (Time.time - resyncPauseAt < pauseResyncFor) return;
			SeekInternal(seekTime);
		}

		private void HardResync(float time) {
			Log("Hard Resync: " + time, this);
			localTime = localIsPlaying ? CalcWithTime(time) : time;
			SeekInternal(time);
			Sync();
		}

		private void SwitchPlayer(int id) {
			Log("Switch Player: " + mediaPlayers.GetPlayerName(id), this);
			if (isEditor && id != 0) {
				LogWarning("Cannot use stream player in editor! Setting ID to 0", this);
				id = 0;
			}
			if (id == localPlayerID) return;
			localPlayerID = id;
			SetPlayerID(id);
			Sync();
		}

		private void SetURL(VRCUrl url) {
			string localStr = localURL != null ? localURL.ToString() : "";
			string urlStr = url != null ? url.ToString() : "";
			Log("Set URL: " + urlStr, this);
			if (string.Equals(localStr, urlStr)) return;
			if (Networking.IsOwner(gameObject)) _Stop();
			localURL = url;
			SetVideoURLFromLocal();
			Sync();
		}

		private void SetQueueURL(VRCUrl url) {
			string localStr = localQueueURL != null ? localQueueURL.ToString() : "";
			string urlStr = url != null ? url.ToString() : "";
			Log("Set Queue URL: " + urlStr, this);
			if (string.Equals(localStr, urlStr)) return;
			localQueueURL = url;
			SetQueueVideoURLFromLocal();
			Sync();
		}

		private void PlayQueueNow() {
			localQueueNow = true;
			PlayQueueNowInternal();
			Sync();
		}

		private void CancelQueueNow() {
			localQueueNow = false;
			CancelQueueNowInternal();
			Sync();
		}

		private void SetQueueVideoTime(float time) {
			LogVerbose("Set Queue Video Time: " + time, this);
			localQueueTime = CalcWithTime(time);
			SetQueueVideoLoadTimeInternal(time);
			Sync();
		}

		private void SetPlaying(bool playing) {
			if (localIsPlaying == playing) return;
			Log("Set Playing: " + (playing ? "Playing" : "Pausing"), this);
			SetPlayingInternal(localIsPlaying = playing);
			HardResync(referencePlayhead);
		}

		private void SwapNetworkQueueToCurrent() {
			currentURL = localURL = localQueueURL;
			string newUrl = currentURL == null ? "" : currentURL.ToString();
			LogVerbose("Swapping network queue URL to current: " + newUrl, this);
			SetQueueURL(VRCUrl.Empty);
		}

		private void SetLooping(bool looping) {
			if (masterLock && !hasPermissions) return;
			if (localLooping == looping) return;
			Log("Set Looping: " + looping, this);
			localLooping = looping;
			Sync();
			SetLoopingInternal();
		}

		public void Resync() {
			Initialize();
			if (Networking.IsOwner(gameObject)) Sync();
		}

		private void Sync() {
			if (masterLock && !hasPermissions && !Networking.IsOwner(gameObject)) return;
			Log("Sync", this);
			isWakingUp = false;
			remotePlayerID = localPlayerID;
			remoteURL = localURL;
			remoteTime = localTime;
			remoteIsPlaying = localIsPlaying;
			remoteQueueURL = localQueueURL;
			remoteLock = localLock;
			remoteLooping = localLooping;
			remoteQueueNow = localQueueNow;
			remoteQueueTime = localQueueTime;
			if (!Networking.IsOwner(gameObject))
				Networking.SetOwner(Networking.LocalPlayer, gameObject);
			RequestSerialization();
		}

		private float GetServerTime() {
			LogVerbose("Get Server Time", this);
			return (Networking.GetServerTimeInMilliseconds() & 0xFFFFFFF) * 0.001f;
		}

		private float CalcWithTime(float val) {
			float estimate = GetServerTime() - val;
			if (estimate < syncPeriod * -2) estimate += 0xFFFFFFF * 0.001f;
			if (estimate > 86400 * 2) estimate -= 0xFFFFFFF * 0.001f;
			return estimate;
		}
		
		// ----------------- Player Stats Methods -----------------

		public override void OnOwnershipTransferred(VRCPlayerApi player) {
			Initialize();
			UpdateUICallback();
			if (player == null) {
				Log("Ownership transferred to nobody", this);
				return;
			}
			Log("Ownership transferred to: " + player.displayName, this);
			if (!player.isLocal) return;
			PingActive();
		}

		private void PingActive() {
			if (!Networking.IsOwner(gameObject)) return;
			SendCustomNetworkEvent(NetworkEventTarget.All, "PingForActive");
			lastActivePing = Time.time;
			numActivePlayersTmp = 1;
			numReadyPlayers = 0;
			SendCustomEventDelayedSeconds("PingActive", pingActiveEvery);
			if (playerReady) numReadyPlayers++;
		}

		public void PingForActive() {
			if (Networking.IsOwner(gameObject)) return;
			lastActivePing = Time.time;
			SendCustomNetworkEvent(NetworkEventTarget.Owner, "ActivePing");
			if (playerReady) SendCustomNetworkEvent(NetworkEventTarget.Owner, "VideoReadyPing");
		}

		public void ActivePing() {
			if (!Networking.IsOwner(gameObject)) return;
			if (Time.time - lastActivePing < holdPingOpenFor) {
				numActivePlayersTmp++;
			} else {
				if (numActivePlayersTmp > 0) {
					numActivePlayers = numActivePlayersTmp;
					numActivePlayersTmp = 0;
				}
				numActivePlayers++;
			}
		}

		public void InactivePing() {
			if (!Networking.IsOwner(gameObject)) return;
			if (Time.time - lastActivePing < holdPingOpenFor) {
				numActivePlayersTmp--;
			} else {
				if (numActivePlayersTmp > 0) {
					numActivePlayers = numActivePlayersTmp;
					numActivePlayersTmp = 0;
				}
				numActivePlayers--;
				if (numActivePlayers < 1) numActivePlayers = 1;
			}
		}

		public void VideoReadyPing() {
			if (!Networking.IsOwner(gameObject)) return;
			numReadyPlayers++;
		}

		public void VideoNotReadyPing() {
			if (!Networking.IsOwner(gameObject)) return;
			numReadyPlayers--;
			int lowReadyCount = playerReady ? 1 : 0;
			if (numReadyPlayers < lowReadyCount) numReadyPlayers = lowReadyCount;
		}

		// ------------------ Video Sync Methods ------------------

		private void UpdateVideoPlayer() {
			if (!isPlaying && playerReady) {
				PauseInternal();
			} else if (isStream) {
				StreamLogic();
			} else if (isPlaying && playerReady) {
				ResyncLogic();
			}
			if (isPlaying && !isStream) PreloadLogic();
			if (isReloadingVideo && Time.time > lastVideoLoadTime + videoLoadCooldown) {
				SetPlayerStatusText("Reloading Video");
				SetVideoURLFromLocal();
			}
			if (!playerReady && !isAutomaticRetry && retryCount == 0 && !localURL.Equals(VRCUrl.Empty) && Time.time > lastVideoLoadTime + videoLoadCooldown) {
				isAutomaticRetry = true;
				ReloadVideoInternal();
			}
		}

		private void StreamLogic() {
			if (!isPlaying || mediaPlayers.IsPlaying) return;
			if (mediaPlayers.IsReady) {
				SetPlayerStatusText("Playing Stream");
				Log("Playing Stream", this);
				mediaPlayers._Play();
			} else if (!isLoading) {
				if (Time.time - lastVideoLoadTime < videoLoadCooldown) return;
				SetPlayerStatusText("Loading Stream");
				string urlStr = currentURL != null ? currentURL.ToString() : "";
				Log("Loading Stream URL: " + urlStr, this);
				if (isPlaying) {
					mediaPlayers._PlayURL(currentURL);
					SetLoadTimeToNow();
				} else {
					mediaPlayers._LoadURL(currentURL);
					SetLoadTimeToNow();
				}
				if (hasCallback && !isLoading) callback.SendCustomEvent("_RelayVideoLoading");
				isLoading = true;
			}
		}

		private void PreloadLogic() {
			if (queueVideoReady) {
				if (!playQueueVideoNow || !(Time.time > playQueueVideoTime)) return;
				if (!mediaPlayers.NextReady) {
					LogWarning("Next video not actually ready!", this);
					queueVideoLoading = false;
					queueVideoReady = false;
					return;
				}
				Log("Playing queued video", this);
				resyncPauseAt = Time.time;
				if (Networking.IsOwner(gameObject)) SeekTo(-seekPeriod);
				else HardResync(-seekPeriod);
				mediaPlayers._PlayNext();
				return;
			}
			if (queueVideoLoading) return;
			if (queueURL == null || queueURL.Equals(VRCUrl.Empty)) return;
			if (!mediaPlayers.IsReady || !isPlaying) return;
			float mediaTime = mediaPlayers.Time;
			if (!playQueueVideoNow && (mediaTime < 0.01f || mediaPlayers.Duration - mediaTime > preloadNextVideoTime)) return;
			if (Time.time - lastVideoLoadTime < videoLoadCooldown) return;
			Log("Loading queue URL: " + queueURL.ToString(), this);
			mediaPlayers._LoadNextURL(queueURL);
			queueVideoLoading = true;
			SetLoadTimeToNow();
		}

		private void ResyncLogic() {
			if (playFromBeginning) {
				HardResync(-seekPeriod);
				playFromBeginning = false;
			}
			referencePlayhead = Time.time - startTime;
			float currentTime = mediaPlayers.Time;
			deviation = currentTime - referencePlayhead;
			float absDeviation = Mathf.Abs(deviation);
			float timeMinusLastSoftSync = Time.time - lastSoftSyncTime;
			if (postResync) {
				if (Time.time < postResyncEndsAt) {
					if (!mediaPlayers.IsPlaying) {
						if (currentTime > referencePlayhead + deviationTolerance) {
							LogVerbose("Ending Post Resync early", this);
							postResyncEndsAt = Time.time;
						} else {
							LogVerbose("Resuming internal player during Post Resync period", this);
							mediaPlayers._Play();
							SetPlayerStatusText("Playing");
						}
					} else {
						BlackOutInternal(absDeviation > deviationTolerance * 2);
					}
					return;
				}
				postResync = false;
				if (absDeviation <= deviationTolerance) {
					SetPlayerStatusText("Playing");
					return;
				}
				float overshoot = Mathf.Clamp(-deviation, videoOvershoot * 2, videoOvershoot * 10 + deviationTolerance * 10 + 5.0f);
				ResyncTime(currentTime, referencePlayhead + overshoot);
				Log("Post Resync Compensation: " + overshoot, this);
				SetPlayerStatusText("Catching Up");
				if (!mediaPlayers.IsPlaying) {
					LogVerbose("Resuming internal player at end of Post Resync period", this);
					mediaPlayers._Play();
					SetPlayerStatusText("Playing");
				}
				return;
			}
			if (isResync) {
				isResync = (Mathf.Abs(currentTime - playerTimeAtResync) < 2.0f || timeMinusLastSoftSync < checkSyncEvery) && timeMinusLastSoftSync < videoOvershoot * 10 + deviationTolerance * 10 + 5.0f;
				if (!isResync) {
					postResync = true;
					postResyncEndsAt = Time.time + (timeMinusLastSoftSync + checkSyncEvery * 0.5f);
					SetPlayerStatusText("Stabilizing");
					if (!mediaPlayers.IsPlaying) {
						LogVerbose("Resuming internal player on end of Resync", this);
						mediaPlayers._Play();
						SetPlayerStatusText("Playing");
					}
				} else if (!mediaPlayers.IsPlaying) {
					if (currentTime < referencePlayhead + deviationTolerance) {
						LogVerbose("Resuming internal player during Resync", this);
						mediaPlayers._Play();
						SetPlayerStatusText("Playing");
					}
				} else {
					BlackOutInternal(absDeviation > deviationTolerance * 2);
				}
			}
			if (isResync) return;
			if (Time.time - lastResyncTime >= resyncEvery) SoftResync();
			if (Time.time - lastCheckTime < checkSyncEvery) return;
			BlackOutInternal(absDeviation > deviationTolerance * 2);
			if (Time.time - resyncPauseAt < pauseResyncFor) return;
			lastCheckTime = Time.time;
			if (mediaPlayers.IsPlaying) {
				if (absDeviation > deviationTolerance) {
					ResyncTime(currentTime, referencePlayhead + videoOvershoot);
					SetPlayerStatusText("Syncing");
				} else if (deviation > videoOvershoot * 2) {
					LogVerbose("Pausing internal player", this);
					mediaPlayers._Pause();
					SetPlayerStatusText("Waiting For Playhead");
				}
			} else {
				if (absDeviation > deviationTolerance) ResyncTime(currentTime, referencePlayhead + videoOvershoot);
				if (deviation <= videoOvershoot / 2) {
					LogVerbose("Resuming internal player on Soft Sync", this);
					mediaPlayers._Play();
					SetPlayerStatusText("Playing");
				}
			}
		}

		private void ResyncTime(float oldTime, float newTime) {
			Log("Resync: " + oldTime + " -> " + newTime, this);
			SetTimeInternal(newTime);
			isResync = true;
			lastSoftSyncTime = Time.time;
			playerTimeAtResync = oldTime;
			if (mediaPlayers.IsReady && !mediaPlayers.IsPlaying) {
				LogVerbose("Resuming internal player on Resync Time", this);
				mediaPlayers._Play();
			}
		}

		private void SetPlayingInternal(bool playing) {
			Log("Set Playing Internal: " + (playing ? "Playing" : "Paused"), this);
			isPlaying = playing;
			if (!isStream && playerReady) {
				SeekInternal(playFromBeginning ? -seekPeriod : referencePlayhead);
				playFromBeginning = false;
			}
			SetPlayerStatusText(playing ? "Waiting to Play" : "Paused");
		}

		private void PauseInternal() {
			if (!mediaPlayers.IsReady) return;
			BlackOutInternal(false);
			if (Time.time - lastResyncTime >= resyncEvery) {
				lastResyncTime = Time.time;
				if (isPlaying == localIsPlaying) return;
				isPlaying = localIsPlaying;
				Log("Playing status corrected to: " + isPlaying, this);
				return;
			}
			if (!mediaPlayers.IsPlaying) return;
			Log("Pause Internal", this);
			mediaPlayers._Pause();
			SetTimeInternal(pausedTime);
			deviation = 0;
			SetPlayerStatusText("Paused");
		}

		private void StopInternal() {
			Log("Stop", this);
			isPlaying = false;
			BlackOutInternal(true);
			if (Networking.IsOwner(gameObject)) SetPlaying(false);
			currentURL = VRCUrl.Empty;
			urlValid = false;
			SetPlayerStatusText("No Video");
			playFromBeginning = true;
		}

		private void SetLoopingInternal() {
			Log("Set Looping Internal: " + localLooping, this);
			mediaPlayers.Loop = localLooping;
		}

		private void PlayQueueNowInternal() {
			LogVerbose("Play Queue Now Internal", this);
			playQueueVideoNow = true;
		}

		private void CancelQueueNowInternal() {
			LogVerbose("Cancel Queue Now Internal", this);
			playQueueVideoNow = false;
		}

		private void SetQueueVideoLoadTimeInternal(float time) {
			LogVerbose("Set Queue Video Load Time Internal: " + time, this);
			playQueueVideoTime = time;
		}

		private void SetLoadTimeToNow() {
			// Adds a random value to the last video load time to randomize the time interval between loads
			lastVideoLoadTime = Time.time + UnityEngine.Random.value;
		}

		private void LoadURLInternal(VRCUrl url) {
			string urlStr = url != null ? url.ToString() : "";
			Log("Load URL: " + urlStr, this);
			string thisStr = currentURL != null ? currentURL.ToString() : "";
			if (isPreparingForLoad) newVideoLoading = true;
			isPreparingForLoad = false;
			if (!isReloadingVideo && string.Equals(urlStr, thisStr)) {
				LogWarning("URL already loaded! Ignoring.", this);
				newVideoLoading = false; // We don't want to leave this set on abort
				return;
			}
			string suffix = "";
			if (!isReloadingVideo) retryCount = 0; // Reset retry count if not manually reloading
			else if (isAutomaticRetry) suffix = " (" + retryCount + "/" + numberOfRetries + ")";
			isReloadingVideo = false;
			isAutomaticRetry = false;
			urlValid = false;
			currentURL = url;
			mediaPlayers._LoadURL(url);
			SetReadyInternal(false);
			SetLoadTimeToNow();
			if (hasCallback && !isLoading) callback.SendCustomEvent("_RelayVideoLoading");
			isLoading = true;
			SetPlayerStatusText("Loading Video" + suffix);
		}

		private void SeekInternal(float time) {
			LogVerbose("Seek Internal: " + time, this);
			startTime = Time.time - time;
			referencePlayhead = pausedTime = urlValid || isPlaying ? time : 0;
			// Terminate Resync and Post Resync periods
			postResync = false;
			isResync = false;
			// Since we cancel out the resync booleans, this is the last chance we get to normalize the status text
			SetPlayerStatusText(isPlaying ? "Playing" : "Paused");
		}

		private void BlackOutInternal(bool blackOut) {
			if (isBlackingOut == blackOut) return;
			mediaPlayers.BlackOutPlayer = isBlackingOut = blackOut;
		}

		// ------------------- Callback Methods -------------------

		public void _RelayVideoReady() {
			LogVerbose("Relay Video Ready", this);
			Initialize();
			if (!isActive) return;
			float duration = mediaPlayers.Duration;
			if (duration <= 0.01f) { // Video loaded incorrectly, retry
				Log("Video loaded incorrectly, retrying...", this);
				ReloadVideoInternal();
				SetPlayerStatusText("Reloading Video");
				return;
			}
			bool isReallyStream = Single.IsNaN(duration) || Single.IsInfinity(duration);
			BlackOutInternal(!isReallyStream);
			if (isStream != isReallyStream) {
				isStream = isReallyStream;
				UpdateUICallback();
				int newPlayerId = isStream ? isLowLatency ? 2 : 1 : 0;
				if (Networking.IsOwner(gameObject)) SwitchPlayer(newPlayerId);
				else SetPlayerID(newPlayerId);
				return;
			}
			if (isLoading && playOnNewVideo && newVideoLoading && Networking.IsOwner(gameObject)) _Start(); 
			isLoading = false;
			SetReadyInternal(true);
			urlValid = true;
			newVideoLoading = false;
			SetPlayerStatusText("Ready");
			if (hasCallback) callback.SendCustomEvent("_RelayVideoReady");
		}

		public void _RelayVideoEnd() {
			LogVerbose("Relay Video End", this);
			Initialize();
			if (!isActive) return;
			isPlaying = false;
			if (Networking.IsOwner(gameObject)) SetPlaying(false);
			SetPlayerStatusText("Stopped");
			playFromBeginning = true;
			if (hasCallback) callback.SendCustomEvent("_RelayVideoEnd");
			if (isStream) return;
			SeekInternal(0);
			// Finish video callback?
		}

		public void _RelayVideoError() {
			LogVerbose("Relay Video Error", this);
			Initialize();
			if (!isActive) return;
			string errorString = GetErrorString(relayVideoError);
			SetPlayerStatusText("Error: " + errorString);
			SetReadyInternal(false);
			if (automaticRetry) {
				if (isPreparingForLoad) {
					LogVerbose("Suppressing retry since we're preparing a new URL", this);
				} else if (relayVideoError == VideoError.AccessDenied) {
					Log("Not retrying, user needs to allow untrusted URLs", this);
				} else if (retryCount < numberOfRetries) {
					retryCount++;
					Log("Retrying load (" + retryCount + " of " + numberOfRetries + ")", this);
					isAutomaticRetry = true;
					ReloadVideoInternal();
					return;
				} else {
					Log("Not retrying, retry limit reached", this);
				}
			} else {
				Log("Not retrying, automatic retries disabled", this);
			}
			if (!hasCallback) return;
			callback.SetProgramVariable("relayVideoError", relayVideoError);
			callback.SendCustomEvent("_RelayVideoError");
		}

		public void _RelayVideoStart() {
			LogVerbose("Relay Video Start", this);
			Initialize();
			if (!isActive) return;
			SetPlayerStatusText("Playing");
			SetReadyInternal(true);
			playFromBeginning = false;
			isLoading = false;
			newVideoLoading = false;
			if (hasCallback) callback.SendCustomEvent("_RelayVideoStart");
		}

		public void _RelayVideoPlay() {
			LogVerbose("Relay Video Play", this);
			Initialize();
			if (!isActive) return;
			SetPlayerStatusText("Playing");
			SetReadyInternal(true);
			playFromBeginning = false;
			isLoading = false;
			if (hasCallback) callback.SendCustomEvent("_RelayVideoPlay");
		}

		public void _RelayVideoPause() {
			LogVerbose("Relay Video Pause", this);
			Initialize();
			if (!isActive) return;
			string urlText = currentURL == null ? "" : currentURL.ToString();
			SetPlayerStatusText(string.IsNullOrWhiteSpace(urlText) ? "No Video" : playFromBeginning ? "Stopped" : "Paused");
			if (hasCallback) callback.SendCustomEvent("_RelayVideoPause");
		}

		public void _RelayVideoLoop() {
			LogVerbose("Relay Video Loop", this);
			Initialize();
			if (!isActive) return;
			resyncPauseAt = Time.time;
			SetTimeInternal(0);
			if (Networking.IsOwner(gameObject)) HardResync(0);
			if (hasCallback) callback.SendCustomEvent("_RelayVideoLoop");
		}

		public void _RelayVideoNext() {
			LogVerbose("Relay Video Next", this);
			Initialize();
			if (!isActive) return;
			// Queued video is starting
			resyncPauseAt = Time.time;
			SetTimeInternal(0);
			if (Networking.IsOwner(gameObject)) HardResync(0);
			// Get new duration!
			queueVideoLoading = queueVideoReady = playQueueVideoNow = false;
			currentURL = queueURL;
			if (Networking.IsOwner(gameObject)) SwapNetworkQueueToCurrent();
			if (hasCallback) callback.SendCustomEvent("_RelayVideoNext");
		}

		public void _RelayVideoQueueError() {
			LogVerbose("Relay Video Queue Error", this);
			Initialize();
			if (!isActive) return;
			// Queued video player has thrown an error
			SetPlayerStatusText("Error: " + GetErrorString(relayVideoError));
			queueVideoLoading = queueVideoReady = false;
			if (Networking.IsOwner(gameObject)) SetQueueURL(VRCUrl.Empty);
			// Get ready to stop?
			// Possibly skip video?
			if (hasCallback) callback.SendCustomEvent("_RelayVideoQueueError");
		}

		public void _RelayVideoQueueReady() {
			LogVerbose("Relay Video Queue Ready", this);
			Initialize();
			if (!isActive) return;
			// Queued video has loaded
			queueVideoLoading = false;
			queueVideoReady = true;
			if (newVideoLoading && !isAutomaticRetry) {
				isAutomaticRetry = true;
				retryCount = 0;
				ReloadVideoInternal();
			}
			if (hasCallback) callback.SendCustomEvent("_RelayVideoQueueReady");
			if (!Networking.IsOwner(gameObject)) return;
			SetQueueVideoTime(Time.time + syncPeriod);
		}

		// -------------------- Utility Methods -------------------

		// Checks URL to see if it needs to swap Player ID
		private int CheckURL(string urlStr, int playerID) {
			Log("CheckURL", this);
			int newPlayerID = playerID;
			int colonPos = urlStr.IndexOf("://", StringComparison.Ordinal);
			if (colonPos < 1 || urlStr.Length < colonPos + 5) {
				LogError("Malformed URL", this);
				return -1;
			}
			int prefixLength = urlStr.IndexOf('/', colonPos + 3);
			if (prefixLength < 1) {
				LogError("Malformed URL", this);
				return -1;
			}
			string urlProtocol = urlStr.Substring(0, colonPos).ToLower();
			bool isAllowedProtocol = false;
			foreach (string protocol in videoProtocols) if (string.Equals(urlProtocol, protocol)) { isAllowedProtocol = true; break; }
			if (!isAllowedProtocol) {
				Log("Invalid Protocol: " + urlProtocol, this);
				return -1;
			}
			Log("URL Protocol: " + urlProtocol, this);
			string urlHost = urlStr.Substring(colonPos + 3, prefixLength - 3 - colonPos);
			Log("URL Host: " + urlHost, this);
			foreach (string host in videoHosts) if (string.Equals(urlHost, host)) { newPlayerID = 0; break; }
			foreach (string host in streamHosts) if (string.Equals(urlHost, host)) { newPlayerID = 1; break; }
			//foreach (string host in videoStreamHosts) if (string.Equals(urlHost, host)) { newPlayerID = 1; break; }
			if (string.Equals(urlHost, "youtube.com") || string.Equals(urlHost, "www.youtube.com")) {
				string parameters = urlStr.Substring(prefixLength + 1, urlStr.Length - prefixLength - 1);
				Log("URL Parameters: " + parameters, this);
				foreach (string verb in new string[] { "playlist", "watch" })
					if (parameters.IndexOf(verb, StringComparison.Ordinal) == 0) newPlayerID = 1;
				if (parameters.Length >= 4 && string.Equals(parameters.Substring(parameters.Length - 4, 4), "live")) newPlayerID = 1;
			}
			if (urlStr.Substring(urlStr.Length - 5, 5).Equals(".m3u8")) newPlayerID = 1;
			if (string.Equals(urlProtocol, "rtmp")) newPlayerID = 1;
			foreach (string llProtocol in lowLatencyProtocols) {
				if (string.Equals(urlProtocol, llProtocol)) newPlayerID = 2;
			}
			if (isEditor && newPlayerID != 0) {
				Log("Cannot play stream in editor! Current Player: " + mediaPlayers.GetPlayerName(0) + ", Desired Player: " + mediaPlayers.GetPlayerName(newPlayerID), this);
				newPlayerID = 0;
			} else if (newPlayerID != playerID) {
				LogWarning("URL not appropriate for specified player, switching from " +
				           mediaPlayers.GetPlayerName(playerID) + " to " + mediaPlayers.GetPlayerName(newPlayerID), this);
			}
			return newPlayerID;
		}

		private bool SanityCheckURL(string url) {
			if (string.IsNullOrWhiteSpace(url)) {
				LogError("URL cannot be blank!", this);
				return false;
			}
			if (!string.Equals(url.Trim(), url)) {
				LogError("URL cannot have whitespace at the beginning or end!", this);
				return false;
			}
			return true;
		}

		private void SetPlayerStatusText(string status) {
			if (string.Equals(status, playerStatus)) return;
			LogVerbose("Setting Player Status Text: " + status, this);
			playerStatus = status;
			SendStatusCallback();
		}

		private void SendStatusCallback() {
			if (!setStatusEnabled) return;
			callback.SetProgramVariable(statusProperty, playerStatus);
			callback.SendCustomEvent(setStatusMethod);
		}

		private void SetReadyInternal(bool ready) {
			if (ready == playerReady) return;
			playerReady = ready;
			if (Networking.IsOwner(gameObject)) numReadyPlayers += playerReady ? 1 : -1;
			else SendCustomNetworkEvent(NetworkEventTarget.Owner, ready ? "VideoReadyPing" : "VideoNotReadyPing");
		}

		private void SetActiveInternal(bool active) {
			if (active == isActive) return;
			isActive = active;
			if (Networking.IsOwner(gameObject)) return;
			SendCustomNetworkEvent(NetworkEventTarget.Owner, active ? "ActivePing" : "InactivePing");
			if (hasPermissions && isActive) VerifyProperOwnership();
		}

		// Returns time in video, conditionally pulling it from the player or
		// from the paused time.
		private float GetTimeInternal() {
			LogVerbose("GetTimeInternal", this);
			return isPlaying ? mediaPlayers.Time : pausedTime;
		}

		private void SetTimeInternal(float time) {
			LogVerbose("Set Time Internal: " + time, this);
			if (isStream) return;
			mediaPlayers.Time = time > 0 ? time : 0;
		}

		private void SwitchVideoPlayerInternal(int id) {
			LogVerbose("Switch Video Player Internal: " + id, this);
			mediaPlayers._SwitchPlayer(id);
			if (isPlaying) ReloadVideoInternal();
			isStream = mediaPlayers.IsStream;
			seekBar._SetEnabled(!isStream);
		}

		// Reload video properly
		private void ReloadVideoInternal() {
			if (isPreparingForLoad) return; // We're trying to load a URL, so don't do anything with reloads
			isReloadingVideo = true; // Set this so the player doesn't reject our request to reload the video
			if (Time.time < lastVideoLoadTime + videoLoadCooldown) { // Cancel video load if we recently attempted to load a video
				SetPlayerStatusText("Waiting to Retry Load");
				return;
			}
			SetPlayerStatusText("Reloading Video");
			SetVideoURLFromLocal();
		}

		private void UpdateUICallback() {
			if (hasCallback) callback.SendCustomEvent("_RecheckVideoPlayer");
		}

		// Takes a VideoError Object and returns a string describing the error.
		private string GetErrorString(VideoError error) {
			string errorString;
			switch (error) {
				case VideoError.Unknown:
					errorString = "Unknown";
					break;
				case VideoError.AccessDenied:
					errorString = "Unsafe URLs Disallowed";
					break;
				case VideoError.PlayerError:
					errorString = "Player Error";
					break;
				case VideoError.RateLimited:
					errorString = "Rate Limited";
					break;
				case VideoError.InvalidURL:
					errorString = "Invalid URL";
					break;
				default:
					errorString = "Invalid Error";
					break;
			}
			return errorString;
		}
		
		// ------------------ Diagnostic Methods ------------------

		private void UpdateDiagnostics() {
			diagnosticStr = "";
			UpdateDiagnosticLog();
			UpdateDiagnosticsDisplay();
		}

		private void UpdateDiagnosticsDisplay() {
			if (!diagnosticsText) return;
			if (Time.time - lastDiagnosticsUpdate < diagnosticsUpdatePeriod) return;
			lastDiagnosticsUpdate = Time.time;
			UpdateDiagnosticString();
			diagnosticsText.text = diagnosticStr;
		}

		private void UpdateDiagnosticLog() {
			if (!isLoggingDiagnostics) return;
			float uTime = Time.time;
			if (uTime - lastDiagnosticsLog < diagnosticDelay) return;
			if (uTime > diagnosticEnd) isLoggingDiagnostics = false;
			UpdateDiagnosticString();
			if (++currentDiagUpdate > diagnosticUpdatesPerLog) {
				currentDiagUpdate = 0;
				currentDiagLog++;
				diagnosticLog[currentDiagLog] = diagnosticStr + "\n";
			} else {
				diagnosticLog[currentDiagLog] += "\n" + diagnosticStr + "\n";
			}
			lastDiagnosticsLog = Time.time;
			if (isLoggingDiagnostics) return;
			diagnosticLog[currentDiagLog] += "\n--- End SMP Diagnostic Log ---";
			Log("Diagnostics finished", this);
			for (int i = 0; i <= currentDiagLog; i++) Debug.Log(diagnosticLog[i], this);
		}

		private void UpdateDiagnosticString() {
			if (!string.IsNullOrWhiteSpace(diagnosticStr)) return;
			float uTime = Time.time;
			string str = "Time: " + uTime.ToString("N3");
			str += ", Player Status: " + playerStatus;
			str += ", Start Time: " + startTime.ToString("N3");
			str += ", Paused Time: " + pausedTime.ToString("N3");
			str += ", Reference Playhead: " + referencePlayhead.ToString("N3");
			str += ", Is Playing: " + isPlaying;
			str += "\nURL Valid: " + urlValid;
			str += ", Player Ready: " + playerReady;
			str += ", Is Loading: " + isLoading;
			str += ", Time Since Video Load: " + (uTime - lastVideoLoadTime).ToString("N3");
			str += "\nIs Automatic Retry: " + isAutomaticRetry;
			str += ", Retry Count: " + retryCount;
			str += ", Is Reloading Video: " + isReloadingVideo;
			str += ", Is Preparing For Load: " + isPreparingForLoad;
			str += ", New Video Loading: " + newVideoLoading;
			str += "\nIs Seeking: " + isSeeking;
			str += ", Time Since Seek: " + (uTime - lastSeekTime).ToString("N3");
			str += ", Player Time At Seek: " + playerTimeAtSeek.ToString("N3");
			str += ", Is Low Latency: " + isLowLatency;
			str += ", Is Stream: " + isStream;
			str += ", Play From Beginning: " + playFromBeginning;
			str += "\nIs Resync: " + isResync;
			str += ", Post Resync: " + postResync;
			str += ", Deviation: " + deviation.ToString("N3");
			str += ", Time Since Last Check: " + (uTime - lastCheckTime).ToString("N3");
			str += "\nTime Since Resync: " + (uTime - lastResyncTime).ToString("N3");
			str += ", Post Resync Ends In: " + Mathf.Max(postResyncEndsAt - uTime, 0).ToString("N3");
			str += ", Time Since Resync Pause: " + (uTime - resyncPauseAt).ToString("N3");
			str += ", Player Time At Resync: " + playerTimeAtResync.ToString("N3");
			str += ", Time Since Soft Sync: " + (uTime - lastSoftSyncTime).ToString("N3");
			str += "\nPlayer Time: " + mediaPlayers.Time.ToString("N3");
			str += ", Player Duration: " + mediaPlayers.Duration.ToString("N3");
			str += ", Player Playing: " + mediaPlayers.IsPlaying;
			str += ", Player Ready: " + mediaPlayers.IsReady;
			str += "\nQueue Video Now: " + playQueueVideoNow;
			str += ", Queue Video Starts In: " + Mathf.Max(0, playQueueVideoTime - uTime).ToString("N3");
			str += ", Queue Video Loading: " + queueVideoLoading;
			str += ", Queue Video Ready: " + queueVideoReady;
			diagnosticStr = str;
		}

		private void StartDiagnostics() {
			if (isLoggingDiagnostics) return;
			Log("Diagnostics starting", this);
			diagnosticLog = new string[(int)(diagnosticPeriod / diagnosticDelay)];
			currentDiagLog = 0;
			diagnosticLog[currentDiagLog] = "--- Begin SMP Diagnostic Log ---\n";
			isLoggingDiagnostics = true;
			diagnosticEnd = Time.time + diagnosticPeriod;
		}

		private void CancelDiagnostics() {
			if (!isLoggingDiagnostics) return;
			isLoggingDiagnostics = false;
			diagnosticLog = null;
			Log("Diagnostics canceled", this);
		}

		private void AddToDiagnosticLog(string str) {
			diagnosticLog[currentDiagLog] += "\n[" + Time.time + "] " + str + "\n";
		}

		// ------------ Security And Ownership Methods ------------

		public override void OnPlayerJoined(VRCPlayerApi player) {
			Initialize();
			if (isActive || isEditor) return;
			if (!Networking.IsOwner(gameObject)) return;
			// We don't want ownership if we're inactive
			Networking.SetOwner(player, gameObject);
		}

		private void ChooseNewOwner() {
			if (isEditor) return;
			int numPlayers = VRCPlayerApi.GetPlayerCount();
			VRCPlayerApi[] players = new VRCPlayerApi[numPlayers];
			VRCPlayerApi newOwner = Networking.LocalPlayer;
			for (int i = 0; i < numPlayers; i++) {
				if (players[i] == null) continue;
				if (!masterLock || CheckPrivilegedInternal(players[i])) {
					if (players[i].isLocal) continue;
					newOwner = players[i];
					break;
				}
				if (newOwner.isLocal) newOwner = players[i];
			}
			// If new owner is local, there is nobody else in the instance.
			if (!newOwner.isLocal) Networking.SetOwner(newOwner, gameObject);
		}

		private void VerifyProperOwnership() {
			if (isEditor) return;
			if (!CheckPrivilegedInternal(Networking.GetOwner(gameObject)))
				Networking.SetOwner(Networking.LocalPlayer, gameObject);
		}

		private bool CheckPrivilegedInternal(VRCPlayerApi vrcPlayer) {
			if (vrcPlayer == null) return true;
			if (vrcPlayer.isMaster && masterCanLock) return true;
			if (vrcPlayer.isInstanceOwner && ownerCanLock) return true;
			string playerName = vrcPlayer.displayName;
			foreach (string moderator in moderators)
				if (string.Equals(playerName, moderator))
					return true;
			return false;
		}

		private void SetLockState(bool lockState) {
			if (!hasPermissions) return;
			SetLockStateInternal(lockState);
			localLock = lockState;
			Sync();
		}

		private void SetLockStateInternal(bool lockState) {
			masterLock = lockState;
			if (seekBar) seekBar._SetLocked(masterLock && !hasPermissions);
			if (hasCallback) callback.SendCustomEvent(masterLock ? "_PlayerLocked" : "_PlayerUnlocked");
		}

		public void _SuppressSecurity(float t) {
			if (Time.time - t > 0.01f) {
				LogWarning("Invalid security override canceled", this);
				return;
			}
			LogVerbose("Suppress Security", this);
			suppressSecurity = true;
			SendCustomEventDelayedFrames("EngageSecurity", 0);
		}

		public void EngageSecurity() {
			suppressSecurity = false;
		}

		// ----------------- Debug Helper Methods -----------------
		private void Log(string message, UnityEngine.Object context) {
			if (isLoggingDiagnostics) AddToDiagnosticLog("SMP: " + message);
			if (enableDebug) Debug.Log(debugPrefix + message, context);
		}

		private void LogVerbose(string message, UnityEngine.Object context) {
			if (isLoggingDiagnostics) AddToDiagnosticLog("SMP Verbose: " + message);
			if (verboseDebug && enableDebug) Debug.Log(debugPrefix + "(+v) " + message, context);
		}

		private void LogWarning(string message, UnityEngine.Object context) {
			if (isLoggingDiagnostics) AddToDiagnosticLog("SMP Warning: " + message);
			Debug.LogWarning(debugPrefix + message, context);
		}

		private void LogError(string message, UnityEngine.Object context) {
			if (isLoggingDiagnostics) AddToDiagnosticLog("SMP Error: " + message);
			Debug.LogError(debugPrefix + message, context);
		}
	}
}
