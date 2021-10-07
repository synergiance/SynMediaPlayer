
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

namespace Synergiance.MediaPlayer.UI {
	public class StatefulButton : UdonSharpBehaviour {
		[Header("Affected Elements")] // Affected Elements
		[SerializeField] private Button button;
		[SerializeField] private Image icon;
		[SerializeField] private Image iconShadow;
		[SerializeField] private Graphic background;

		[Header("Modes")] // Modes
		[SerializeField] private int defaultMode;
		[SerializeField] private Sprite[] sprites;
		[SerializeField] private Color[] fgColors;
		[SerializeField] private Color[] bgColors;
		
		[Header("Settings")]
		[SerializeField] private bool enableDebug;
		[SerializeField] private UdonSharpBehaviour callback;
		[SerializeField] private string callbackMethod;

		public bool Interactable {
			set => button.interactable = value;
			get => button.interactable;
		}

		private int numModes;
		private int currentMode = -1;
		private bool hasCallback;
		private bool isCustom;
		private bool hasShadow;
		private bool isValid;

		private bool initialized;

		private string debugPrefix = "[<color=#22AABB>Stateful Button</color>] ";

		void Start() {
			Initialize();
		}

		private void Initialize() {
			if (initialized) return;
			hasCallback = callback != null && !string.IsNullOrWhiteSpace(callbackMethod);
			numModes = sprites.Length;
			hasShadow = iconShadow != null;
			isValid = icon != null && background != null;
			if (fgColors.Length < numModes) numModes = fgColors.Length;
			if (bgColors.Length < numModes) numModes = bgColors.Length;
			SetModeInternal(defaultMode);
			if (!hasCallback) LogWarning("Callback is not set, clicks will not be registered!", this);
			if (numModes <= 0) LogWarning("No modes set! No mode setting will work!", this);
			Log("Shadow is " + (hasShadow ? "" : "not ") + "set", this);
			initialized = true;
		}

		public void _Click() {
			Initialize();
			Log("Button " + gameObject.name + " has been clicked", this);
			if (!hasCallback) return;
			callback.SendCustomEvent(callbackMethod);
		}

		public void _SetMode(int mode) {
			Initialize();
			SetModeInternal(mode);
		}

		public void _ChangeColors(Color fgColor, Color bgColor) {
			Initialize();
			if (!isValid) {
				LogError("Changing colors while not properly configured!", this);
				return;
			}
			isCustom = true;
			icon.color = fgColor;
			background.color = bgColor;
		}

		private void SetModeInternal(int mode) {
			if (!isValid) {
				LogError("Changing mode while not properly configured!", this);
				return;
			}
			if (mode >= numModes) {
				LogError("Mode index out of range!", this);
				return;
			}

			Log("Switching mode from " + currentMode + " to " + mode, this);
			currentMode = mode;
			icon.sprite = sprites[mode];
			icon.color = fgColors[mode];
			background.color = bgColors[mode];
			if (hasShadow) iconShadow.sprite = sprites[mode];
			isCustom = false;
		}

		public int GetCurrentMode() { Initialize(); return currentMode; }
		public bool GetIsCustom() { Initialize(); return isCustom; }

		// ----------------- Debug Helper Methods -----------------
		private void Log(string message, Object context) { if (enableDebug) Debug.Log(debugPrefix + message, context); }
		private void LogWarning(string message, Object context) { Debug.LogWarning(debugPrefix + message, context); }
		private void LogError(string message, Object context) { Debug.LogError(debugPrefix + message, context); }
	}
}
