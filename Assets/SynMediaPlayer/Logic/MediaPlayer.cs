
using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDK3.Components.Video;
using VRC.SDKBase;
using VRC.Udon;
using Synergiance.MediaPlayer.UI;

namespace Synergiance.MediaPlayer {
	[DefaultExecutionOrder(100)]
	public class MediaPlayer : UdonSharpBehaviour {
		[Header("Objects")] // Objects and components
		[SerializeField]  private VideoInterpolator  mediaPlayers;                  // Object to reference all your different media players.  Needs to correspond with media select
		[SerializeField]  private VRCUrlInputField   urlInputField;                 // URL Input Field to use to load a URL from
		[SerializeField]  private ToggleGroupHelper  mediaSelect;                   // Toggle Group with names indicating what type of media you want
		[SerializeField]  private SeekControl        seekBar;                       // Seek bar object.  Needs to be normalized
		[SerializeField]  private Slider             volumeBar;                     // Volume bar object.  Range is 0-1
		[SerializeField]  private Text               statisticsText;                // Text for video stats, will read out video player status
		[SerializeField]  private Toggle             loopToggle;                    // Toggle for whether video should loop

		[Header("Timings")] // Timings
		[SerializeField]  private float              syncPeriod = 1.0f;             // This is an internal value used in network to local time conversion.
		[SerializeField]  private float              loadTime = 5.0f;               // This is how much time the video player will expect media to take to load.  Recommended 5s
		[SerializeField]  private float              deviationTolerance = 0.5f;     // If video player somehow differs from the synced time by this amount it will resync locally. (Higher values cause fewer resyncs)
		[SerializeField]  private float              checkSyncEvery = 1.0f;         // This value keeps the time keep method from running too often.  Higher values could lead to errors, lower values could lead to high CPU usage.
		[SerializeField]  private float              videoOvershoot = 0.25f;        // This value helps the video player begin playing at the right spot after seeking.  I advice not to alter this.
		[SerializeField]  private float              resyncEvery = 5.0f;            // This value is how often video player compares local time reference to network time constant.
		[SerializeField]  private float              seekPeriod = 1.0f;             // This is how much time the video player will wait before resuming a video.  High values could cause others to skip.
		[SerializeField]  private float              pauseResyncFor = 2.0f;         // This value prevents the video player from being too aggressive on resync.  Lower values could cause issues on loading videos or seeking.
		[SerializeField]  private float              preloadNextVideoTime = 15.0f;  // How much time before the current video ends to start loading the next video to get ready for gapless switch.

		[Header("Settings")] // Settings
		[SerializeField]  private bool               enableDebug;                   // If toggled off, all info level messages are disabled
		[SerializeField]  private bool               automaticRetry = true;         // If toggled on, video player will automatically retry on failed load
		[SerializeField]  private int                numberOfRetries = 3;           // Number of automatic retries to attempt
		[SerializeField]  private bool               allowRetryWhenLoaded;          // If toggled off, retry won't do anything if the video is successfully loaded
		[SerializeField]  private UdonSharpBehaviour callback;                      // Used for sending events
		[SerializeField]  private string             setStatusMethod;               // Method name for status update events
		[SerializeField]  private string             statusProperty = "statusText"; // Property name for status update events
		[SerializeField]  private bool               startActive = true;            // If toggled off, videos won't load or sync until locally set to active
		
		[Header("Security")] // Security
		[SerializeField]  private bool               masterCanLock = true;          // If toggled on, instance master will be able to lock and unlock the player
		[SerializeField]  private bool               ownerCanLock = true;           // If toggled on, instance owner will be able to lock and unlock the player
		[SerializeField]  private string[]           moderators;                    // List of players who are always allowed to unlock and unlock the player

		// Public Callback Variables
		[HideInInspector] public  int                relayIdentifier;               // Unused value for compatibility.
		[HideInInspector] public  VideoError         relayVideoError;               // Video error identified, copied from video player.
		[HideInInspector] public  float              seekVal;                       // Normalized seek bar value.  Used when seeking.

