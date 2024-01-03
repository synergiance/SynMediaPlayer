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
		[SerializeField] private AudioSource[] speakers;
		[SerializeField] private string playerName = "Video Player";
		[SerializeField] private bool isStream;

		[Header("Callback Settings")]
		[SerializeField] private UdonSharpBehaviour relayPoint;
		[SerializeField] private int identifier;

		private bool isValid;

		private bool[] speakersPlaying;

		private void Start() {
			isValid = false;
			if (!videoPlayer) return;
			if (speakers == null) return;
			if (speakers.Length < 1) return;
			speakersPlaying = new bool[speakers.Length];
			isValid = true;
		}

		private void CheckAudioSources(string method) {
			for (int i = 0; i < speakers.Length; i++) {
				if (speakers[i].isPlaying != speakersPlaying[i]) {
					speakersPlaying[i] = !speakersPlaying[i];
					Debug.Log($"Speaker {i} is now {(speakersPlaying[i] ? "playing" : "not playing")}.");
				}
			}
		}

		public void _Play() { if (isValid) videoPlayer.Play(); }
		public void _Pause() { if (isValid) videoPlayer.Pause(); }
		public void _Stop() { if (isValid) videoPlayer.Stop(); }
		public void _LoadURL(VRCUrl url) { if (isValid) videoPlayer.LoadURL(url); }
		public void _PlayURL(VRCUrl url) { if (isValid) videoPlayer.PlayURL(url); }

		public bool Loop { set { if (isValid) videoPlayer.Loop = value; } get => isValid && videoPlayer.Loop; }
		public float Volume { set { if (isValid) foreach (AudioSource speaker in speakers) { speaker.volume = value; } } get => isValid ? speakers[0].volume : 0; }
		public float Time { set { if (isValid) videoPlayer.SetTime(value); } get => isValid ? videoPlayer.GetTime() : 0; }
		public float Duration => isValid ? videoPlayer.GetDuration() : 0;
		public bool IsReady => isValid && videoPlayer.IsReady;
		public bool IsPlaying => isValid && videoPlayer.IsPlaying;
		public string PlayerName => playerName;
		public bool IsStream => isStream;
		public AudioSource Speaker => isValid ? speakers[0] : null;

		private void SendRelayEvent(string eventName) {
			relayPoint.SetProgramVariable("relayIdentifier", identifier);
			relayPoint.SendCustomEvent(eventName);
		}

		public override void OnVideoEnd() {
			SendRelayEvent("_RelayVideoEnd");
		}

		public override void OnVideoReady() {
			CheckAudioSources("OnVideoReady");
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
