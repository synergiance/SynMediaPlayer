
using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

namespace Synergiance.MediaPlayer.UI {
	public class VolumeControl : UdonSharpBehaviour {
		[SerializeField] private Slider volumeSlider;
		[SerializeField] private StatefulButton muteButton;

		[SerializeField] private UdonSharpBehaviour callback;
		[SerializeField] private string callbackMethod;
		[SerializeField] private string callbackVar;

		private bool isMuted;
		private float nonMutedVolume;
		private bool hasCallback;
		private bool isSettingControl;

		private bool initialized;

		private void Start() {
			Initialize();
		}

		private void Initialize() {
			if (initialized) return;
			hasCallback = callback != null;
			hasCallback &= !string.IsNullOrWhiteSpace(callbackMethod);
			hasCallback &= !string.IsNullOrWhiteSpace(callbackVar);
			SetVolumeInternal(volumeSlider.value);
			initialized = true;
		}

		public void _DragVolume() {
			if (isSettingControl) return;
			Initialize();
			SetVolumeInternal(volumeSlider.value);
			UpdateCallback();
		}

		public void _SetVolume(float volume) {
			Initialize();
			SetVolumeInternal(volume);
			SetVolumeSlider(volume);
		}

		public void _ToggleMute() {
			Initialize();
			_SetMute(!isMuted);
		}

		public void _SetMute(bool mute) {
			Initialize();
			isMuted = mute;
			UpdateCallback();
			UpdateGraphics();
			SetVolumeSlider(mute ? 0 : nonMutedVolume);
		}

		private void SetVolumeInternal(float volume) {
			nonMutedVolume = volume;
			UpdateGraphics();
		}

		private void SetVolumeSlider(float value) {
			isSettingControl = true;
			volumeSlider.value = value;
			isSettingControl = false;
		}

		private void UpdateGraphics() {
			int state;
			if (isMuted) state = 0;
			else if (nonMutedVolume == 0) state = 1;
			else if (nonMutedVolume <= 0.5) state = 2;
			else state = 3;
			if (state == muteButton.GetCurrentMode()) return;
			muteButton._SetMode(state);
			volumeSlider.interactable = !isMuted;
		}

		private void UpdateCallback() {
			if (!hasCallback) return;
			callback.SetProgramVariable(callbackVar, isMuted ? 0.0f : nonMutedVolume);
			callback.SendCustomEvent(callbackMethod);
		}
	}
}