		// Sync Variables
		[UdonSynced]      private float              remoteTime;                    // Network time based reference for when video began.
		[UdonSynced]      private bool               remoteIsPlaying;               // Network sync boolean for whether video is playing.
		[UdonSynced]      private VRCUrl             remoteURL = VRCUrl.Empty;      // Network sync URL of a video.
		[UdonSynced]      private VRCUrl             remoteNextURL = VRCUrl.Empty;  // Network sync URL of next video
		[UdonSynced]      private int                remotePlayerID;                // Network sync media type.
		                  private float              localTime;                     // Local variables are used for local use of network variables.
		                  private bool               localIsPlaying;                // Before use, they will be checked against for changes.
		                  private VRCUrl             localURL = VRCUrl.Empty;       // Upon change, the proper methods will be called.
		                  private VRCUrl             localNextURL = VRCUrl.Empty;   //
		                  private int                localPlayerID;                 //

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
		private VRCUrl   currentURL;                     // URL of currently loaded video
		private VRCUrl   nextURL;                        // URL of video set to load seemlessly after current
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
		private bool     isResync;                       // Stores whether player is in the process of a resync.
		private bool     postResync;                     // Stores whether we're in the post resync state
		private bool     nextVideoReady;                 // Stores state of queued video
		private bool     nextVideoLoading;               // Stores state of whether queued video is loading
		private bool     isReloadingVideo;               // Stores state of whether the video is reloading.  Used for forcing the video to actually reload.
		private float    lastVideoLoadTime;              // Stores the last time a video was loaded, to prevent any issues loading new videos
		private int      retryCount;                     // Stores number of automatic retries that have happened
		private bool     isAutomaticRetry;               // Stores whether this is an automatic retry
		private bool     urlInvalidUI;                   // Stores whether the input URL is malformed

		private bool     masterLock;                     // Stores state of whether the player is locked

		private bool     hasStatsText;                   // Cached value for whether statisticstext exists
		private bool     hasCallback;                    // Cached value for whether callback exists
		private bool     setStatusEnabled;               // Cached value for whether status can be set without error
		private bool     isActive;                       // Value for whether media player is active or not. Videos will only load/play/sync while the player is active

		// Video Checking
		private string[] videoHosts        = {
			"youtu.be", "drive.google.com", "twitter.com", "vimeo.com",
			"youku.com", "tiktok.com", "nicovideo.jp", "facebook.com"
		};
		private string[] streamHosts       = { "twitch.tv" };
		private string[] musicHosts        = { "soundcloud.com" };
		private string[] videoProtocols    = { "http", "https", "rtmp", "rtspt", "file", "ftp", "gopher", "telnet", "data" };

		private string debugPrefix         = "[<color=#DF004F>Media Player</color>] ";

		private void Start() {
			hasCallback = callback != null;
			hasStatsText = statisticsText != null;
			isActive = startActive;
			setStatusEnabled = callback && !string.IsNullOrWhiteSpace(setStatusMethod) && !string.IsNullOrWhiteSpace(statusProperty);
			if (mediaSelect != null) mediaSelect._SetToggleID(mediaPlayers.GetActiveID());
			if (Networking.LocalPlayer == null) isEditor = true;
			if (volumeBar != null) volumeBar.value = mediaPlayers.GetVolume();
			_SetLoop();
			if (isActive) return;
			// Need to set inactive status here since the update method will be disabled
			SetPlayerStatusText("Inactive");
			UpdateStatus();
		}

		private void Update() {
			if (!isActive) return;
			UpdateVideoPlayer();
			UpdateStatus();
			UpdateSeek();
		}

		// ---------------------- UI Methods ----------------------

		public void _Load() {
			if (!isActive) return;
			if (urlInputField == null) {
				LogError("Cannot read URL Input Field if its null!", this);
				return;
			}
			_LoadURL(urlInputField.GetUrl());
		}

		public void _LoadURL(VRCUrl url) {
			if (!isActive) return;
			int playerID = mediaSelect == null ? mediaPlayers.GetActiveID() : mediaSelect.GetCurrentID();
			_LoadURLAs(url, playerID);
		}

