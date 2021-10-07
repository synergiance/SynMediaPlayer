
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

		private string debugPrefix = "[<color=#2090B0>Stateful Button</color>] ";

		void Start() {
			Initialize();
		}

		private void Initialize() {
			if (initialized) return;
			hasCallback = callback != null && !string.IsNullOrWhiteSpace(callbackMethod) && !string.IsNullOrWhiteSpace(callbackVar);
			initialized = true;
		}

		public void _SwitchType() {
			if (isSettingControl) return;
			Initialize();
			int newType = Mathf.FloorToInt(slider.value + 0.5f);
			if (newType >= typeIcons.Length) {
				LogError("Type out of bounds!", this);
				return;
			}
			Log("Switching type: " + currentType + " to " + newType, this);
			if (hasCallback) {
				callback.SetProgramVariable(callbackVar, newType);
				callback.SendCustomEvent(callbackMethod);
			} else {
				LogWarning("No callback set!", this);
			}
			GameObject newIcon = typeIcons[newType];
			GameObject prevIcon = typeIcons[currentType];
			currentType = newType;
			if (newIcon == null) {
				LogWarning("No icon set for type!", this);
				return;
			}
			newIcon.SetActive(true);
			if (prevIcon) prevIcon.SetActive(false);
		}

		private void SetTypeInternal(int type) {
			isSettingControl = true;
			slider.value = type;
			isSettingControl = false;
		}

		// ----------------- Debug Helper Methods -----------------
		private void Log(string message, Object context) { if (enableDebug) Debug.Log(debugPrefix + message, context); }
		private void LogWarning(string message, Object context) { Debug.LogWarning(debugPrefix + message, context); }
		private void LogError(string message, Object context) { Debug.LogError(debugPrefix + message, context); }
	}
}
