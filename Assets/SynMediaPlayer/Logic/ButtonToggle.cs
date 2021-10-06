
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

namespace Synergiance.MediaPlayer.UI {
	public class ButtonToggle : UdonSharpBehaviour {
		[SerializeField] private Graphic affectedBackground;
		[SerializeField] private Graphic affectedForeground;
		[SerializeField] private GameObject offObject;
		[SerializeField] private GameObject onObject;
		[SerializeField] private UdonSharpBehaviour callback;
		[SerializeField] private string callbackMethod;

		private bool state;

		public void _Click() {
			if (callback != null && !string.IsNullOrWhiteSpace(callbackMethod))
				callback.SendCustomEvent(callbackMethod);
		}

		public void _SetState(bool state) {
			this.state = state;
			if (offObject) offObject.SetActive(!state);
			if (onObject) onObject.SetActive(state);
		}

		public void _SetForegroundColor(Color color) {
			if (affectedForeground) affectedForeground.color = color;
		}

		public void _SetBackgroundColor(Color color) {
			if (affectedBackground) affectedBackground.color = color;
		}

		public void _SetColors(Color fgColor, Color bgColor) {
			_SetForegroundColor(fgColor);
			_SetBackgroundColor(bgColor);
		}

		public bool GetState() {
			return state;
		}
	}
}