		public int _LoadURLAs(VRCUrl url, int playerID) {
			if (!isActive) return playerID;
			Log("_Load", this);
			// Sanity Check URL
			string urlStr = url != null ? url.ToString() : "";
			if (!SanityCheckURL(urlStr)) return mediaPlayers.GetActiveID(); 
			// Sync new values over the network
			int correctedID = CheckURL(urlStr, playerID);
			if (correctedID < 0) return mediaPlayers.GetActiveID();
			SwitchPlayer(correctedID);
			SetURL(url);
			if (mediaPlayers.GetIsStream()) SetPlaying(true);
			return correctedID;
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

		public void _LoadQueueURLAs(VRCUrl url, int playerID) {
			if (!isActive) return;
			Log("Load Queued", this);
			string urlStr = url != null ? url.ToString() : "";
			if (!SanityCheckURL(urlStr)) return;
			playerID = CheckURL(urlStr, playerID);
			if (playerID == 0) SetNextURL(url);
		}

		// Attempt to load video again
		public void _Retry() {
			if (!isActive) return;
			Log("_Retry", this);
			if (!allowRetryWhenLoaded && urlValid) {
				Log("Video is already successfully loaded", this);
				return;
			}
			ReloadVideoInternal();
		}

		// Call _Play if paused and _Pause if playing
		public void _PlayPause() {
			if (!isActive) return;
			bool playing = !isPlaying;
			Log("_PlayPause: " + (playing ? "True" : "False"), this);
			if (playing) _Play();
			else _Pause();
		}

		// Play a the video if possible.
		public void _Play() {
			if (!isActive) return;
			Log("_Play", this);
			SetPlaying(true);
			if (playFromBeginning) { // This variable is set from video stop or video end, reset it when used
				playFromBeginning = false;
				_SeekTo(0);
			}
		}

		// Pause a video if possible
		public void _Pause() {
			if (!isActive) return;
			Log("_Pause", this);
			SetPlaying(false);
		}

		// Play a video from the beginning if possible
		public void _Start() {
			if (!isActive) return;
			Log("_Start", this);
			SetPlaying(true);
			_SeekTo(0);
		}

		// Stop a currently playing video and unload it
		public void _Stop() {
			if (!isActive) return;
			Log("_Stop", this);
			SetPlaying(false);
			SetURL(VRCUrl.Empty);
		}

		// Reinterpret master time to resync video
		public void _Resync() {
			if (!isActive) return;
			Log("_Resync", this);
			SoftResync();
			lastResyncTime += 5.0f; // This is a slight hack to force the video player to lay easy on the resyncs for a few seconds
		}

		// Set the volume on the video player
		public void _Volume() {
			if (volumeBar == null) return;
			mediaPlayers._SetVolume(volumeBar.value);
		}

		public void _SetVolume(float volume) {
			mediaPlayers._SetVolume(volume);
		}

		// Seek to a different position in a video if possible
		public void _Seek() {
			if (!isActive) return;
			SeekTo(seekVal * mediaPlayers.GetDuration()); // seekVal is normalized so multiply it by media length
		}

		// Seek to a different position in a video if possible
		public void _SeekTo(float time) {
			if (!isActive) return;
			seekBar._SetVal(time / mediaPlayers.GetDuration());
			SeekTo(time);
		}

		// Set whether the video should loop when it finishes
		public void _SetLoop() {
			if (!isActive) return;
			if (loopToggle != null) mediaPlayers._SetLoop(loopToggle.isOn);
		}

		public void _SetLooping(bool loop) {
			if (!isActive) return;
			if (loopToggle != null) loopToggle.isOn = loop;
			else mediaPlayers._SetLoop(loop);
		}

		public void _SetActive(bool active) {
			isActive = active;
			if (isActive) CheckDeserializedData(); // Deserialization is paused and flushed while inactive
			else UnloadMediaAndFlushBuffers(); // Flush buffers and unload media
		}

		public void _Lock() {
			SetLockState(true);
		}

		public void _Unlock() {
			SetLockState(false);
		}

		// ---------------------- Accessors -----------------------

		public string GetStatus() { return playerStatus; }
		public float GetTime() { return isPlaying ? Time.time - startTime : pausedTime; }
		public float GetDuration() { return playerReady ? mediaPlayers.GetDuration() : 0; }
		public float GetTimePrecise() { return mediaPlayers.GetTime(); }
		public bool GetIsPlaying() { return isPlaying; }
		public bool GetIsReady() { return playerReady; }
		public VRCUrl GetCurrentURL() { return currentURL; }
		public VRCUrl GetNextURL() { return nextURL; }
		public float GetVolume() { return mediaPlayers.GetVolume(); }
		public bool GetIsActive() { return isActive; }
		public bool GetIsLooping() { return mediaPlayers.GetLoop(); }
		public int GetMediaType() { return mediaPlayers.GetActiveID(); }
		public bool GetLockStatus() { return masterLock; }

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
			TimeSpan playingTime = TimeSpan.FromSeconds(mediaPlayers.GetTime());
			string timeStr = playingTime.ToString("G");
			timeStr = timeStr.Substring(0, timeStr.Length - 4);
			statisticsText.text = "Playback Time: " + timeStr + "\nStatus: " + playerStatus;
		}

