
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace SynPhaxe {
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
		private float seekVal;
		private float lastSeekTime;
		private bool  uiNeedsUpdate;

		private string debugPrefix = "[<color=#0FDF0F>Seek Control</color>] ";

		private void Update() {
			if (!isSeeking || Time.time < lastSeekTime + seekTimeout) return;
			isSeeking = false;
			seekVal = seekBar.value;
			Log("Seeking to: " + seekVal);
			callback.SetProgramVariable(callbackValue, seekVal);
			callback.SendCustomEvent(callbackMethod);
		}

		public void _DragSeek() {
			if (!isEnabled) return;
			if (Mathf.Abs(seekBar.value - seekVal) < 0.00001f) return;
			isSeeking = true;
			lastSeekTime = Time.time;
		}

		public void _SetVal(float val) {
			if (isEnabled && !isSeeking && val >= 0 && val <= 1) seekBar.value = seekVal = val;
			else if (!isSeeking) seekBar.value = -1;
		}

		public void _SetEnabled(bool val) {
			Log(val ? "Enabling" : "Disabling");
			seekBar.interactable = isEnabled = val;
			seekBar.value = isEnabled ? seekVal : -1;
		}

		private void Log(string message) { if (enableDebug) Debug.Log(debugPrefix + message, this); }
	}
}
