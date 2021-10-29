
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
		[SerializeField] private bool loadGapless;

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

		private string[] modList;
		private int[] modIdList;

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
			UpdateTimeAndStatus();
			SendCustomEventDelayedSeconds("_SlowUpdate", timeBetweenUpdates);
			RebuildModList();
			UpdateModList();
			initialized = true;
		}

		public void _SlowUpdate() {
			if (Time.time < lastSlowUpdate + timeBetweenUpdates * 0.9f) return;
			lastSlowUpdate = Time.time;
			SendCustomEventDelayedSeconds("_SlowUpdate", timeBetweenUpdates);
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
			if (!urlField) {
				LogError("Url Field not set!", this);
				return;
			}
			if (string.IsNullOrWhiteSpace(urlField.GetUrl().ToString())) {
				UpdateMediaTypeSlider();
				return;
			}
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
			if (player == null || !player.IsValid()) return;
			if (Array.IndexOf(modIdList, player.playerId) < 0) return;
			RebuildModList();
			UpdateModList();
		}

		public void _SetStatusText() {
			// Status text has been sent to us
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
			UpdateAllButtons();
			if (urlField && !string.IsNullOrWhiteSpace(urlField.GetUrl().ToString())) return;
			mediaType = mediaPlayer.GetMediaType();
			UpdateMediaTypeSlider();
			UpdateCurrentOwner();
		}

		public void _PlayerLocked() {
			// Video player has been locked
			Initialize();
			bool hasPermissions = mediaPlayer.HasPermissions();
			lockUnlockButton._SetMode(hasPermissions ? 1 : 2);
			if (urlPlaceholderField) urlPlaceholderField.text = hasPermissions ? "Enter Video URL (Instance Moderators)..." : "Player locked!";
		}

		public void _PlayerUnlocked() {
			// Video player has been unlocked
			Initialize();
			lockUnlockButton._SetMode(0);
			if (urlPlaceholderField) urlPlaceholderField.text = "Enter Video URL (Anyone)...";
		}

		public void _RelayVideoLoading() {
			// Video is beginning to load
			Initialize();
			VRCUrl currentURL = mediaPlayer.GetCurrentURL();
			UpdateAllButtons();
		}

		public void _RelayVideoReady() {
			// Video has finished loading
			Initialize();
			UpdateAllButtons();
			UpdateUrls();
		}

		public void _RelayVideoError() {
			// Video player has thrown an error
			Initialize();
		}

		public void _RelayVideoStart() {
			// Video has started playing
			Initialize();
			UpdateAllButtons();
		}

		public void _RelayVideoPlay() {
			// Video has resumed playing
			Initialize();
			UpdateAllButtons();
		}

		public void _RelayVideoPause() {
			// Video has paused
			Initialize();
			UpdateAllButtons();
		}

		public void _RelayVideoEnd() {
			// Video has finished playing
			Initialize();
			UpdateAllButtons();
		}

		public void _RelayVideoLoop() {
			// Video has looped
			Initialize();
			UpdateAllButtons();
		}

		public void _RelayVideoNext() {
			// Queued video is starting
			Initialize();
			UpdateAllButtons();
		}

		public void _RelayVideoQueueLoading() {
			// Queued video is beginning to load
			Initialize();
			//ShowLoadingBar();
			UpdateAllButtons();
		}

		public void _RelayVideoQueueReady() {
			// Queued video has loaded
			Initialize();
			UpdateAllButtons();
		}

		public void _RelayVideoQueueError() {
			// Queued video player has thrown an error
			Initialize();
		}

		// ----------------- Debug Helper Methods -----------------
		private void Log(string message, UnityEngine.Object context) { if (enableDebug) Debug.Log(debugPrefix + message, context); }
		private void LogWarning(string message, UnityEngine.Object context) { Debug.LogWarning(debugPrefix + message, context); }
		private void LogError(string message, UnityEngine.Object context) { Debug.LogError(debugPrefix + message, context); }
	}
}
