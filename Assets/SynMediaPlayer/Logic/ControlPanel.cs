
using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

namespace Synergiance.MediaPlayer.UI {
	public class ControlPanel : UdonSharpBehaviour {
		[SerializeField] private MediaPlayer mediaPlayer;
		[SerializeField] private VolumeControl volumeControl;
		[SerializeField] private SliderTypeSelect selectionSlider;
		[SerializeField] private VRCUrlInputField urlField;
		[SerializeField] private Text urlPlaceholderField;
		[SerializeField] private StatefulButton playPauseStopButton;
		[SerializeField] private StatefulButton playPauseButton;
		[SerializeField] private StatefulButton stopButton;
		[SerializeField] private StatefulButton lockUnlockButton;
		[SerializeField] private StatefulButton powerButton;
		[SerializeField] private StatefulButton loopButton;
		[SerializeField] private StatefulButton refreshButton;
		[SerializeField] private MultiText statusField;
		[SerializeField] private MultiText timeField;
		[SerializeField] private InputField prevUrlField;
		[SerializeField] private InputField currentUrlField;
		[SerializeField] private Text moderatorListField;
		[SerializeField] private Text currentOwnerField;
		[SerializeField] private bool combineStatusAndTime;
		[SerializeField] private float updatesPerSecond = 5;
		[SerializeField] private float reloadAvailableFor = 5;
		[SerializeField] private bool enableDebug;
		[SerializeField] private bool verboseDebug;
		[SerializeField] private bool loadGapless;
		[SerializeField] private bool autoplay = true;
		[Range(5,50)] [SerializeField] private int maxVideosInQueue = 20;

		[SerializeField] private VRCUrl[] defaultPlaylist;

		[UdonSynced] private VRCUrl[] videoQueueRemote;
		private VRCUrl[] videoQueueLocal;

		[HideInInspector] public float volumeVal;
		[HideInInspector] public int mediaTypeVal;
		[HideInInspector] public string statusText;

		private bool initialized;
		private bool isValid;
		private int mediaType;
		private bool isStream;
		private float timeBetweenUpdates;
		private float lastSlowUpdate;
		private float lastResync;
		private bool hideTime;
		private bool queueCheckURL;
		private bool reachedEnd;
		private bool hasActivated;

		[UdonSynced] private bool isDefaultPlaylist = true;
		[UdonSynced] private int currentDefaultIndex;

		private string[] modList;
		private int[] modIdList;

		private int hardQueueCap = 100;

		private string debugPrefix = "[<color=#20C0A0>SMP Control Panel</color>] ";

		void Start() {
			Initialize();
		}

		private void Initialize() {
			if (initialized) return;
			Log("Initializing", this);
			isValid = true;
			if (!mediaPlayer) {
				isValid = false;
				LogWarning("Media Player not set!", this);
			}
			// Keep UPS to between 50 per second to one every 10 seconds
			timeBetweenUpdates = Mathf.Max(0.02f, 1 / Mathf.Max(0.1f, updatesPerSecond));
			if (volumeControl && isValid) volumeControl._SetVolume(mediaPlayer.GetVolume());
			// ReSharper disable once ConditionIsAlwaysTrueOrFalse
			if (maxVideosInQueue < 1) maxVideosInQueue = 1;
			if (maxVideosInQueue > hardQueueCap) maxVideosInQueue = hardQueueCap;
			initialized = true;
			UpdateMethods();
			RebuildModList();
			UpdateModList();
		}

		// This function is what will be called on first activation when the video player decides its right and
		// after its done all of its initialization.
		private void Activate() {
			Log("First Activation", this);
			if (!mediaPlayer.GetIsPlaying()) InitializeDefaultPlaylist();
			SendCustomEventDelayedSeconds("_SlowUpdate", timeBetweenUpdates);
			UpdateMethods();
			hasActivated = true;
			CheckDeserialization();
		}

