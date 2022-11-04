
using Synergiance.MediaPlayer.Diagnostics;

namespace Synergiance.MediaPlayer.Interfaces {
	public class SMPCallbackReceiver : DiagnosticBehaviour {
		protected override string DebugName => "Callback Receiver";

		#region Virtual Methods
		/// <summary>
		/// Event method. This is fired when media begins loading.
		/// </summary>
		protected virtual void MediaLoading() {}

		/// <summary>
		/// Event method. This is fired when media becomes ready.
		/// </summary>
		protected virtual void MediaReady() {}

		/// <summary>
		/// Event method. This is fired when media starts playing.
		/// </summary>
		protected virtual void MediaStart() {}

		/// <summary>
		/// Event method. This is fired when media finishes playing.
		/// </summary>
		protected virtual void MediaEnd() {}

		/// <summary>
		/// Event method. This is fired when next media begins.
		/// </summary>
		protected virtual void MediaNext() {}

		/// <summary>
		/// Event method. This is fired when media loops.
		/// </summary>
		protected virtual void MediaLoop() {}

		/// <summary>
		/// Event method. This is fired when media resumes playing.
		/// </summary>
		protected virtual void MediaPlay() {}

		/// <summary>
		/// Event method. This is fired when media pauses.
		/// </summary>
		protected virtual void MediaPause() {}

		/// <summary>
		/// Event method. This is fired when queued media begins loading.
		/// </summary>
		protected virtual void QueuedMediaLoading() {}

		/// <summary>
		/// Event method. This is fired when queued media becomes ready.
		/// </summary>
		protected virtual void QueuedMediaReady() {}

		/// <summary>
		/// Event method. This is fired when the player is locked.
		/// </summary>
		protected virtual void PlayerLocked() {}

		/// <summary>
		/// Event method. This is fired when the player is unlocked.
		/// </summary>
		protected virtual void PlayerUnlocked() {}

		/// <summary>
		/// Event method. This is fired when the player is first initialized.
		/// </summary>
		protected virtual void PlayerInitialized() {}

		/// <summary>
		/// Event method. This is fired when user gains permissions. Permissions
		/// are gained in very strict circumstances, so it will only be fired
		/// once and never revoked.
		/// </summary>
		protected virtual void GainedPermissions() {}

		/// <summary>
		/// Event method. This is fired when an error is encountered
		/// </summary>
		/// <param name="_error">Error that was encountered</param>
		protected virtual void PlayerError(MediaError _error) {}
		#endregion

		/// <summary>
		/// Interface for callbacks. Do not use unless you know what you're doing.
		/// </summary>
		/// <param name="_event">Callback Event to call</param>
		public virtual void _SendCallback(CallbackEvent _event) {
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
					LogWarning("Please use _SendError instead when sending errors!");
					_SendError(MediaError.Internal);
					break;
				default:
					LogWarning($"Unknown Event: {_event}");
					break;
			}
		}

		/// <summary>
		/// Callback interface for errors. Do not use unless you know what
		/// you're doing.
		/// </summary>
		/// <param name="_err">Error to be sent</param>
		public virtual void _SendError(MediaError _err) {
			PlayerError(_err);
			Log($"Error: {_err}");
		}
	}
}
