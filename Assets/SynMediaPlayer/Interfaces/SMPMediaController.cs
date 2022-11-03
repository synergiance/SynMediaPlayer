
using VRC.SDKBase;

namespace Synergiance.MediaPlayer.Interfaces {
	public class SMPMediaController : SMPCallbackReceiver {
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
		protected bool RegisterWithVideoManager(string _name) {
			if (MediaControllerValid) {
				LogWarning("Already registered!");
				return true;
			}

			MediaControllerId = VideoManager._Register(this, _name);
			return MediaControllerValid;
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

		/// <summary>
		/// Interface for callbacks on media controllers. Do not use unless you
		/// know what you're doing.
		/// </summary>
		/// <param name="_event">Callback Event to call</param>
		public override void _SendCallback(CallbackEvent _event) {
			switch (_event) {
				case CallbackEvent.PlayerLocked:
				case CallbackEvent.PlayerUnlocked:
				case CallbackEvent.PlayerInitialized:
				case CallbackEvent.GainedPermissions:
					LogWarning("Media controller should never receive events from itself or another media controller!");
					break;
				default:
					base._SendCallback(_event);
					break;
			}
		}
	}
}