		private void UpdateSeek() {
			if (isSeeking) {
				if (Time.time - lastSeekTime > seekPeriod) isSeeking = false;
				if (Mathf.Abs(mediaPlayers.GetTime() - playerTimeAtSeek) < 0.5f) return;
			}
			if (!isPlaying) return;
			seekBar._SetVal(mediaPlayers.GetTime() / Mathf.Max(0.1f, mediaPlayers.GetDuration()));
		}

		// If anything is playing, unload it, flush buffers
		private void UnloadMediaAndFlushBuffers() {
			// These are some values that will trigger the required logic to
			// execute next time we check the deserialized data
			localTime = -1;
			localIsPlaying = false;
			localPlayerID = 0;
			localURL = VRCUrl.Empty;
			localNextURL = VRCUrl.Empty;
			// Stop and unload all media
			SetPlayingInternal(false);
			SetTimeInternal(0);
			StopInternal();
			// Ensure there's no queued media
			nextURL = VRCUrl.Empty;
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
			if (remotePlayerID != localPlayerID) { // Determines whether we swapped media type
				Log("Deserialization found new Media Player: " + mediaPlayers.GetPlayerName(remotePlayerID), this);
				localPlayerID = remotePlayerID;
				SetPlayerID(localPlayerID);
				SetPlayingInternal(true);
			}
			// Cache local and remote strings so ToString() doesn't get called multiple times
			string localStr = localURL != null ? localURL.ToString() : "";
			string remoteStr = remoteURL != null ? remoteURL.ToString() : "";
			if (!string.Equals(localStr, remoteStr)) { // Load the new video if it has changed
				Log("Deserialization found new URL: " + remoteStr, this);
				Log("Old URL: " + localStr, this);
				localURL = remoteURL;
				SetVideoURLFromLocal();
			}
			// Cache next local and remote strings
			localStr = localNextURL != null ? localNextURL.ToString() : "";
			remoteStr = remoteNextURL != null ? remoteNextURL.ToString() : "";
			if (!string.Equals(localStr, remoteStr)) { // Load the new video if it has changed
				Log("Deserialization found next URL: " + remoteStr, this);
				Log("Old URL: " + localStr, this);
				localNextURL = remoteNextURL;
				SetNextVideoURLFromLocal();
			}
			if (remoteIsPlaying != localIsPlaying) { // Update playing status if changed.
				Log("Deserialization found new playing status: " + (remoteIsPlaying ? "Playing" : "Paused"), this);
				SetPlayingInternal(localIsPlaying = remoteIsPlaying);
			}
			if (Mathf.Abs(remoteTime - localTime) > 0.1f) { // Update local reference time if seek occurred
				Log("Deserialization found new seek position: " + remoteTime, this);
				localTime = remoteTime;
				SoftResync();
			}
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
				if (localPlayerID == 0) SeekInternal(-loadTime);
				else SeekInternal(0);
				LoadURLInternal(localURL);
			}
		}

		private void SetNextVideoURLFromLocal() {
			Log("Decode Next URL: " + localNextURL, this);
			nextURL = localNextURL;
		}

		private void SetPlayerID(int id) {
			Log("Set Media Player: " + mediaPlayers.GetPlayerName(id), this);
			SwitchVideoPlayerInternal(id);
			if (mediaSelect != null) mediaSelect._SetToggleID(id);
		}