		public void _SlowUpdate() {
			if (Time.time < lastSlowUpdate + timeBetweenUpdates * 0.9f) return;
			lastSlowUpdate = Time.time;
			SendCustomEventDelayedSeconds("_SlowUpdate", timeBetweenUpdates);
			UpdateMethods();
		}

		private void UpdateMethods() {
			UpdateTimeAndStatus();
			UpdateAllButtons();
			UpdateCurrentOwner();
		}

		public void _ClickPlayPauseStop() {
			Initialize();
			if (!isValid) {
				LogInvalid();
				return;
			}
			if (mediaPlayer.GetMediaType() == 0) { // Media Type 0 is video
				mediaPlayer._PlayPause();
			} else { // Media Type 1-2 is stream
				if (mediaPlayer.GetIsPlaying()) mediaPlayer._Stop();
				else mediaPlayer._Play();
			}
			UpdatePlayPauseStopButtons();
		}

		public void _ClickPlayPause() {
			Initialize();
			if (!isValid) {
				LogInvalid();
				return;
			}
			mediaPlayer._PlayPause();
			UpdatePlayPauseStopButtons();
		}

		public void _ClickPlay() {
			Initialize();
			if (!isValid) {
				LogInvalid();
				return;
			}
			mediaPlayer._Play();
			UpdatePlayPauseStopButtons();
		}

		public void _ClickPause() {
			Initialize();
			if (!isValid) {
				LogInvalid();
				return;
			}
			mediaPlayer._Pause();
			UpdatePlayPauseStopButtons();
		}

		public void _ClickStop() {
			Initialize();
			if (!isValid) {
				LogInvalid();
				return;
			}
			mediaPlayer._Stop();
			UpdatePlayPauseStopButtons();
		}

		public void _ClickPower() {
			Initialize();
			if (!isValid) {
				LogInvalid();
				return;
			}
			bool active = !mediaPlayer.GetIsActive();
			Log("Setting active: " + active, this);
			mediaPlayer._SetActive(active);
			UpdatePowerButton();
		}

		public void _ClickLoop() {
			Initialize();
			if (!isValid) {
				LogInvalid();
				return;
			}
			mediaPlayer._SetLooping(!mediaPlayer.GetIsLooping());
			UpdateLoopButton();
		}

		public void _ClickLock() {
			Initialize();
			if (!isValid) {
				LogInvalid();
				return;
			}
			if (mediaPlayer.GetLockStatus()) mediaPlayer._Unlock();
			else mediaPlayer._Lock();
		}

		public void _SetVolume() {
			Initialize();
			if (!isValid) {
				LogInvalid();
				return;
			}
			mediaPlayer._SetVolume(volumeVal);
		}

		public void _ClickResync() {
			Initialize();
			if (!isValid) return;
			if (mediaPlayer.GetIsReady() && Time.time > lastResync + reloadAvailableFor) {
				mediaPlayer.Resync();
				lastResync = Time.time;
			} else {
				mediaPlayer._Retry();
				lastResync = Time.time - reloadAvailableFor;
			}
		}

		public void _ClickDiagnostics() {
			if (!isValid) return;
			if (mediaPlayer.GetIsLoggingDiagnostics())
				mediaPlayer._CancelDiagnostics();
			else
				mediaPlayer._StartDiagnostics();
		}

		public void _SetMediaType() {
			Initialize();
			if (!isValid) {
				LogInvalid();
				return;
			}
			if (mediaTypeVal > 2 || mediaTypeVal < 0) {
				LogError("Media Type out of bounds!", this);
				return;
			}
			mediaType = mediaTypeVal;
		}

