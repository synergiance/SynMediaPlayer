
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

namespace Synergiance.MediaPlayer.UI {
	public class ControlPanel : UdonSharpBehaviour {
		[SerializeField] private MediaPlayer mediaPlayer;
		[SerializeField] private VolumeControl volumeControl;
		[SerializeField] private SliderTypeSelect selectionSlider;
		[SerializeField] private VRCUrlInputField urlField;
		[SerializeField] private StatefulButton playPauseStopButton;
		[SerializeField] private StatefulButton playPauseButton;
		[SerializeField] private StatefulButton stopButton;
		[SerializeField] private StatefulButton lockUnlockButton;
		[SerializeField] private StatefulButton powerButton;
		[SerializeField] private StatefulButton loopButton;
		[SerializeField] private bool enableDebug;

		[HideInInspector] public float volumeVal;
		[HideInInspector] public int mediaTypeVal;

		private bool initialized;
		private bool isValid;
		private int mediaType;
		private bool isStream;

		private string debugPrefix = "[<color=#20C0A0>SMP Control Panel</color>] ";

		void Start() {
			Initialize();
		}

		private void Initialize() {
			if (initialized) return;
			Log("Initializing", this);
			if (!mediaPlayer) {
				isValid = false;
				LogWarning("Media Player not set!", this);
			}
			initialized = true;
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
			mediaPlayer._SetActive(!mediaPlayer.GetIsActive());
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
			//if (selectionSlider) selectionSlider.
		}

		private void LogInvalid() {
			LogError("Not properly initialized!", this);
		}

		// ----------------- Debug Helper Methods -----------------
		private void Log(string message, UnityEngine.Object context) { if (enableDebug) Debug.Log(debugPrefix + message, context); }
		private void LogWarning(string message, UnityEngine.Object context) { Debug.LogWarning(debugPrefix + message, context); }
		private void LogError(string message, UnityEngine.Object context) { Debug.LogError(debugPrefix + message, context); }
	}
}
