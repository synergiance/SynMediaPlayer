
using System;
using Synergiance.MediaPlayer.UI;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace Synergiance.MediaPlayer.Helpers {
	public class ToggleObject : UdonSharpBehaviour {
		public GameObject toggleObject;
		public Canvas toggleCanvas;
		public MediaPlayer mediaPlayer;
		[SerializeField] private bool state;

		private bool stateInternal;

		private void Start() {
			SetState(state);
		}

		public void _Toggle() {
			if (mediaPlayer && mediaPlayer.IsLocked && !mediaPlayer.HasPermissions) {
				SetState(false);
				return;
			}
			SetState(!stateInternal);
		}

		private void SetState(bool newState) {
			stateInternal = newState;
			if (toggleObject) toggleObject.SetActive(stateInternal);
			if (toggleCanvas) toggleCanvas.enabled = stateInternal;
		}
	}
}