		public void _Load() {
			Initialize();
			if (!isValid) {
				LogInvalid();
				return;
			}
			if (mediaPlayer.GetLockStatus() && !mediaPlayer.HasPermissions()) {
				LogWarning("Not permitted to load a new URL", this);
				return;
			}
			if (!urlField) {
				LogError("Url Field not set!", this);
				return;
			}
			if (string.IsNullOrWhiteSpace(urlField.GetUrl().ToString())) {
				LogError("URL is empty!", this);
				UpdateMediaTypeSlider();
				return;
			}
			CancelDefaultPlaylist();
			int loadedType = mediaType;
			VRCUrl newUrl = urlField.GetUrl();
			if (loadGapless && mediaPlayer.GetIsPlaying()) {
				if (newUrl != null) Log("Load Queue URL: " + newUrl.ToString(), this);
				mediaPlayer._LoadQueueURL(newUrl);
				mediaPlayer._PlayNext();
			} else {
				if (newUrl != null) Log("Load URL: " + newUrl.ToString(), this);
				loadedType = mediaPlayer._LoadURLAs(newUrl, mediaType);
			}
			urlField.SetUrl(VRCUrl.Empty);
			if (loadedType == mediaType) return;
			mediaType = loadedType;
			UpdateMediaTypeSlider();
		}

		public void _CheckURL() {
			if (queueCheckURL) return;
			queueCheckURL = true;
			SendCustomEventDelayedFrames("QueueCheckURL", 0);
		}

		public void QueueCheckURL() {
			queueCheckURL = false;
			if (!urlField) return;
			VRCUrl url = urlField.GetUrl();
			if (url == null) return;
			string urlStr = url.ToString();
			if (string.IsNullOrWhiteSpace(urlStr)) return;
			int newType = mediaPlayer.GetUrlId(urlStr, mediaType);
			if (newType < 0 || newType > 2) return;
			UpdateMediaTypeSlider();
		}

		private void LogInvalid() {
			LogError("Not properly initialized!", this);
		}

		private void UpdateAllButtons() {
			UpdatePlayPauseStopButtons();
			UpdateResyncButton();
			UpdatePowerButton();
			UpdateLoopButton();
		}

		private void UpdatePlayPauseStopButtons() {
			if (playPauseButton) playPauseButton._SetMode(mediaPlayer.GetIsPlaying() ? 1 : 0);
			if (playPauseStopButton) {
				bool isPlaying = mediaPlayer.GetIsPlaying();
				bool stream = mediaPlayer.GetMediaType() != 0;
				playPauseStopButton._SetMode(isPlaying ? stream ? 2 : 1 : 0);
			}
		}

		private void UpdateResyncButton() {
			if (!isValid || !refreshButton) return;
			int loaded = mediaPlayer.GetIsReady() ? 1 : 0;
			int syncing = mediaPlayer.GetIsSyncing() ? 2 : 0;
			if (loaded == 1 && Time.time <= lastResync + reloadAvailableFor) loaded = 0;
			refreshButton._SetMode(loaded + syncing);
		}

		private void UpdatePowerButton() {
			if (!isValid || !powerButton) return;
			powerButton._SetMode(mediaPlayer.GetIsActive() ? 0 : 1);
		}

		private void UpdateLoopButton() {
			if (!isValid || !loopButton) return;
			loopButton._SetMode(mediaPlayer.GetIsLooping() ? 1 : 0);
		}

		private void UpdateMediaTypeSlider() {
			if (selectionSlider) selectionSlider._SetType(mediaType);
		}

		private void UpdateCurrentOwner() {
			if (!isValid || !currentOwnerField) return;
			VRCPlayerApi owner = Networking.GetOwner(mediaPlayer.gameObject);
			string newOwnerName = owner != null ? owner.displayName : "Nobody";
			string oldOwnerName = currentOwnerField.text;
			if (string.Equals(newOwnerName, oldOwnerName)) return;
			currentOwnerField.text = newOwnerName;
		}

