
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
		[SerializeField] private MultiText statusField;
		[SerializeField] private MultiText timeField;
		[SerializeField] private bool combineStatusAndTime;
		[SerializeField] private float updatesPerSecond = 10;
		[SerializeField] private bool enableDebug;

		[HideInInspector] public float volumeVal;
		[HideInInspector] public int mediaTypeVal;
		[HideInInspector] public string statusText;

		private bool initialized;
		private bool isValid;
		private int mediaType;
		private bool isStream;
		private bool displayingStatus;
		private float timeBetweenUpdates;
		private float lastSlowUpdate = 0;

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
			UpdateTimeString();
			SendCustomEventDelayedSeconds("_SlowUpdate", timeBetweenUpdates);
			initialized = true;
		}

		public void _SlowUpdate() {
			if (Time.time < lastSlowUpdate + timeBetweenUpdates * 0.9f) return;
			lastSlowUpdate = Time.time;
			SendCustomEventDelayedSeconds("_SlowUpdate", timeBetweenUpdates);
			UpdateTimeString();
		}

		public void _ClickPlayPauseStop() {
			Initialize();
			if (!isValid) {
				LogInvalid();
				return;
			}
			if (mediaType == 0) { // Media Type 0 is video
				mediaPlayer._PlayPause();
			} else { // Media Type 1-2 is stream
				if (mediaPlayer.GetIsPlaying()) mediaPlayer._Stop();
				else mediaPlayer._Play();
			}
		}

		public void _ClickPlayPause() {
			Initialize();
			if (!isValid) {
				LogInvalid();
				return;
			}
			mediaPlayer._PlayPause();
		}

		public void _ClickPlay() {
			Initialize();
			if (!isValid) {
				LogInvalid();
				return;
			}
			mediaPlayer._Play();
		}

		public void _ClickPause() {
			Initialize();
			if (!isValid) {
				LogInvalid();
				return;
			}
			mediaPlayer._Pause();
		}

		public void _ClickStop() {
			Initialize();
			if (!isValid) {
				LogInvalid();
				return;
			}
			mediaPlayer._Stop();
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
		}

		public void _ClickLoop() {
			Initialize();
			if (!isValid) {
				LogInvalid();
				return;
			}
			mediaPlayer._SetLooping(!mediaPlayer.GetIsLooping());
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
			int loadedType = mediaPlayer._LoadURLAs(urlField.GetUrl(), mediaType);
			if (loadedType == mediaType) return;
			mediaType = loadedType;
			if (selectionSlider) selectionSlider._SetType(mediaType);
		}

		private void LogInvalid() {
			LogError("Not properly initialized!", this);
		}

		private void UpdateTimeString() {
			if (displayingStatus) return;
			string textToDisplay = "00:00:00/00:00:00";
			if (isValid) {
				float duration = mediaPlayer.GetDuration();
				float currentTime = mediaPlayer.GetTime();
				textToDisplay = FormatTime(currentTime) + "/" + FormatTime(duration);
			}
			statusField._SetText(textToDisplay);
		}

		private string FormatTime(float time) {
			string str = ((int)time % 60).ToString("D2");
			time /= 60;
			if (time < 1) {
				str = "0:" + str;
				return str;
			}
			bool hasHours = time >= 60;
			str = ((int)time % 60).ToString(hasHours ? "D2" : "D1") + ":" + str;
			time /= 60;
			if (time < 1) return str;
			bool hasDays = time >= 24;
			str = ((int)time % 24).ToString(hasDays ? "D2" : "D1") + ":" + str;
			time /= 24;
			str = (int)time + ":" + str;
			return str;
		}

		// ------------------- Callback Methods -------------------

		public void _SetStatusText() {
			// Status text has been sent to us
			displayingStatus = !string.Equals("Playing", statusText);
			if (displayingStatus) statusField._SetText(statusText);
			else UpdateTimeString();
		}

		public void _PlayerLocked() {
			// Video player has been unlocked
			bool hasPermissions = mediaPlayer.HasPermissions();
			lockUnlockButton._SetMode(hasPermissions ? 1 : 2);
			if (urlPlaceholderField) urlPlaceholderField.text = hasPermissions ? "Enter Video URL (Instance Moderators)..." : "Player locked!";
		}

		public void _PlayerUnlocked() {
			// Video player has been unlocked
			lockUnlockButton._SetMode(0);
			if (urlPlaceholderField) urlPlaceholderField.text = "Enter Video URL (Anyone)...";
		}

		public void _RelayVideoLoading() {
			// Video is beginning to load
			VRCUrl currentURL = mediaPlayer.GetCurrentURL();
			//lcdDisplay.SetURL(currentURL == null ? "" : currentURL.ToString());
			//HideErrorControls();
			//ShowLoadingBar();
		}

		public void _RelayVideoReady() {
			// Video has finished loading
			//VideoReady();
		}

		public void _RelayVideoError() {
			// Video player has thrown an error
			//DecodeVideoError(relayVideoError);
		}

		public void _RelayVideoStart() {
			// Video has started playing
			//VideoStart();
		}

		public void _RelayVideoPlay() {
			// Video has resumed playing
			//VideoPlay();
		}

		public void _RelayVideoPause() {
			// Video has paused
			//VideoPause();
		}

		public void _RelayVideoEnd() {
			// Video has finished playing
			//VideoEnd();
		}

		public void _RelayVideoLoop() {
			// Video has looped
			//VideoLoop();
		}

		public void _RelayVideoNext() {
			// Queued video is starting
		}

		public void _RelayVideoQueueLoading() {
			// Queued video is beginning to load
			//ShowLoadingBar();
		}

		public void _RelayVideoQueueReady() {
			// Queued video has loaded
			//HideLoadingBar();
		}

		public void _RelayVideoQueueError() {
			// Queued video player has thrown an error
		}

		// ----------------- Debug Helper Methods -----------------
		private void Log(string message, UnityEngine.Object context) { if (enableDebug) Debug.Log(debugPrefix + message, context); }
		private void LogWarning(string message, UnityEngine.Object context) { Debug.LogWarning(debugPrefix + message, context); }
		private void LogError(string message, UnityEngine.Object context) { Debug.LogError(debugPrefix + message, context); }
	}
}
