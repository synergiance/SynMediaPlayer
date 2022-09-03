using Synergiance.MediaPlayer.Diagnostics;
using UnityEngine;
using VRC.SDK3.Components.Video;
using VRC.SDK3.Video.Components.Base;
using VRC.SDKBase;

namespace Synergiance.MediaPlayer {
	public class VideoRelay : DiagnosticBehaviour {
		[SerializeField] private BaseVRCVideoPlayer videoSource;
		[SerializeField] private Renderer videoRendererSource;
		[SerializeField] private string videoTextureName = "_MainTex";
		[SerializeField] private AudioSource[] speakers;
		[SerializeField] private int videoType;
		[SerializeField] private string videoName;

		public int VideoType => videoType;

		public bool IsPlaying => initialized && videoSource.IsPlaying;
		public bool IsReady => initialized && videoSource.IsReady;
		public float Duration => initialized ? videoSource.GetDuration() : float.NaN;

		public bool Loop {
			get => initialized && videoSource.Loop;
			set { if (initialized) videoSource.Loop = value; }
		}

		public bool AutomaticResync {
			get => initialized && videoSource.EnableAutomaticResync;
			set { if (initialized) videoSource.EnableAutomaticResync = value; }
		}

		public float Time {
			get => initialized ? videoSource.GetTime() : float.NaN;
			set { if (initialized) videoSource.SetTime(value); }
		}

		private VideoManager relayPoint;
		private int identifier;

		private bool initialized;

		/// <summary>
		/// Initializes the relay with the correct relay point and identifier.
		/// This allows the relay to properly report back to the Video Manager
		/// it's connected to.
		/// </summary>
		/// <param name="_relayPoint">The Video Manager to report back to</param>
		/// <param name="_identifier">The identifier to use when reporting back</param>
		/// <returns>The name of the video player, or the game object if not specified.</returns>
		public string InitializeRelay(VideoManager _relayPoint, int _identifier) {
			if (initialized) {
				LogError("Already initialized!");
				return null;
			}
			if (string.IsNullOrWhiteSpace(videoName)) videoName = gameObject.name;
			relayPoint = _relayPoint;
			identifier = _identifier;
			initialized = true;
			return videoName;
		}

		/// <summary>
		/// Plays the video
		/// </summary>
		/// <returns>Boolean indicating success</returns>
		public bool _Play() {
			if (!initialized) return false;
			videoSource.Play();
			return true;
		}

		/// <summary>
		/// Pauses the video
		/// </summary>
		/// <returns>Boolean indicating success</returns>
		public bool _Pause() {
			if (!initialized) return false;
			videoSource.Pause();
			return true;
		}

		/// <summary>
		/// Stops the video
		/// </summary>
		/// <returns>Boolean indicating success</returns>
		public bool _Stop() {
			if (!initialized) return false;
			videoSource.Stop();
			return true;
		}

		/// <summary>
		/// Loads a video
		/// </summary>
		/// <param name="_link">Link to the video to load</param>
		/// <param name="_playImmediately">If true, video player will play the
		/// video as soon as it's loaded</param>
		/// <returns>Boolean indicating success</returns>
		public bool _Load(VRCUrl _link, bool _playImmediately) {
			if (!initialized) return false;
			if (_playImmediately) videoSource.PlayURL(_link);
			else videoSource.LoadURL(_link);
			return true;
		}

		private void SendRelayEvent(string _eventName) {
			if (!initialized) {
				Log("(Uninitialized) Event ignored: " + _eventName);
				return;
			}
			relayPoint.SetProgramVariable("relayIdentifier", identifier);
			relayPoint.SendCustomEvent(_eventName);
		}

		public override void OnVideoEnd() {
			SendRelayEvent("_RelayVideoEnd");
		}

		public override void OnVideoReady() {
			SendRelayEvent("_RelayVideoReady");
		}

		public override void OnVideoError(VideoError _videoError) {
			relayPoint.SetProgramVariable("relayVideoError", _videoError);
			SendRelayEvent("_RelayVideoError");
		}

		public override void OnVideoPlay() {
			SendRelayEvent("_RelayVideoPlay");
		}

		public override void OnVideoStart() {
			SendRelayEvent("_RelayVideoStart");
		}

		public override void OnVideoLoop() {
			SendRelayEvent("_RelayVideoLoop");
		}

		public override void OnVideoPause() {
			SendRelayEvent("_RelayVideoPause");
		}
	}
}