		private void UpdateTimeAndStatus() {
			// TODO: Allow splitting time and status
			string textToDisplay = "00:00:00/00:00:00";
			if (isValid) {
				if (hideTime) return;
				float duration = mediaPlayer.GetDuration();
				float currentTime = mediaPlayer.GetTime();
				textToDisplay = FormatTime(currentTime);
				if (Single.IsNaN(duration) || Single.IsInfinity(duration)) textToDisplay = "Live";
				else if (duration > 0.01f) textToDisplay += "/" + FormatTime(duration);
			}
			statusField._SetText(textToDisplay);
		}

		private string FormatTime(float time) {
			if (Single.IsInfinity(time) || Single.IsNaN(time)) return time.ToString();
			float wTime = Mathf.Abs(time);
			bool neg = time < 0;
			string str = ((int)wTime % 60).ToString("D2");
			wTime /= 60;
			bool hasHours = wTime >= 60;
			str = ((int)wTime % 60).ToString(hasHours ? "D2" : "D1") + ":" + str;
			if (!hasHours) return neg ? "-" + str : str;
			wTime /= 60;
			bool hasDays = wTime >= 24;
			str = ((int)wTime % 24).ToString(hasDays ? "D2" : "D1") + ":" + str;
			if (!hasDays) return neg ? "-" + str : str;
			wTime /= 24;
			str = (int)wTime + ":" + str;
			return neg ? "-" + str : str;
		}

		private void UpdateModList() {
			if (!moderatorListField) return;
			string str;
			if (Networking.LocalPlayer == null) {
				str = "Debug User";
			} else {
				if (modList == null || modList.Length == 0) RebuildModList();
				if (modList.Length <= 0) {
					str = "No Moderators Present!";
				} else {
					str = modList[0];
					for (int i = 1; i < modList.Length; i++) str += ", " + modList[i];
				}
			}
			moderatorListField.text = str;
		}

		private void UpdateUrls() {
			if (!isValid) return;
			VRCUrl url = mediaPlayer.GetCurrentURL();
			if (url == null || string.IsNullOrWhiteSpace(url.ToString())) return;
			if (!currentUrlField) return;
			if (prevUrlField) prevUrlField.text = currentUrlField.text;
			currentUrlField.text = url.ToString();
		}

		private void SuppressSecurity() {
			if (mediaPlayer.HasPermissions()) return;
			LogVerbose("Suppress Security", this);
			mediaPlayer._SuppressSecurity(Time.time);
		}

		// --------------- Default Playlist Methods ---------------

		private void InitializeDefaultPlaylist() {
			if (!isValid) return;
			if (!isDefaultPlaylist) return;
			VRCPlayerApi localPlayer = Networking.LocalPlayer;
			if (localPlayer != null && !localPlayer.isMaster) return;
			LogVerbose("Initialize Default Playlist", this);
			if (defaultPlaylist == null || defaultPlaylist.Length < 1) {
				Log("Default playlist empty, disabling", this);
				isDefaultPlaylist = false;
				RequestSerialization();
				return;
			}
			if (!mediaPlayer.HasPermissions()) SuppressSecurity();
			mediaPlayer._LoadURL(defaultPlaylist[0]);
			reachedEnd = false;
			if (autoplay) mediaPlayer._Play();
			mediaPlayer.EngageSecurity();
			PreloadNextDefaultItem();
			if (!isDefaultPlaylist) return;
			PreloadNextDefaultItem();
		}

		private void PreloadNextDefaultItem() {
			if (!isValid || !isDefaultPlaylist) return;
			VRCPlayerApi localPlayer = Networking.LocalPlayer;
			if (localPlayer != null && !localPlayer.isMaster) return;
			Log("Preload Next Default Item", this);
			currentDefaultIndex++;
			if (currentDefaultIndex >= defaultPlaylist.Length) currentDefaultIndex = 0;
			AddToQueueInternal(defaultPlaylist[currentDefaultIndex]);
		}

		private void CancelDefaultPlaylist() {
			if (!isDefaultPlaylist) return;
			LogVerbose("Cancel Default Playlist", this);
			isDefaultPlaylist = false;
			ClearQueueInternal();
			RequestSerialization();
		}

