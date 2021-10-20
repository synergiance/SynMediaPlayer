
using Synergiance.MediaPlayer.UI;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace Synergiance.MediaPlayer.Helpers {
	public class ToggleObject : UdonSharpBehaviour {
		public GameObject toggleObject;

		public void _Toggle() {
			if (toggleObject == null) return;
			toggleObject.SetActive(!toggleObject.activeSelf);
		}
	}
}
