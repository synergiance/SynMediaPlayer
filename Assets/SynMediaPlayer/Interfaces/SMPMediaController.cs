
using Synergiance.MediaPlayer.Diagnostics;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace Synergiance.MediaPlayer.Interfaces {
	public class SMPMediaController : DiagnosticBehaviour {
		protected override string DebugName => "Media Controller";
		protected VideoManager videoManager;
		protected int mediaControllerId;

		protected bool MediaIsPlaying {
			get => false;
		}

		protected bool MediaIsReady {
			get => false;
		}

		protected bool LoopMedia {
			set;
			get;
		}

		protected bool EnableMediaResync {
			set;
			get;
		}

		protected bool MuteMedia {
			set;
			get;
		}

		protected float CurrentMediaTime {
			set;
			get;
		}

		protected float MediaDuration {
			set;
			get;
		}

		protected float MediaVolume {
			set;
			get;
		}

		protected void RegisterWithVideoManager() {
			// TODO: Hook up to video manager
		}

		protected void PlayMedia() {}

		protected void PauseMedia() {}

		protected void StopMedia() {}

		protected void LoadMedia(VRCUrl _link, bool _playImmediately) {}

		protected void SeekMedia(float _timestamp) {}

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
