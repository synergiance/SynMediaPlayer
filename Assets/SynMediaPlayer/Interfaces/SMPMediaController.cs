
using Synergiance.MediaPlayer.Diagnostics;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace Synergiance.MediaPlayer.Interfaces {
	public class SMPMediaController : DiagnosticBehaviour {
		protected override string DebugName => "Media Controller";
		private VideoManager videoManager;

		/// <summary>
		/// ID we use to talk to the Video Manager
		/// </summary>
		protected int MediaControllerId { get; private set; } = -1;

		/// <summary>
		/// Sets and returns the video manager associated with this media controller.
		/// </summary>
		protected VideoManager VideoManager {
			set {
				if (MediaControllerId >= 0) {
					LogError("Error: Video Manager cannot be set after registering!");
					return;
				}
				videoManager = value;
			}
			get => videoManager;
		}

		/// <summary>
		/// Returns whether media player is registered.
		/// </summary>
		protected bool MediaControllerValid => MediaControllerId >= 0;

		#region Media Information Properties
		/// <summary>
		/// Returns whether media is currently playing
		/// </summary>
		protected bool MediaIsPlaying => MediaControllerValid && VideoManager._GetPlaying(MediaControllerId);

		/// <summary>
		/// Returns duration of currently playing media
		/// </summary>
		protected float MediaDuration => MediaControllerValid ? VideoManager._GetDuration(MediaControllerId) : -1;

		/// <summary>
		/// Returns whether media is ready to be played
		/// </summary>
		protected bool MediaIsReady => MediaControllerValid && VideoManager._GetMediaReady(MediaControllerId);

		/// <summary>
		/// This property designates whether or not media should loop when it gets to the end
		/// </summary>
		protected bool LoopMedia {
			set => SetLoopMedia(value);
			get => MediaControllerValid && VideoManager._GetLoop(MediaControllerId);
		}

		/// <summary>
		/// This property designates whether VRChat's automatic audio resync occurs
		/// </summary>
		protected bool EnableMediaResync {
			set => SetEnableMediaResync(value);
			get => MediaControllerValid && VideoManager._GetMediaResync(MediaControllerId);
		}

		/// <summary>
		/// Mutes or unmutes playing media
		/// </summary>
		protected bool MuteMedia {
			set => SetMuteMedia(value);
			get => MediaControllerValid && VideoManager._GetMute(MediaControllerId);
		}

		/// <summary>
		/// The current time at the play head.
		/// </summary>
		protected float MediaTime {
			set => SetMediaTime(value);
			get => MediaControllerValid ? VideoManager._GetTime(MediaControllerId) : -1;
		}

		/// <summary>
		/// Volume of playing media
		/// </summary>
		protected float MediaVolume {
			set => SetMediaVolume(value);
			get => MediaControllerValid ? VideoManager._GetVolume(MediaControllerId) : -1;
		}
		#endregion

		/// <summary>
		/// Registers Media Controller with Video Manager so we can get a handle
		/// to control it. Without this, Video Manager will not allocate a
		/// virtual video player to us.
		/// </summary>
		protected void RegisterWithVideoManager() {
			// TODO: Hook up to video manager
			if (MediaControllerId >= 0) {
				LogWarning("Already registered!");
				return;
			}

			MediaControllerId = 0;
		}

		#region Internal Setter Methods
		private void SetMediaTime(float _value) {
			if (!MediaControllerValid) return;
			VideoManager._SeekTo(MediaControllerId, _value);
		}

		private void SetMuteMedia(bool _value) {
			if (!MediaControllerValid) return;
			VideoManager._SetMute(MediaControllerId, _value);
		}

		private void SetMediaVolume(float _value) {
			if (!MediaControllerValid) return;
			VideoManager._SetVolume(MediaControllerId, _value);
		}

		private void SetLoopMedia(bool _value) {
			if (!MediaControllerValid) return;
			VideoManager._SetLoop(MediaControllerId, _value);
		}

		private void SetEnableMediaResync(bool _value) {
			if (!MediaControllerValid) return;
			VideoManager._SetMediaResync(MediaControllerId, _value);
		}
		#endregion

		#region Interface Action Methods
		/// <summary>
		/// Play or resume current media
		/// </summary>
		protected void PlayMedia() {
			if (!MediaControllerValid) return;
			VideoManager._Play(MediaControllerId);
		}

		/// <summary>
		/// Pause currently playing media
		/// </summary>
		protected void PauseMedia() {
			if (!MediaControllerValid) return;
			VideoManager._Pause(MediaControllerId);
		}

		/// <summary>
		/// Stop playing current media
		/// </summary>
		protected void StopMedia() {
			if (!MediaControllerValid) return;
			VideoManager._Stop(MediaControllerId);
		}

		/// <summary>
		/// Move on to next loaded media
		/// </summary>
		protected void PlayNextMedia() {
			if (!MediaControllerValid) return;
			VideoManager._PlayNext(MediaControllerId);
		}

		/// <summary>
		/// Load media to be played
		/// </summary>
		/// <param name="_link">URL of media to be loaded</param>
		/// <param name="_type">Type of link this is, video, stream, low latency link</param>
		/// <param name="_playImmediately">Set to true to automatically play as soon as media is loaded</param>
		protected void LoadMedia(VRCUrl _link, VideoType _type, bool _playImmediately = false) {
			if (!MediaControllerValid) return;
			VideoManager._LoadVideo(_link, _type, MediaControllerId, _playImmediately);
		}

		/// <summary>
		/// Load media to be played next
		/// </summary>
		/// <param name="_link">URL of media to be loaded</param>
		/// <param name="_type">Type of link this is, video, stream, low latency link</param>
		protected void LoadNextMedia(VRCUrl _link, VideoType _type) {
			if (!MediaControllerValid) return;
			VideoManager._LoadNextVideo(_link, _type, MediaControllerId);
		}
		#endregion

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
		/// Event method. This is fired when an error is encountered
		/// </summary>
		/// <param name="_error">Error that was encountered</param>
		protected virtual void PlayerError(MediaError _error) {}
		#endregion

		/// <summary>
		/// Callback interface for Video Manager. Do not use unless you know
		/// what you're doing.
		/// </summary>
		/// <param name="_event">Callback Event to call</param>
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
				case CallbackEvent.PlayerUnlocked:
				case CallbackEvent.PlayerInitialized:
				case CallbackEvent.GainedPermissions:
					LogWarning("This code path should never happen!");
					break;
				case CallbackEvent.PlayerError:
					LogWarning("Please use _SendError instead when sending errors!");
					MediaError err = MediaError.Uninitialized;
					if (VideoManager != null)
						err = VideoManager.lastError;
					_SendError(err);
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
		public void _SendError(MediaError _err) {
			PlayerError(_err);
			Log($"Error: {_err}");
		}
	}
}
