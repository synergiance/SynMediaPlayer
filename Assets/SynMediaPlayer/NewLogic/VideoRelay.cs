using Synergiance.MediaPlayer.Diagnostics;
using UnityEngine;
using VRC.SDK3.Components.Video;
using VRC.SDK3.Video.Components.Base;

namespace Synergiance.MediaPlayer {
	public class VideoRelay : DiagnosticBehaviour {
		[SerializeField] private BaseVRCVideoPlayer videoSource;
		[SerializeField] private Renderer videoRendererSource;
		[SerializeField] private string videoTextureName = "_MainTex";
		[SerializeField] private AudioSource[] speakers;
		[SerializeField] private int videoType;
		[SerializeField] private string videoName;

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
