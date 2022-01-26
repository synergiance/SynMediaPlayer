
using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

namespace Synergiance.MediaPlayer.UI {
	public class ControlPanel : UdonSharpBehaviour {
		[SerializeField] private ControlPanelInterface mediaPlayerInterface;
		[SerializeField] private MediaPlayer mediaPlayer;
		[SerializeField] private VolumeControl volumeControl;
		[SerializeField] private SeekControl seekControl;
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
		[SerializeField] private Text playerVersionField;
		[SerializeField] private bool combineStatusAndTime;
		[SerializeField] private float updatesPerSecond = 5;
		[SerializeField] private float reloadAvailableFor = 5;
		[SerializeField] private bool enableDebug;
		[SerializeField] private bool verboseDebug;
		[Range(5,50)] [SerializeField] private int maxVideosInQueue = 20; // Deprecated

		[HideInInspector] public float volumeVal;
		[HideInInspector] public int mediaTypeVal;
		[HideInInspector] public string statusText;
		[HideInInspector] public float seekVal; // Normalized seek bar value.  Used when seeking.

		private bool initialized;
		private bool isValid;
		private int mediaType;
		private bool isStream;
		private float lastSlowUpdate;
		private float lastResync;
		private bool hideTime;
		private bool queueCheckURL;
		private bool reachedEnd;
		private bool hasActivated;
		private int panelId = -1;

		private float duration;
		private float time;
		private string status;

		private string[] modList;
		private int[] modIdList;

		private int hardQueueCap = 100;

		private string debugPrefix = "[<color=#20B0A7>SMP Control Panel</color>] ";
		private string smpVersionString = "SynMediaPlayer ";

		void Start() {
			Initialize();
		}

		private void Initialize() {
			if (initialized) return;
			Log("Initializing", this);
			isValid = true;
			if (!mediaPlayerInterface) {
				isValid = false;
				LogWarning("Media Player Interface not set!", this);
			}
			if (volumeControl && isValid) volumeControl._SetVolume(mediaPlayer.Volume);
			// TODO: Trim unnecessary code
			// ReSharper disable once ConditionIsAlwaysTrueOrFalse
			if (maxVideosInQueue < 1) maxVideosInQueue = 1;
			if (maxVideosInQueue > hardQueueCap) maxVideosInQueue = hardQueueCap;
			initialized = true;
			UpdateMethods();
			if (isValid && playerVersionField) playerVersionField.text = smpVersionString + mediaPlayer.BuildString;
		}

		private void Register() {
			if (!isValid) return;
			if (panelId >= 0) return;
			panelId = mediaPlayerInterface.RegisterPanel(this);
		}

		private void UpdateMethods() {
			UpdateTimeAndStatus();
			UpdateAllButtons();
			UpdateCurrentOwner();
		}

		/// <summary>
		/// Click event for a combination Play/Pause/Stop button.
		/// </summary>
		public void _ClickPlayPauseStop() {
			Initialize();
			if (!isValid) {
				LogInvalid();
				return;
			}
			mediaPlayerInterface._PlayPauseStop();
		}

		/// <summary>
		/// Click event for a combination Play/Pause toggle button.
		/// </summary>
		public void _ClickPlayPause() {
			Initialize();
			if (!isValid) {
				LogInvalid();
				return;
			}
			mediaPlayerInterface._PlayPause();
		}

		/// <summary>
		/// Click event for a Play button.
		/// </summary>
		public void _ClickPlay() {
			Initialize();
			if (!isValid) {
				LogInvalid();
				return;
			}
			mediaPlayerInterface._Play();
		}

		/// <summary>
		/// Click event for a Pause button.
		/// </summary>
		public void _ClickPause() {
			Initialize();
			if (!isValid) {
				LogInvalid();
				return;
			}
			mediaPlayerInterface._Pause();
		}

		/// <summary>
		/// Click event for a Stop button.
		/// </summary>
		public void _ClickStop() {
			Initialize();
			if (!isValid) {
				LogInvalid();
				return;
			}
			mediaPlayerInterface._Stop();
		}

		/// <summary>
		/// Click event for a Power toggle button.
		/// </summary>
		public void _ClickPower() {
			Initialize();
			if (!isValid) {
				LogInvalid();
				return;
			}
			mediaPlayerInterface.Active = !mediaPlayerInterface.Active;
			UpdatePowerButton();
		}

