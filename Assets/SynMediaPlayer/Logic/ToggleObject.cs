
using Synergiance.MediaPlayer.UI;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace Synergiance.MediaPlayer.Helpers {
	public class ToggleObject : UdonSharpBehaviour {
		public GameObject toggleObject;
		public Canvas toggleCanvas;

		public void _Toggle() {
			if (toggleObject) toggleObject.SetActive(!toggleObject.activeSelf);
			if (toggleCanvas) toggleCanvas.enabled = !toggleCanvas.enabled;
		}
	}
}
