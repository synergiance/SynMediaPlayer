
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

namespace SynPhaxe {
	public class ToggleGroupHelper : UdonSharpBehaviour {
		[SerializeField] private Toggle[]           toggles;
		[SerializeField] private UdonSharpBehaviour callback;
		[SerializeField] private string             callbackMethod = "_Toggle";
		[SerializeField] private string             callbackValue  = "toggleID";
		[SerializeField] private bool               sendCallback   = true;

		private int currentToggle;
		private bool initialized;

		private string debugPrefix = "[<color=#3FBF3F>Toggle Group Helper</color>] ";

		private void Start() {
			Initialize();
		}

		private void Initialize() {
			if (initialized) return;
			bool found = false;
			for (int c = 0; c < toggles.Length; c++) {
				if (!toggles[c].isOn) continue;
				currentToggle = c;
				found = true;
				break;
			}
			if (!found) currentToggle = -1;
			Log("Initial toggle: " + currentToggle);
			initialized = true;
		}

		public int GetCurrentID() {
			Initialize();
			return currentToggle;
		}

		public void _SetToggleID(int id) {
			Log("Setting Toggle: " + id);
			currentToggle = id;
			toggles[id].isOn = true;
		}

		public void _Toggle() {
			bool found = false;
			for (int c = 0; c < toggles.Length; c++) {
				if (!toggles[c].isOn) continue;
				found = true;
				if (c == currentToggle) break;
				currentToggle = c;
				Log("New toggle: " + currentToggle);
				if (!sendCallback) break;
				callback.SetProgramVariable(callbackValue, c);
				callback.SendCustomEvent(callbackMethod);
				break;
			}
			if (found || currentToggle == -1) return;
			currentToggle = -1;
			Log("Toggled Off");
			if (!sendCallback) return;
			callback.SetProgramVariable(callbackValue, -1);
			callback.SendCustomEvent(callbackMethod);
		}

		private void Log(string message) { Debug.Log(debugPrefix + message, this); }
	}
}
