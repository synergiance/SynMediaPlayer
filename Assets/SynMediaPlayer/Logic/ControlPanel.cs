﻿
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
		[SerializeField] private bool combineStatusAndTime;
		[SerializeField] private float updatesPerSecond = 10;
		[SerializeField] private float reloadAvailableFor = 5;
		[SerializeField] private bool enableDebug;

		[HideInInspector] public float volumeVal;
		[HideInInspector] public int mediaTypeVal;
		[HideInInspector] public string statusText;

		private bool initialized;
		private bool isValid;
		private int mediaType;
		private bool isStream;
		//private bool hideStatus;
		private float timeBetweenUpdates;
		private float lastSlowUpdate;
		private float lastResync;

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
			UpdatePlayPauseStopButtons();
			UpdateResyncButton();
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

		private void UpdateTimeString() {
			string textToDisplay = "00:00:00/00:00:00";
			if (isValid) {
				if (!mediaPlayer.GetIsPlaying()) return;
				float duration = mediaPlayer.GetDuration();
				float currentTime = mediaPlayer.GetTime();
				textToDisplay = FormatTime(currentTime);
				if (duration > 0.01f) textToDisplay += "/" + FormatTime(duration);
				//if (!hideStatus) textToDisplay += " (" + statusText + ")";
			}
			statusField._SetText(textToDisplay);
		}

		private string FormatTime(float time) {
			float wTime = Mathf.Abs(time);
			bool neg = time < 0;
			string str = ((int)wTime % 60).ToString("D2");
			wTime /= 60;
			if (wTime < 1) {
				str = "0:" + str;
				return neg ? "-" + str : str;
			}
			bool hasHours = wTime >= 60;
			str = ((int)wTime % 60).ToString(hasHours ? "D2" : "D1") + ":" + str;
			wTime /= 60;
			if (wTime < 1) return neg ? "-" + str : str;
			bool hasDays = wTime >= 24;
			str = ((int)wTime % 24).ToString(hasDays ? "D2" : "D1") + ":" + str;
			wTime /= 24;
			str = (int)wTime + ":" + str;
			return neg ? "-" + str : str;
		}

		// ------------------- Callback Methods -------------------

		public void _SetStatusText() {
			// Status text has been sent to us
			Initialize();
			if (isValid) {
				bool isPlaying = mediaPlayer.GetIsPlaying();
				//bool isSyncing = mediaPlayer.GetIsSyncing();
				//hideStatus = isPlaying && !isSyncing;
				if (isPlaying) UpdateTimeString();
				else statusField._SetText(statusText);
				UpdateResyncButton();
			} else {
				statusField._SetText(statusText);
			}
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
			//lcdDisplay.SetURL(currentURL == null ? "" : currentURL.ToString());
			//HideErrorControls();
			//ShowLoadingBar();
			UpdatePlayPauseStopButtons();
			UpdateResyncButton();
		}

		public void _RelayVideoReady() {
			// Video has finished loading
			Initialize();
			UpdatePlayPauseStopButtons();
			UpdateResyncButton();
		}

		public void _RelayVideoError() {
			// Video player has thrown an error
			Initialize();
			//DecodeVideoError(relayVideoError);
		}

		public void _RelayVideoStart() {
			// Video has started playing
			Initialize();
			UpdatePlayPauseStopButtons();
			UpdateResyncButton();
		}

		public void _RelayVideoPlay() {
			// Video has resumed playing
			Initialize();
			UpdatePlayPauseStopButtons();
			UpdateResyncButton();
		}

		public void _RelayVideoPause() {
			// Video has paused
			Initialize();
			UpdatePlayPauseStopButtons();
			UpdateResyncButton();
		}

		public void _RelayVideoEnd() {
			// Video has finished playing
			Initialize();
			UpdatePlayPauseStopButtons();
			UpdateResyncButton();
		}

		public void _RelayVideoLoop() {
			// Video has looped
			Initialize();
			UpdatePlayPauseStopButtons();
			UpdateResyncButton();
		}

		public void _RelayVideoNext() {
			// Queued video is starting
			Initialize();
			UpdatePlayPauseStopButtons();
			UpdateResyncButton();
		}

		public void _RelayVideoQueueLoading() {
			// Queued video is beginning to load
			Initialize();
			//ShowLoadingBar();
			UpdatePlayPauseStopButtons();
			UpdateResyncButton();
		}

		public void _RelayVideoQueueReady() {
			// Queued video has loaded
			Initialize();
			//HideLoadingBar();
			UpdatePlayPauseStopButtons();
			UpdateResyncButton();
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