		// -------------------- Queue Methods ---------------------

		private void AddToQueue(VRCUrl url) {
			LogVerbose("Add To Queue", this);
			if (!isValid) return;
			if (!mediaPlayer.HasPermissions()) {
				LogWarning("Cannot add video to queue! Permission denied!", this);
				return;
			}
			CancelDefaultPlaylist();
			AddToQueueInternal(url);
		}

		private void ClearQueue() {
			LogVerbose("Clear Queue", this);
			if (!isValid) return;
			if (!mediaPlayer.HasPermissions()) {
				LogWarning("Cannot clear queue! Permission denied!", this);
				return;
			}
			CancelDefaultPlaylist();
			ClearQueueInternal();
		}

		private void RemoveFromQueue(int index) {
			LogVerbose("Remove From Queue", this);
			if (!isValid) return;
			if (!mediaPlayer.HasPermissions()) {
				LogWarning("Cannot remove video from queue! Permission denied!", this);
				return;
			}
			CancelDefaultPlaylist();
			RemoveFromQueueInternal(index);
		}

		private void InsertToQueue(VRCUrl url, int index) {
			LogVerbose("Insert To Queue", this);
			if (!isValid) return;
			if (!mediaPlayer.HasPermissions()) {
				LogWarning("Cannot insert video into queue! Permission denied!", this);
				return;
			}
			CancelDefaultPlaylist();
			InsertToQueueInternal(url, index);
		}

		private void AddToQueueInternal(VRCUrl url) {
			if (videoQueueLocal != null && videoQueueLocal.Length >= maxVideosInQueue) {
				LogWarning("Cannot add video to queue! Queue length exceeded!", this);
				return;
			}
			LogVerbose("Add To Queue Internal: " + url, this);
			VRCUrl[] tempUrls = new VRCUrl[videoQueueLocal == null ? 1 : videoQueueLocal.Length + 1];
			if (tempUrls.Length > 1) Array.Copy(videoQueueLocal, tempUrls, videoQueueLocal.Length);
			tempUrls[tempUrls.Length - 1] = url;
			videoQueueLocal = tempUrls;
			if (tempUrls.Length == 1) {
				if (reachedEnd) {
					InsertNextUrl();
					videoQueueLocal = null;
				} else if (loadGapless) {
					UpdateNextUrl();
				}
			}
			Sync();
			UpdateQueueUI();
		}

		private void ClearQueueInternal() {
			if (videoQueueLocal == null || videoQueueLocal.Length == 0) {
				Log("Cannot clear queue! Queue already empty!", this);
				return;
			}
			LogVerbose("Clear Queue Internal", this);
			videoQueueLocal = null;
			Sync();
			if (loadGapless) UpdateNextUrl();
			UpdateQueueUI();
		}

		private void RemoveFromQueueInternal(int index) {
			if (index < 0 || videoQueueLocal == null || index >= videoQueueLocal.Length) {
				LogWarning("Cannot remove video from queue! Index out of bounds!", this);
				return;
			}
			VRCUrl[] tempUrls;
			if (videoQueueLocal.Length == 1) {
				tempUrls = null;
			} else {
				tempUrls = new VRCUrl[videoQueueLocal.Length - 1];
				if (index > 0) Array.Copy(videoQueueLocal, tempUrls, index);
				if (index < videoQueueLocal.Length - 1) Array.Copy(videoQueueLocal, index + 1, tempUrls, index, tempUrls.Length - index);
			}
			LogVerbose("Remove From Queue Internal: " + index, this);
			videoQueueLocal = tempUrls;
			Sync();
			if (index == 0 && loadGapless) UpdateNextUrl();
			UpdateQueueUI();
		}