		private void SeekTo(float newTime) {
			Log("Seek To: " + newTime, this);
			playerTimeAtSeek = mediaPlayers.GetTime(); // Set a reference value to be able to timeout if seeking takes too long
			lastSeekTime = Time.time; // Video player will essentially pause on this time until it has finished seeking
			isSeeking = true;
			HardResync(newTime);
			if (hasCallback) callback.SendCustomEvent("_RelayVideoSeek");
		}

		private void SoftResync() {
			// Make sure local time is in agreement with remote time, since it can change in certain conditions
			if (Mathf.Abs(remoteTime - localTime) > 0.1f) localTime = remoteTime;
			// Read local values and calculate local reference point
			float seekTime = localIsPlaying ? CalcWithTime(localTime) : localTime;
			if (Time.time - resyncPauseAt < pauseResyncFor) return;
			SeekInternal(seekTime);
			lastResyncTime = Time.time;
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
				if (mediaSelect != null) mediaSelect._SetToggleID(id = 0);
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
			localURL = url;
			SetVideoURLFromLocal();
			Sync();
		}

		private void SetNextURL(VRCUrl url) {
			string localStr = localNextURL != null ? localNextURL.ToString() : "";
			string urlStr = url != null ? url.ToString() : "";
			Log("Set Next URL: " + urlStr, this);
			if (string.Equals(localStr, urlStr)) return;
			localNextURL = url;
			SetNextVideoURLFromLocal();
			Sync();
		}

		private void SetPlaying(bool playing) {
			if (localIsPlaying == playing) return;
			Log("Set Playing: " + (playing ? "Playing" : "Pausing"), this);
			SetPlayingInternal(localIsPlaying = playing);
			HardResync(referencePlayhead);
		}

		private void Sync() {
			remotePlayerID = localPlayerID;
			remoteURL = localURL;
			remoteTime = localTime;
			remoteIsPlaying = localIsPlaying;
			remoteNextURL = localNextURL;
			Networking.SetOwner(Networking.LocalPlayer, gameObject);
			RequestSerialization();
		}

		private float GetServerTime() {
			return (Networking.GetServerTimeInMilliseconds() & 0xFFFFFFF) * 0.001f;
		}

		private float CalcWithTime(float val) {
			float estimate = GetServerTime() - val;
			if (estimate < syncPeriod * -2) estimate += 0xFFFFFFF * 0.001f;
			if (estimate > 86400 * 2) estimate -= 0xFFFFFFF * 0.001f;
			return estimate;
		}

		// ------------------ Video Sync Methods ------------------

		private void UpdateVideoPlayer() {
			if (!isPlaying && playerReady) {
				PauseInternal();
			} else if (mediaPlayers.GetIsStream()) {
				StreamLogic();
			} else if (isPlaying && playerReady) {
				ResyncLogic();
			}
			if (isPlaying && !mediaPlayers.GetIsStream()) PreloadLogic();
			if (isReloadingVideo && Time.time > lastVideoLoadTime + 5.0f) {
				SetPlayerStatusText("Reloading Video");
				SetVideoURLFromLocal();
			}
		}

		private void StreamLogic() {
			if (isPlaying && !mediaPlayers.GetPlaying()) {
				if (mediaPlayers.GetReady()) {
					SetPlayerStatusText("Playing Stream");
					Log("Playing Stream", this);
					mediaPlayers._Play();
				} else if (!isLoading) {
					SetPlayerStatusText("Loading Stream");
					string urlStr = currentURL != null ? currentURL.ToString() : "";
					Log("Loading Stream URL: " + urlStr, this);
					mediaPlayers._PlayURL(currentURL);
					if (hasCallback && !isLoading) callback.SendCustomEvent("_RelayVideoLoading");
					isLoading = true;
				}
			}
		}

		private void PreloadLogic() {
			if (nextURL == null || nextURL == VRCUrl.Empty) return;
			if (mediaPlayers.GetDuration() - mediaPlayers.GetTime() > preloadNextVideoTime) return;
			mediaPlayers._LoadNextURL(nextURL);
			nextVideoLoading = true;
		}

