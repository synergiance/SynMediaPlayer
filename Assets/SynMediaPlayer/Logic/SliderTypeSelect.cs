
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

namespace Synergiance.MediaPlayer.UI {
	public class SliderTypeSelect : UdonSharpBehaviour {
		[SerializeField] private Slider slider;
		[SerializeField] private GameObject[] typeIcons;
		
		[Header("Settings")]
		[SerializeField] private bool enableDebug;
		[SerializeField] private UdonSharpBehaviour callback;
		[SerializeField] private string callbackMethod;
		[SerializeField] private string callbackVar;

		private bool hasCallback;
		private int currentType;
		private bool initialized;
		private bool isSettingControl;

		private string debugPrefix = "[<color=#2090B0>SMP Slider Select</color>] ";

		void Start() {
			Initialize();
		}

		private void Initialize() {
			if (initialized) return;
			hasCallback = callback != null && !string.IsNullOrWhiteSpace(callbackMethod) && !string.IsNullOrWhiteSpace(callbackVar);
			initialized = true;
			int newType = Mathf.FloorToInt(slider.value + 0.5f);
			UpdateHandle(currentType, newType);
			currentType = newType;
		}

		public void _SwitchType() {
			if (isSettingControl) return;
			Initialize();
			int newType = Mathf.FloorToInt(slider.value + 0.5f);
			int oldType = currentType;
			if (!SetTypeInternal(newType)) return;
			UpdateHandle(oldType, newType);
			InvokeCallback();
		}

		public void _SetType(int newType) {
			Initialize();
			int oldType = currentType;
			if (!SetTypeInternal(newType)) return;
			UpdateSlider();
			UpdateHandle(oldType, newType);
		}

		public void _VerifyHandle() {
			for (int i = 0; i < typeIcons.Length; i++) typeIcons[i].SetActive(i == currentType);
		}

		private bool SetTypeInternal(int newType) {
			if (newType >= typeIcons.Length || newType < 0) {
				LogError("Type out of bounds!", this);
				return false;
			}
			Log("Switching type: " + currentType + " to " + newType, this);
			currentType = newType;
			return true;
		}

		private void UpdateHandle(int oldType, int newType) {
			GameObject newIcon = typeIcons[newType];
			GameObject prevIcon = typeIcons[oldType];
			if (newIcon == null) {
				LogWarning("No icon set for type!", this);
				return;
			}
			newIcon.SetActive(true);
			if (prevIcon) prevIcon.SetActive(false);
			SendCustomEventDelayedFrames("_VerifyHandle", 2);
		}

		private void UpdateSlider() {
			isSettingControl = true;
			slider.value = currentType;
			isSettingControl = false;
		}

		private void InvokeCallback() {
			if (!hasCallback) {
				LogWarning("No callback set!", this);
				return;
			}
			callback.SetProgramVariable(callbackVar, currentType);
			callback.SendCustomEvent(callbackMethod);
		}

		// ----------------- Debug Helper Methods -----------------
		private void Log(string message, Object context) { if (enableDebug) Debug.Log(debugPrefix + message, context); }
		private void LogWarning(string message, Object context) { Debug.LogWarning(debugPrefix + message, context); }
		private void LogError(string message, Object context) { Debug.LogError(debugPrefix + message, context); }
	}
}
