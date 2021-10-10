
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace Synergiance.MediaPlayer.UI {
	public class SeekControl : UdonSharpBehaviour {
		// Settings
		[SerializeField]  private UdonSharpBehaviour callback;
		[SerializeField]  private string             callbackMethod = "_Seek";
		[SerializeField]  private string             callbackValue  = "seekVal";
		[SerializeField]  private Slider             seekBar;
		[SerializeField]  private float              seekTimeout    = 0.1f;
		[SerializeField]  private bool               enableDebug;

		// Private values
		private bool  isSeeking;
		private bool  isEnabled = true;
		private bool  isLocked;
		private float seekVal;
		private float lastSeekTime;
		private bool  uiNeedsUpdate;
		private bool  initialized;
		private bool  isValid;

		private string debugPrefix = "[<color=#0FDF0F>Seek Control</color>] ";

		private void Start() {
			Initialize();
		}

		private void Initialize() {
			if (initialized) return;
			if (!seekBar) seekBar = transform.GetComponentInChildren<Slider>();
			isValid = seekBar != null;
			if (!isValid) LogError("Seekbar slider not set!");
			else Log("Seekbar is set");
			seekVal = isValid ? seekBar.value : 0;
			initialized = true;
		}

		private void Update() {
			if (!isSeeking || Time.time < lastSeekTime + seekTimeout) return;
			if (isSeeking) Log("No Longer Seeking");
			isSeeking = false;
			seekVal = isValid ? seekBar.value : 0;
			Log("Seeking to: " + seekVal);
			callback.SetProgramVariable(callbackValue, seekVal);
			callback.SendCustomEvent(callbackMethod);
		}

		public void _DragSeek() {
			Initialize();
			if (!isEnabled || !isValid) return;
			if (Mathf.Abs(seekBar.value - seekVal) < 0.00001f) return;
			if (!isSeeking) Log("Now Seeking");
			isSeeking = true;
			lastSeekTime = Time.time;
		}

		public void _SetVal(float val) {
			Initialize();
			if (!isValid) return;
			if (isEnabled && !isSeeking && val >= 0 && val <= 1) seekBar.value = seekVal = val;
			else if (!isSeeking) seekBar.value = -1;
		}

		public void _SetEnabled(bool val) {
			Initialize();
			Log(val ? "Enabling" : "Disabling");
			isEnabled = val;
			if (!isValid) return;
			SetInteractable();
			seekBar.value = isEnabled ? seekVal : -1;
		}

		public void _SetLocked(bool val) {
			Initialize();
			if (val == isLocked) return;
			SetInteractable();
		}

		private void SetInteractable() {
			if (!isValid) return;
			seekBar.interactable = isEnabled && !isLocked;
		}

		private void Log(string message) { if (enableDebug) Debug.Log(debugPrefix + message, this); }
		private void LogError(string message) { Debug.LogError(debugPrefix + message, this); }
	}
}