		private void ResyncLogic() {
			referencePlayhead = Time.time - startTime;
			float currentTime = mediaPlayers.GetTime();
			deviation = currentTime - referencePlayhead;
			float absDeviation = Mathf.Abs(deviation);
			float timeMinusLastSoftSync = Time.time - lastSoftSyncTime;
			if (postResync) {
				if (Time.time < postResyncEndsAt) return;
				postResync = false;
				if (absDeviation <= deviationTolerance) {
					SetPlayerStatusText("Playing");
					return;
				}
				float overshoot = Mathf.Clamp(-deviation, videoOvershoot * 2, 15);
				Resync(currentTime, referencePlayhead + overshoot);
				Log("Post Resync Compensation: " + overshoot, this);
				SetPlayerStatusText("Catching Up");
				return;
			}
			if (isResync) {
				isResync = (Mathf.Abs(currentTime - playerTimeAtResync) < 2.0f || timeMinusLastSoftSync < checkSyncEvery) && timeMinusLastSoftSync < 15.0f;
				if (!isResync) {
					postResync = true;
					postResyncEndsAt = Time.time + (timeMinusLastSoftSync + checkSyncEvery * 0.5f);
					SetPlayerStatusText("Stabilizing");
				}
			}
			if (isResync) return;
			if (Time.time - lastResyncTime >= resyncEvery) SoftResync();
			if (Time.time - lastCheckTime < checkSyncEvery) return;
			if (Time.time - resyncPauseAt < pauseResyncFor) return;
			lastCheckTime = Time.time;
			if (mediaPlayers.GetPlaying()) {
				if (absDeviation > deviationTolerance) {
					Resync(currentTime, referencePlayhead + videoOvershoot);
					SetPlayerStatusText("Syncing");
				} else if (deviation > videoOvershoot * 2) {
					mediaPlayers._Pause();
					SetPlayerStatusText("Waiting For Playhead");
				}
			} else {
				if (absDeviation > deviationTolerance) Resync(currentTime, referencePlayhead + videoOvershoot);
				if (deviation <= videoOvershoot / 2) {
					mediaPlayers._Play();
					SetPlayerStatusText("Playing");
				}
			}
		}

		private void Resync(float oldTime, float newTime) {
			SetTimeInternal(newTime);
			isResync = true;
			lastSoftSyncTime = Time.time;
			playerTimeAtResync = oldTime;
		}

		private void SetPlayingInternal(bool playing) {
			Log("Set Playing Internal: " + (playing ? "Playing" : "Paused"), this);
			isPlaying = playing;
			if (!mediaPlayers.GetIsStream()) SeekInternal(referencePlayhead);
			SetPlayerStatusText(playing ? "Waiting to Play" : "Paused");
		}

		private void PauseInternal() {
			if (!mediaPlayers.GetReady()) return;
			if (!mediaPlayers.GetPlaying()) return;
			Log("Pause Internal", this);
			mediaPlayers._Pause();
			SetTimeInternal(pausedTime);
			deviation = 0;
			SetPlayerStatusText("Paused");
		}

		private void StopInternal() {
			Log("Stop", this);
			isPlaying = false;
			if (Networking.IsMaster) SetPlaying(false);
			currentURL = VRCUrl.Empty;
			urlValid = false;
			SetPlayerStatusText("No Video");
			playFromBeginning = true;
		}

