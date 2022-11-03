
using Synergiance.MediaPlayer.Diagnostics;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace Synergiance.MediaPlayer.Interfaces {
	public class SMPMediaController : DiagnosticBehaviour {
		protected override string DebugName => "Media Controller";
		protected VideoManager videoManager;
		protected int mediaControllerId = -1;

		protected bool MediaIsPlaying => MediaControllerValid && videoManager._GetPlaying(mediaControllerId);
		protected float MediaDuration => MediaControllerValid ? videoManager._GetDuration(mediaControllerId) : -1;
		protected bool MediaIsReady => MediaControllerValid && videoManager._GetMediaReady(mediaControllerId);
		protected bool MediaControllerValid => mediaControllerId >= 0;

		protected bool LoopMedia {
			set => SetLoopMedia(value);
			get => MediaControllerValid && videoManager._GetLoop(mediaControllerId);
		}

		protected bool EnableMediaResync {
			set => SetEnableMediaResync(value);
			get => MediaControllerValid && videoManager._GetMediaResync(mediaControllerId);
		}

		protected bool MuteMedia {
			set => SetMuteMedia(value);
			get => MediaControllerValid && videoManager._GetMute(mediaControllerId);
		}

		protected float MediaTime {
			set => SetMediaTime(value);
			get => MediaControllerValid ? videoManager._GetTime(mediaControllerId) : -1;
		}

		protected float MediaVolume {
			set => SetMediaVolume(value);
			get => MediaControllerValid ? videoManager._GetVolume(mediaControllerId) : -1;
		}

		protected void RegisterWithVideoManager() {
			// TODO: Hook up to video manager
			if (mediaControllerId >= 0) {
				LogWarning("Already registered!");
				return;
			}

			mediaControllerId = 0;
		}

		private void SetMediaTime(float _value) {
			if (!MediaControllerValid) return;
			videoManager._SeekTo(mediaControllerId, _value);
		}

		private void SetMuteMedia(bool _value) {
			if (!MediaControllerValid) return;
			videoManager._SetMute(mediaControllerId, _value);
		}

		private void SetMediaVolume(float _value) {
			if (!MediaControllerValid) return;
			videoManager._SetVolume(mediaControllerId, _value);
		}

		private void SetLoopMedia(bool _value) {
			if (!MediaControllerValid) return;
			videoManager._SetLoop(mediaControllerId, _value);
		}

		private void SetEnableMediaResync(bool _value) {
			if (!MediaControllerValid) return;
			videoManager._SetMediaResync(mediaControllerId, _value);
		}

		protected void PlayMedia() {
			if (!MediaControllerValid) return;
			videoManager._Play(mediaControllerId);
		}

		protected void PauseMedia() {
			if (!MediaControllerValid) return;
			videoManager._Pause(mediaControllerId);
		}

		protected void StopMedia() {
			if (!MediaControllerValid) return;
			videoManager._Stop(mediaControllerId);
		}

		protected void PlayNextMedia() {
			if (!MediaControllerValid) return;
			videoManager._PlayNext(mediaControllerId);
		}

		protected void LoadMedia(VRCUrl _link, VideoType _type, bool _playImmediately = false) {
			if (!MediaControllerValid) return;
			videoManager._LoadVideo(_link, _type, mediaControllerId, _playImmediately);
		}

		protected void LoadNextMedia(VRCUrl _link, VideoType _type) {
			if (!MediaControllerValid) return;
			videoManager._LoadNextVideo(_link, _type, mediaControllerId);
		}

		#region Virtual Methods
		protected virtual void MediaLoading() {}
		protected virtual void MediaReady() {}
		protected virtual void MediaStart() {}
		protected virtual void MediaEnd() {}
		protected virtual void MediaNext() {}
		protected virtual void MediaLoop() {}
		protected virtual void MediaPlay() {}
		protected virtual void MediaPause() {}
		protected virtual void QueuedMediaLoading() {}
		protected virtual void QueuedMediaReady() {}

		protected virtual void PlayerLocked() {}
		protected virtual void PlayerUnlocked() {}
		protected virtual void PlayerInitialized() {}
		protected virtual void GainedPermissions() {}
		protected virtual void PlayerError(MediaError _error) {}
		#endregion

		public void _SendCallback(CallbackEvent _event) {
			Log($"Received event: {_event}");
			switch (_event) {
				case CallbackEvent.MediaLoading:
					MediaLoading();
					break;
				case CallbackEvent.MediaReady:
					MediaReady();
					break;
				case CallbackEvent.MediaStart:
					MediaStart();
					break;
				case CallbackEvent.MediaEnd:
					MediaEnd();
					break;
				case CallbackEvent.MediaNext:
					MediaNext();
					break;
				case CallbackEvent.MediaLoop:
					MediaLoop();
					break;
				case CallbackEvent.MediaPlay:
					MediaPlay();
					break;
				case CallbackEvent.MediaPause:
					MediaPause();
					break;
				case CallbackEvent.QueueMediaLoading:
					QueuedMediaLoading();
					break;
				case CallbackEvent.QueueMediaReady:
					QueuedMediaReady();
					break;
				case CallbackEvent.PlayerLocked:
					PlayerLocked();
					break;
				case CallbackEvent.PlayerUnlocked:
					PlayerUnlocked();
					break;
				case CallbackEvent.PlayerInitialized:
					PlayerInitialized();
					break;
				case CallbackEvent.GainedPermissions:
					GainedPermissions();
					break;
				case CallbackEvent.PlayerError:
					LogWarning($"Please use _SendError instead when sending errors!");
					MediaError err = MediaError.Uninitialized;
					if (videoManager != null)
						err = videoManager.lastError;
					_SendError(err);
					break;
				default:
					LogWarning($"Unknown Event: {_event}");
					break;
			}
		}

		public void _SendError(MediaError _err) {
			PlayerError(_err);
			Log($"Error: {_err}");
		}
	}
}