		private void InsertToQueueInternal(VRCUrl url, int index) {
			VRCUrl[] tempUrls;
			if (videoQueueLocal == null || videoQueueLocal.Length == 0) {
				if (index != 0) {
					LogWarning("Cannot insert video into queue! Index out of bounds!", this);
					return;
				}
				AddToQueueInternal(url);
				return;
			}
			if (videoQueueLocal.Length >= maxVideosInQueue) {
				LogWarning("Cannot insert video into queue! Queue length exceeded!", this);
				return;
			}
			if (index > videoQueueLocal.Length || index < 0) {
				LogWarning("Cannot insert video into queue! Index out of bounds!", this);
				return;
			}
			tempUrls = new VRCUrl[videoQueueLocal.Length + 1];
			if (index > 0) Array.Copy(videoQueueLocal, tempUrls, index);
			if (index < videoQueueLocal.Length) Array.Copy(videoQueueLocal, index, tempUrls, index + 1, videoQueueLocal.Length - index);
			LogVerbose("Insert To Queue Internal: " + index + ", " + url, this);
			tempUrls[index] = url;
			videoQueueLocal = tempUrls;
			Sync();
			if (index == 0 && loadGapless) UpdateNextUrl();
			UpdateQueueUI();
		}

		private void UpdateQueue() {
			LogVerbose("Update Queue", this);
			int oldLen = videoQueueLocal != null ? videoQueueLocal.Length : 0;
			int newLen = videoQueueRemote != null ? videoQueueRemote.Length : 0;
			bool firstUrlChanged = oldLen == 0 && newLen > 0 || oldLen > 0 && newLen == 0;
			if (oldLen > 0 && newLen > 0) firstUrlChanged = string.Equals(videoQueueLocal[0].ToString(), videoQueueRemote[0].ToString());
			if (!isValid || !Networking.IsOwner(mediaPlayer.gameObject)) firstUrlChanged = false;
			videoQueueLocal = videoQueueRemote;
			if (firstUrlChanged && loadGapless) UpdateNextUrl();
			UpdateQueueUI();
		}

		private void UpdateNextUrl() {
			if (!isValid) return;
			if (!mediaPlayer.HasPermissions() && !Networking.IsOwner(mediaPlayer.gameObject)) return;
			VRCUrl nextUrl = VRCUrl.Empty;
			if (videoQueueLocal != null && videoQueueLocal.Length > 0) {
				nextUrl = videoQueueLocal[0];
			}
			LogVerbose("Update Next URL: " + nextUrl, this);
			if (!mediaPlayer.HasPermissions()) SuppressSecurity();
			mediaPlayer._LoadQueueURL(nextUrl);
			mediaPlayer.EngageSecurity();
		}

		private void InsertNextUrl() {
			if (!isValid) return;
			if (!mediaPlayer.HasPermissions() && !Networking.IsOwner(mediaPlayer.gameObject)) return;
			if (videoQueueLocal == null || videoQueueLocal.Length <= 0) {
				LogWarning("Attempting to insert new URL when queue is empty!", this);
				return;
			}
			LogVerbose("Insert Next URL: " + videoQueueLocal[0], this);
			if (!mediaPlayer.HasPermissions()) SuppressSecurity();
			mediaPlayer._LoadURL(videoQueueLocal[0]);
			mediaPlayer.EngageSecurity();
			reachedEnd = false;
			if (autoplay || isDefaultPlaylist) mediaPlayer._Play();
		}

		private void AdvanceQueue() {
			if (!Networking.IsOwner(mediaPlayer.gameObject)) return;
			LogVerbose("Advance Queue", this);
			if (videoQueueLocal == null || videoQueueLocal.Length <= 0) {
				Log("No more videos in queue!", this);
				if (videoQueueRemote != null) Sync();
				return;
			}
			if (videoQueueLocal.Length == 1) {
				if (!loadGapless) InsertNextUrl();
				videoQueueLocal = null;
				if (videoQueueRemote != null) Sync();
				if (loadGapless) UpdateNextUrl();
				UpdateQueueUI();
				PreloadNextDefaultItem();
				Sync();
				return;
			}
			if (!loadGapless) InsertNextUrl();
			VRCUrl[] tempUrls = new VRCUrl[videoQueueLocal.Length - 1];
			Array.Copy(videoQueueLocal, 1, tempUrls, 0, tempUrls.Length);
			videoQueueLocal = tempUrls;
			Sync();
			if (loadGapless) UpdateNextUrl();
			UpdateQueueUI();
			PreloadNextDefaultItem();
			Sync();
		}

