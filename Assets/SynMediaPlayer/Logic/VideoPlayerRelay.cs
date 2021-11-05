using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components.Video;
using VRC.SDK3.Video.Components.Base;
using VRC.SDKBase;

namespace Synergiance.MediaPlayer {
	/*
	 * Class to relay video player information to a host class that interacts
	 * with multiple video players in order to help the host class in
	 * identifying which video player is which. This class also allows video
	 * player controller classes to be on different objects than their
	 * respective video player. This also acts as a relay to relay commands to
	 * a video player component, because that may help.
	 */
	public class VideoPlayerRelay : UdonSharpBehaviour {
		[Header("Player Settings")] // Video Player Settings
		[SerializeField] private BaseVRCVideoPlayer videoPlayer;
		[SerializeField] private AudioSource speaker;
		[SerializeField] private string playerName = "Video Player";
		[SerializeField] private bool isStream;

		[Header("Callback Settings")]
		[SerializeField] private UdonSharpBehaviour relayPoint;
		[SerializeField] private int identifier;

		private bool isValid;

		private void Start() {
			isValid = videoPlayer;
		}

		public void _Play() { if (isValid) videoPlayer.Play(); }
		public void _Pause() { if (isValid) videoPlayer.Pause(); }
		public void _Stop() { if (isValid) videoPlayer.Stop(); }
		public void _LoadURL(VRCUrl url) { if (isValid) videoPlayer.LoadURL(url); }
		public void _PlayURL(VRCUrl url) { if (isValid) videoPlayer.PlayURL(url); }
		public void _SetLoop(bool loop) { if (isValid) videoPlayer.Loop = loop; }
		public void _SetTime(float time) { if (isValid) videoPlayer.SetTime(time); }
		public void _SetVolume(float volume) { if (isValid) speaker.volume = volume; }
		public bool GetLoop() { return isValid && videoPlayer.Loop; }
		public float GetTime() { return isValid ? videoPlayer.GetTime() : 0; }
		public float GetDuration() { return isValid ? videoPlayer.GetDuration() : 0; }
		public float GetVolume() { return isValid ? speaker.volume : 0; }
		public bool GetReady() { return isValid && videoPlayer.IsReady; }
		public bool GetPlaying() { return isValid && videoPlayer.IsPlaying; }
		public bool GetStream() { return isStream; }
		public string GetName() { return playerName; }
		public AudioSource GetSpeaker() { return speaker; }

		private void SendRelayEvent(string eventName) {
			relayPoint.SetProgramVariable("relayIdentifier", identifier);
			relayPoint.SendCustomEvent(eventName);
		}

		public override void OnVideoEnd() {
			SendRelayEvent("_RelayVideoEnd");
		}

		public override void OnVideoReady() {
			SendRelayEvent("_RelayVideoReady");
		}

		public override void OnVideoError(VideoError videoError) {
			relayPoint.SetProgramVariable("relayVideoError", videoError);
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
