
using Synergiance.MediaPlayer.Diagnostics;

namespace Synergiance.MediaPlayer.Interfaces {
	public class SMPCallbackReceiver : DiagnosticBehaviour {
		protected override string DebugName => "Callback Receiver";

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

		public void SendCallback(CallbackEvent _event, VideoBehaviour _sender) {
			Log($"Received event from \"{(_sender == null ? "Unknown sender" : _sender.name)}\": {_event}");
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
					if (_sender == null) break;
					MediaError err = _sender.lastError;
					Log($"Error: {err}");
					PlayerError(err);
					break;
				default:
					LogWarning($"Unknown Event: {_event}");
					break;
			}
		}
	}
}
