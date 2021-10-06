
using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

namespace Synergiance.MediaPlayer.UI {
	public class VolumeControl : UdonSharpBehaviour {
		[SerializeField] private GameObject muteIcon;
		[SerializeField] private GameObject noVolumeIcon;
		[SerializeField] private GameObject lowVolumeIcon;
		[SerializeField] private GameObject highVolumeIcon;
		[SerializeField] private Slider volumeSlider;

		[SerializeField] private UdonSharpBehaviour callback;
		[SerializeField] private string callbackMethod;
		[SerializeField] private string callbackVar;

		private bool isMuted;
		private float nonMutedVolume;
		private bool hasCallback;

		private void Start() {
			hasCallback = callback != null;
			hasCallback &= !string.IsNullOrWhiteSpace(callbackMethod);
			hasCallback &= !string.IsNullOrWhiteSpace(callbackVar);
		}

		public void _DragVolume() {
			_SetVolume(volumeSlider.value);
			UpdateCallback();
		}

		public void _SetVolume(float volume) {
			nonMutedVolume = volume;
			UpdateGraphics();
		}

		public void _ToggleMute() {
			_SetMute(!isMuted);
		}

		public void _SetMute(bool mute) {
			isMuted = mute;
			UpdateCallback();
			UpdateGraphics();
		}

		private void UpdateGraphics() {
			int state;
			if (isMuted) state = 0;
			else if (nonMutedVolume == 0) state = 1;
			else if (nonMutedVolume <= 0.5) state = 2;
			else state = 3;
			muteIcon.SetActive(state == 0);
			noVolumeIcon.SetActive(state == 1);
			lowVolumeIcon.SetActive(state == 2);
			highVolumeIcon.SetActive(state == 3);
			volumeSlider.interactable = !isMuted;
		}

		private void UpdateCallback() {
			if (!hasCallback) return;
			callback.SetProgramVariable(callbackVar, isMuted ? 0.0f : nonMutedVolume);
			callback.SendCustomEvent(callbackMethod);
		}
	}
}