		private void UpdateQueueUI() {
			// TODO: Implement
		}
		
		// ----------------- Serialization Methods ----------------

		public override void OnDeserialization() {
			if (!hasActivated) return;
			CheckDeserialization();
		}

		private void CheckDeserialization() {
			if (videoQueueLocal != videoQueueRemote) UpdateQueue();
		}

		private void Sync() {
			videoQueueRemote = videoQueueLocal;
			if (Networking.LocalPlayer == null) return;
			Networking.SetOwner(Networking.LocalPlayer, gameObject);
			RequestSerialization();
		}

		// --------------- Player Detection Methods ---------------

		private void RebuildModList() {
			if (Networking.LocalPlayer == null) {
				modList = new[] { "Debug User" };
				modIdList = new[] { 0 };
				return;
			}
			int numPlayers = VRCPlayerApi.GetPlayerCount();
			modList = new string[numPlayers];
			modIdList = new int[numPlayers];
			int numMods = 0;
			VRCPlayerApi[] players = new VRCPlayerApi[numPlayers];
			VRCPlayerApi.GetPlayers(players);
			for (int i = 0; i < numPlayers; i++) {
				VRCPlayerApi player = players[i];
				if (player == null || !player.IsValid()) continue;
				if (!mediaPlayer.CheckPrivileged(player)) continue;
				modList[numMods] = player.displayName;
				modIdList[numMods] = player.playerId;
				numMods++;
			}
			string[] tempMods = modList;
			int[] tempIds = modIdList;
			modList = new string[numMods];
			modIdList = new int[numMods];
			Array.Copy(tempMods, modList, numMods);
			Array.Copy(tempIds, modIdList, numMods);
		}

		// ------------------- Callback Methods -------------------

		public override void OnPlayerJoined(VRCPlayerApi player) {
			Log("On Player Joined", this);
			if (player == null || !player.IsValid()) return;
			if (!mediaPlayer.CheckPrivileged(player)) return;
			if (Array.IndexOf(modIdList, player.playerId) >= 0) return;
			int numMods = modList.Length + 1;
			string[] tempMods = modList;
			int[] tempIds = modIdList;
			modList = new string[numMods];
			modIdList = new int[numMods];
			numMods -= 1;
			Array.Copy(tempMods, modList, numMods);
			Array.Copy(tempIds, modIdList, numMods);
			modList[numMods] = player.displayName;
			modIdList[numMods] = player.playerId;
			UpdateModList();
		}

		public override void OnPlayerLeft(VRCPlayerApi player) {
			Log("On Player Left", this);
			if (player == null || !player.IsValid()) return;
			if (Array.IndexOf(modIdList, player.playerId) < 0) return;
			RebuildModList();
			UpdateModList();
		}

		public void _Activate() {
			if (hasActivated) return;
			if (!mediaPlayer.GetIsActive()) return;
			Activate();
		}

		public void _SetStatusText() {
			// Status text has been sent to us
			Log("Set Status Text", this);
			Initialize();
			if (isValid) {
				bool isPlaying = mediaPlayer.GetIsPlaying();
				hideTime = !string.Equals(statusText, "Playing") &&
				           !string.Equals(statusText, "Stabilizing") &&
				           mediaPlayer.GetMediaType() == 0;
				if (!hideTime) UpdateTimeAndStatus();
				else statusField._SetText(statusText);
				UpdateResyncButton();
			} else {
				statusField._SetText(statusText);
			}
		}