		/// <summary>
		/// Click event for a Loop toggle button.
		/// </summary>
		public void _ClickLoop() {
			Initialize();
			if (!isValid) {
				LogInvalid();
				return;
			}
			mediaPlayer._SetLooping(!mediaPlayer.Loop);
			UpdateLoopButton();
		}

		/// <summary>
		/// Click event for a Lock toggle button.
		/// </summary>
		public void _ClickLock() {
			Initialize();
			if (!isValid) {
				LogInvalid();
				return;
			}
			mediaPlayerInterface.IsLocked = !mediaPlayerInterface.IsLocked;
			// Update button?
		}

		/// <summary>
		/// On Change event for a Volume slider.
		/// </summary>
		public void _SetVolume() {
			Initialize();
			if (!isValid) {
				LogInvalid();
				return;
			}
			mediaPlayerInterface._SetVolume(volumeControl.Volume, volumeControl.IsMuted);
		}

		/// <summary>
		/// Click event for a Resync button.
		/// </summary>
		public void _ClickResync() {
			Initialize();
			if (!isValid) return;
			if (mediaPlayer.Ready && Time.time > lastResync + reloadAvailableFor) {
				mediaPlayer.Resync();
				lastResync = Time.time;
			} else {
				mediaPlayer._Retry();
				lastResync = Time.time - reloadAvailableFor;
			}
		}

		/// <summary>
		/// Click event for a Diagnostics button.
		/// </summary>
		public void _ClickDiagnostics() {
			if (!isValid) return;
			mediaPlayerInterface._RunOrCancelDiagnostics();
		}

		/// <summary>
		/// On Change event for a media selection switch.
		/// </summary>
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

		/// <summary>
		/// Event to load media from a <c>VRCUrlInputField</c>.
		/// </summary>
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
			mediaPlayerInterface._LoadUrl(urlField.GetUrl());
			urlField.SetUrl(VRCUrl.Empty);
		}

		/// <summary>
		/// Optional on change event for a <c>VRCUrlInputField</c>.
		/// </summary>
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

		// TODO: Replace this method
		private void UpdateAllButtons() {
			UpdatePlayPauseStopButtons();
			UpdateResyncButton();
			UpdatePowerButton();
			UpdateLoopButton();
		}

		private void UpdatePlayPauseStopButtons() {
			bool isPlaying = mediaPlayerInterface.IsPlaying;
			if (playPauseButton) playPauseButton._SetMode(isPlaying ? 1 : 0);
			if (!playPauseStopButton) return;
			bool stream = mediaPlayerInterface.MediaType != 0;
			playPauseStopButton._SetMode(isPlaying ? stream ? 2 : 1 : 0);
		}

		private void UpdateResyncButton() {
			if (!isValid || !refreshButton) return;
			int loaded = mediaPlayer.Ready ? 1 : 0;
			int syncing = mediaPlayer.IsSyncing ? 2 : 0;
			if (loaded == 1 && Time.time <= lastResync + reloadAvailableFor) loaded = 0;
			refreshButton._SetMode(loaded + syncing);
		}

		private void UpdatePowerButton() {
			if (!isValid || !powerButton) return;
			powerButton._SetMode(mediaPlayerInterface.Active ? 0 : 1);
		}

		private void UpdateLoopButton() {
			if (!isValid || !loopButton) return;
			loopButton._SetMode(mediaPlayerInterface.Loop ? 1 : 0);
		}

		private void UpdateMediaTypeSlider() {
			if (selectionSlider) selectionSlider._SetType(mediaType);
		}

		private void UpdateCurrentOwner() {
			if (!isValid || !currentOwnerField) return;
			VRCPlayerApi owner = mediaPlayerInterface.CurrentOwner;
			string newOwnerName = owner != null ? owner.displayName + (owner.isLocal ? "*" : "") : "Nobody";
			string oldOwnerName = currentOwnerField.text;
			if (string.Equals(newOwnerName, oldOwnerName)) return;
			currentOwnerField.text = newOwnerName;
		}

		private void UpdateTimeAndStatus() {
			string timeToDisplay = combineStatusAndTime && hideTime ? "" : GenerateTimeString();
			string statusToDisplay = status;
			if (combineStatusAndTime) {
				if (hideTime) timeToDisplay = statusToDisplay;
				statusToDisplay = timeToDisplay;
			}
			if (timeField) timeField._SetText(timeToDisplay);
			if (statusField) statusField._SetText(statusToDisplay);
		}