		private void LoadURLInternal(VRCUrl url) {
			string urlStr = url != null ? url.ToString() : "";
			Log("Load URL: " + urlStr, this);
			string thisStr = currentURL != null ? currentURL.ToString() : "";
			if (!isReloadingVideo && string.Equals(urlStr, thisStr)) {
				LogWarning("URL already loaded! Ignoring.", this);
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
			lastVideoLoadTime = Time.time + UnityEngine.Random.value; // Adds a random value to the last video load time to randomize the time interval between loads
			if (hasCallback && !isLoading) callback.SendCustomEvent("_RelayVideoLoading");
			isLoading = true;
			SetPlayerStatusText("Loading Video" + suffix);
		}

		private void SeekInternal(float time) {
			startTime = Time.time - time;
			referencePlayhead = pausedTime = urlValid || isPlaying ? time : 0;
		}

		// ------------------- Callback Methods -------------------

		public void _RelayVideoReady() {
			if (!isActive) return;
			urlValid = true;
			SetPlayerStatusText("Ready");
			playerReady = true;
			isLoading = false;
			if (mediaPlayers.GetIsStream()) mediaPlayers._Play();
			if (hasCallback) callback.SendCustomEvent("_RelayVideoReady");
		}

		public void _RelayVideoEnd() {
			if (!isActive) return;
			isPlaying = false;
			if (Networking.IsMaster) SetPlaying(false);
			SetPlayerStatusText("Stopped");
			playFromBeginning = true;
			if (hasCallback) callback.SendCustomEvent("_RelayVideoEnd");
			if (mediaPlayers.GetIsStream()) return;
			SeekInternal(0);
			// Finish video callback?
		}

		public void _RelayVideoError() {
			if (!isActive) return;
			string errorString = GetErrorString(relayVideoError);
			SetPlayerStatusText("Error: " + errorString);
			playerReady = false;
			if (automaticRetry) {
				if (relayVideoError == VideoError.AccessDenied) {
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
			if (!isActive) return;
			SetPlayerStatusText("Playing");
			playerReady = true;
			playFromBeginning = false;
			isLoading = false;
			if (hasCallback) callback.SendCustomEvent("_RelayVideoStart");
		}

		public void _RelayVideoPlay() {
			if (!isActive) return;
			SetPlayerStatusText("Playing");
			playerReady = true;
			playFromBeginning = false;
			isLoading = false;
			if (hasCallback) callback.SendCustomEvent("_RelayVideoPlay");
		}

		public void _RelayVideoPause() {
			if (!isActive) return;
			string urlText = currentURL == null ? "" : currentURL.ToString();
			SetPlayerStatusText(string.IsNullOrWhiteSpace(urlText) ? "No Video" : playFromBeginning ? "Stopped" : "Paused");
			if (hasCallback) callback.SendCustomEvent("_RelayVideoPause");
		}

		public void _RelayVideoLoop() {
			if (!isActive) return;
			resyncPauseAt = Time.time;
			if (Networking.IsMaster) HardResync(mediaPlayers.GetTime());
			if (hasCallback) callback.SendCustomEvent("_RelayVideoLoop");
		}

		public void _RelayVideoNext() {
			if (!isActive) return;
			// Queued video is starting
			resyncPauseAt = Time.time;
			if (Networking.IsMaster) HardResync(mediaPlayers.GetTime());
			// Get new duration!
			nextVideoLoading = nextVideoReady = false;
			if (Networking.IsMaster) SetNextURL(VRCUrl.Empty);
			if (hasCallback) callback.SendCustomEvent("_RelayVideoNext");
		}

		public void _RelayVideoQueueError() {
			if (!isActive) return;
			// Queued video player has thrown an error
			SetPlayerStatusText("Error: " + GetErrorString(relayVideoError));
			nextVideoLoading = nextVideoReady = false;
			if (Networking.IsMaster) SetNextURL(VRCUrl.Empty);
			// Get ready to stop?
			// Possibly skip video?
			if (hasCallback) callback.SendCustomEvent("_RelayVideoQueueError");
		}

		public void _RelayVideoQueueReady() {
			if (!isActive) return;
			// Queued video has loaded
			nextVideoLoading = false;
			nextVideoReady = true;
			if (hasCallback) callback.SendCustomEvent("_RelayVideoQueueReady");
		}

		// -------------------- Utility Methods -------------------

		// Checks URL to see if it needs to swap Player ID
		private int CheckURL(string urlStr, int playerID) {
			Log("CheckURL", this);
			int newPlayerID = playerID;
			int colonPos = urlStr.IndexOf("://", StringComparison.Ordinal);
			if (colonPos < 1 || urlStr.Length < colonPos + 5) {
				LogError("Malformed URL", this);
				urlInvalidUI = true;
				return -1;
			}
			int prefixLength = urlStr.IndexOf('/', colonPos + 3);
			if (prefixLength < 1) {
				LogError("Malformed URL", this);
				urlInvalidUI = true;
				return -1;
			}
			string urlProtocol = urlStr.Substring(0, colonPos).ToLower();
			bool isAllowedProtocol = false;
			foreach (string protocol in videoProtocols) if (string.Equals(urlProtocol, protocol)) { isAllowedProtocol = true; break; }
			if (!isAllowedProtocol) {
				Log("Invalid Protocol: " + urlProtocol, this);
				urlInvalidUI = true;
				return -1;
			}
			Log("URL Protocol: " + urlProtocol, this);
			string urlHost = urlStr.Substring(colonPos + 3, prefixLength - 3 - colonPos);
			Log("URL Host: " + urlHost, this);
			foreach (string host in videoHosts) if (string.Equals(urlHost, host)) { newPlayerID = 0; break; }
			foreach (string host in streamHosts) if (string.Equals(urlHost, host)) { newPlayerID = 1; break; }
			if (string.Equals(urlHost, "youtube.com") || string.Equals(urlHost, "www.youtube.com")) {
				string parameters = urlStr.Substring(prefixLength + 1, urlStr.Length - prefixLength - 1);
				Log("URL Parameters: " + parameters, this);
				foreach (string verb in new string[] { "playlist", "watch" })
					if (parameters.IndexOf(verb, StringComparison.Ordinal) == 0) newPlayerID = 0;
				if (string.Equals(parameters.Substring(parameters.Length - 4, 4), "live")) newPlayerID = 1;
			}
			if (urlStr.Substring(urlStr.Length - 5, 5).Equals(".m3u8")) newPlayerID = 1;
			if (string.Equals(urlProtocol, "rtmp")) newPlayerID = 1;
			if (string.Equals(urlProtocol, "rtspt")) newPlayerID = 2;
			if (newPlayerID != playerID)
				if (isEditor) {
					Log("Cannot play stream in editor! Current Player: " + mediaPlayers.GetPlayerName(0), this);
					newPlayerID = 0;
				} else {
					LogWarning("URL not appropriate for specified player, switching from " +
					           mediaPlayers.GetPlayerName(playerID) + " to " + mediaPlayers.GetPlayerName(newPlayerID), this);
				}
			return newPlayerID;
		}

		private void SetPlayerStatusText(string status) {
			if (string.Equals(status, playerStatus)) return;
			playerStatus = status;
			if (!setStatusEnabled) return;
			callback.SetProgramVariable(statusProperty, status);
			callback.SendCustomEvent(setStatusMethod);
		}

		// Returns time in video, conditionally pulling it from the player or
		// from the paused time.
		private float GetTimeInternal() {
			if (isPlaying) return mediaPlayers.GetTime();
			return pausedTime;
		}

		private void SetTimeInternal(float time) {
			if (mediaPlayers.GetIsStream()) return;
			mediaPlayers._SetTime(time > 0 ? time : 0);
		}

		private void SwitchVideoPlayerInternal(int id) {
			mediaPlayers._SwitchPlayer(id);
			bool isStream = mediaPlayers.GetIsStream();
			seekBar._SetEnabled(!isStream);
		}

		// Reload video properly
		private void ReloadVideoInternal() {
			isReloadingVideo = true; // Set this so the player doesn't reject our request to reload the video
			if (Time.time < lastVideoLoadTime + 5.0f) { // Cancel video load if we recently attempted to load a video
				SetPlayerStatusText("Waiting to Retry Load");
				return;
			}
			SetPlayerStatusText("Reloading Video");
			SetVideoURLFromLocal();
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
		
		// ------------------- Security Methods -------------------

		private bool CheckPrivileged(VRCPlayerApi vrcPlayer) {
			if (vrcPlayer == null) return true;
			if (vrcPlayer.isMaster) return true;
			if (vrcPlayer.isInstanceOwner) return true;
			string playerName = vrcPlayer.displayName;
			foreach (string moderator in moderators)
				if (string.Equals(playerName, moderator))
					return true;
			return false;
		}

		private void SetLockState(bool lockState) {
			if (CheckPrivileged(Networking.LocalPlayer)) return;
			masterLock = lockState;
			// TODO: Announce lock state to various parts of the player and to callback behaviours
		}

		// ----------------- Debug Helper Methods -----------------
		private void Log(string message, UnityEngine.Object context) { if (enableDebug) Debug.Log(debugPrefix + message, context); }
		private void LogWarning(string message, UnityEngine.Object context) { Debug.LogWarning(debugPrefix + message, context); }
		private void LogError(string message, UnityEngine.Object context) { Debug.LogError(debugPrefix + message, context); }
	}
}