		public void _RecheckVideoPlayer() {
			Log("Recheck Video Player", this);
			UpdateAllButtons();
			if (urlField && !string.IsNullOrWhiteSpace(urlField.GetUrl().ToString())) return;
			mediaType = mediaPlayer.GetMediaType();
			UpdateMediaTypeSlider();
			UpdateCurrentOwner();
		}

		public void _PlayerLocked() {
			// Video player has been locked
			Log("Player Locked", this);
			Initialize();
			bool hasPermissions = mediaPlayer.HasPermissions();
			lockUnlockButton._SetMode(hasPermissions ? 1 : 2);
			if (urlPlaceholderField) urlPlaceholderField.text = hasPermissions ? "Enter Video URL (Instance Moderators)..." : "Player locked!";
		}

		public void _PlayerUnlocked() {
			// Video player has been unlocked
			Log("Player Unlocked", this);
			Initialize();
			lockUnlockButton._SetMode(0);
			if (urlPlaceholderField) urlPlaceholderField.text = "Enter Video URL (Anyone)...";
		}

		public void _RelayVideoLoading() {
			// Video is beginning to load
			Log("Relay Video Loading", this);
			Initialize();
			VRCUrl currentURL = mediaPlayer.GetCurrentURL();
			UpdateAllButtons();
		}

		public void _RelayVideoReady() {
			// Video has finished loading
			Log("Relay Video Ready", this);
			Initialize();
			UpdateAllButtons();
			UpdateUrls();
		}

		public void _RelayVideoError() {
			// Video player has thrown an error
			Log("Relay Video Error", this);
			reachedEnd = true;
			Initialize();
		}

		public void _RelayVideoStart() {
			// Video has started playing
			Log("Relay Video Start", this);
			Initialize();
			UpdateAllButtons();
		}

		public void _RelayVideoPlay() {
			// Video has resumed playing
			Log("Relay Video Play", this);
			Initialize();
			UpdateAllButtons();
		}

		public void _RelayVideoPause() {
			// Video has paused
			Log("Relay Video Pause", this);
			Initialize();
			UpdateAllButtons();
		}

		public void _RelayVideoEnd() {
			// Video has finished playing
			Log("Relay Video End", this);
			reachedEnd = true;
			Initialize();
			UpdateAllButtons();
			AdvanceQueue();
		}

		public void _RelayVideoLoop() {
			// Video has looped
			Log("Relay Video Loop", this);
			reachedEnd = false;
			Initialize();
			UpdateAllButtons();
		}

		public void _RelayVideoNext() {
			// Queued video is starting
			Log("Relay Video Next", this);
			reachedEnd = false;
			Initialize();
			UpdateAllButtons();
			AdvanceQueue();
		}

		public void _RelayVideoQueueLoading() {
			// Queued video is beginning to load
			Log("Relay Video Queue Loading", this);
			Initialize();
			//ShowLoadingBar();
			UpdateAllButtons();
		}

		public void _RelayVideoQueueReady() {
			// Queued video has loaded
			Log("Relay Video Queue Ready", this);
			Initialize();
			UpdateAllButtons();
		}

		public void _RelayVideoQueueError() {
			// Queued video player has thrown an error
			Log("Relay Video Queue Error", this);
			Initialize();
		}

		// ----------------- Debug Helper Methods -----------------
		private void Log(string message, UnityEngine.Object context) { if (enableDebug) Debug.Log(debugPrefix + message, context); }
		private void LogVerbose(string message, UnityEngine.Object context) { if (verboseDebug && enableDebug) Debug.Log(debugPrefix + "(+v) " + message, context); }
		private void LogWarning(string message, UnityEngine.Object context) { Debug.LogWarning(debugPrefix + message, context); }
		private void LogError(string message, UnityEngine.Object context) { Debug.LogError(debugPrefix + message, context); }
	}
}