		private string GenerateTimeString() {
			if (!isValid) return "00:00:00/00:00:00";
			string timeString = FormatTime(time);
			if (Single.IsNaN(duration) || Single.IsInfinity(duration)) timeString = "Live";
			else if (duration > 0.01f) timeString += "/" + FormatTime(duration);
			return timeString;
		}

		private void UpdateSeek() {
			if (!seekControl) return;
			float seekPos = mediaPlayerInterface.SeekPos;
			seekControl._SetVal(seekPos);
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

		private void UpdateUrls() {
			if (!isValid) return;
			VRCUrl url = mediaPlayer.CurrentUrl;
			if (url == null || string.IsNullOrWhiteSpace(url.ToString())) return;
			if (!currentUrlField) return;
			if (prevUrlField) prevUrlField.text = currentUrlField.text;
			currentUrlField.text = url.ToString();
		}

		private void UpdateQueueUI() {
			// TODO: Implement
			LogVerbose("Update Queue UI (Doesn't actually do anything yet)", this);
		}
		
		// -------------------- Refresh Methods -------------------

		/// <summary>
		/// Fetches the state of the playback buttons from the media player and updates the UI accordingly
		/// </summary>
		private void RefreshPlayback() {
			LogVerbose("Refresh Playback", this);
			UpdatePlayPauseStopButtons();
		}

		/// <summary>
		/// Fetches whether the media player is looping and updates the UI accordingly
		/// </summary>
		private void RefreshLoop() {
			LogVerbose("Refresh Loop", this);
			bool isLooping = mediaPlayerInterface.Loop;
		}

		/// <summary>
		/// Fetches whether the media player is active and updates the UI accordingly
		/// </summary>
		private void RefreshActive() {
			LogVerbose("Refresh Active", this);
			bool isActive = mediaPlayerInterface.Active;
		}

		/// <summary>
		/// Fetches the status from the media player and updates the UI accordingly
		/// </summary>
		private void RefreshStatus() {
			LogVerbose("Refresh Status", this);
			if (isValid) status = mediaPlayerInterface.Status;
			hideTime = !string.Equals(status, "Playing") &&
			           !string.Equals(status, "Stabilizing") &&
			           mediaPlayerInterface.MediaType == 0;
			if (!mediaPlayerInterface.Ready) hideTime = true;
			UpdateTimeAndStatus();
			UpdateResyncButton();
		}

		/// <summary>
		/// Fetches the current time from the media player and updates the UI accordingly
		/// </summary>
		private void RefreshTime() {
			LogVerbose("Refresh Time", this);
			if (isValid) time = mediaPlayerInterface.CurrentTime;
			UpdateTimeAndStatus();
			UpdateSeek();
		}

		private void RefreshSeek() {
			LogVerbose("Refresh Seek", this);
			UpdateSeek();
		}

		/// <summary>
		/// Fetches the duration of the current video from the media player and updates the UI accordingly
		/// </summary>
		private void RefreshDuration() {
			LogVerbose("Refresh Duration", this);
			if (isValid) duration = mediaPlayerInterface.Duration;
			UpdateTimeAndStatus();
			UpdateSeek();
		}

		/// <summary>
		/// Fetches the volume from the media player and updates the UI accordingly
		/// </summary>
		private void RefreshVolume() {
			LogVerbose("Refresh Volume", this);
			volumeControl._SetVolume(mediaPlayerInterface.Volume);
			volumeControl._SetMute(mediaPlayerInterface.Mute);
		}

		/// <summary>
		/// Fetches the current media type from the media player and updates the UI accordingly
		/// </summary>
		private void RefreshMediaType() {
			LogVerbose("Refresh Media Type", this);
			int newMediaType = mediaPlayerInterface.MediaType;
			if (mediaType == newMediaType) return;
			mediaType = newMediaType;
			UpdateMediaTypeSlider(); // TODO: Decide whether to replace this method
		}

		/// <summary>
		/// Fetches whether you are allowed to make changes to the playing media and updates the UI accordingly
		/// </summary>
		private void RefreshPermissions() {
			LogVerbose("Refresh Permissions", this);
			// TODO: Get permissions from mediaplayerinterface
			// Seek bar too
		}

		/// <summary>
		/// Fetches the current owner of the media player and updates the UI accordingly
		/// </summary>
		private void RefreshOwner() {
			LogVerbose("Refresh Owner", this);
			VRCPlayerApi currentOwner = mediaPlayerInterface.CurrentOwner;
			currentOwnerField.text = currentOwner.displayName + (currentOwner.isLocal ? "*" : "");
		}

		/// <summary>
		/// Fetches the list of moderators from the media player and updates the UI accordingly
		/// </summary>
		private void RefreshModList() {
			LogVerbose("Refresh Mod List", this);
			string[] modList = mediaPlayerInterface.ModList;
			int[] modIdList = mediaPlayerInterface.ModIdList;
			int localPlayerId = Networking.LocalPlayer.playerId;
			// TODO: Finish method
			for (int i = 0; i < modList.Length; i++) {
				LogVerbose("Found user: " + modList[i] + (modIdList[i] == localPlayerId ? " (me)" : ""), this);
				// Build and format string of mods
			}
			// Display string in UI
		}

		/// <summary>
		/// Fetches current and previous video links from the media player and update the UI accordingly
		/// </summary>
		private void RefreshVideoLinks() {
			LogVerbose("Refresh Video Links", this);
			// TODO: Get video links from mediaplayerinterface
		}

		// ------------------- Callback Methods -------------------

		[Obsolete("_SetStatusText is deprecated, use _Refresh instead!")]
		public void _SetStatusText() {
			// Status text has been sent to us
			Log("Set Status Text", this);
			Initialize();
			if (isValid) {
				bool isPlaying = mediaPlayerInterface.IsPlaying;
				hideTime = !string.Equals(statusText, "Playing") &&
				           !string.Equals(statusText, "Stabilizing") &&
				           mediaPlayerInterface.MediaType == 0;
				if (!mediaPlayerInterface.Ready) hideTime = true;
				if (!hideTime) UpdateTimeAndStatus();
				else statusField._SetText(statusText);
				UpdateResyncButton();
			} else {
				statusField._SetText(statusText);
			}
		}

		/// <summary>
		/// Refreshes specified parts of the UI
		/// </summary>
		/// <param name="elements">Space separated list of all items meant to be refreshed</param>
		public void _Refresh(string elements) {
			foreach (string element in elements.Split(' ')) {
				switch (element) {
					case "Playback":
						RefreshPlayback();
						break;
					case "Loop":
						RefreshLoop();
						break;
					case "Active":
						RefreshActive();
						break;
					case "Status":
						RefreshStatus();
						break;
					case "Time":
						RefreshTime();
						break;
					case "Duration":
						RefreshDuration();
						break;
					case "Volume":
						RefreshVolume();
						break;
					case "MediaType":
						RefreshMediaType();
						break;
					case "Permissions":
						RefreshPermissions();
						break;
					case "Owner":
						RefreshOwner();
						break;
					case "ModList":
						RefreshModList();
						break;
					case "VideoLinks":
						RefreshVideoLinks();
						break;
					default:
						LogWarning("Invalid Element: " + element, this);
						break;
				}
			}
		}

		public void _RefreshAll() {
			UpdateAllButtons();
			if (urlField && !string.IsNullOrWhiteSpace(urlField.GetUrl().ToString())) return;
			mediaType = mediaPlayerInterface.MediaType;
			UpdateMediaTypeSlider();
			UpdateCurrentOwner();
		}

		public void _RecheckVideoPlayer() {
			LogWarning("(<color=#20C0A0>Obsolete Warning!</color>) Recheck Video Player", this);
			_RefreshAll();
		}

		// ----------------- Debug Helper Methods -----------------
		private void Log(string message, UnityEngine.Object context) { if (enableDebug) Debug.Log(debugPrefix + message, context); }
		private void LogVerbose(string message, UnityEngine.Object context) { if (verboseDebug && enableDebug) Debug.Log(debugPrefix + "(+v) " + message, context); }
		private void LogWarning(string message, UnityEngine.Object context) { Debug.LogWarning(debugPrefix + message, context); }
		private void LogError(string message, UnityEngine.Object context) { Debug.LogError(debugPrefix + message, context); }
		//private string GetPanelId() { return  }
	}
}
