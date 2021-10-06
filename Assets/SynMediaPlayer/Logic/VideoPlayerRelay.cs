using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components.Video;
using VRC.SDK3.Video.Components.Base;
using VRC.SDKBase;

namespace SynPhaxe {
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

		public void _Play() { videoPlayer.Play(); }
		public void _Pause() { videoPlayer.Pause(); }
		public void _Stop() { videoPlayer.Stop(); }
		public void _LoadURL(VRCUrl url) { videoPlayer.LoadURL(url); }
		public void _PlayURL(VRCUrl url) { videoPlayer.PlayURL(url); }
		public void _SetLoop(bool loop) { videoPlayer.Loop = loop; }
		public void _SetTime(float time) { videoPlayer.SetTime(time); }
		public void _SetVolume(float volume) { speaker.volume = volume; }
		public bool GetLoop() { return videoPlayer.Loop; }
		public float GetTime() { return videoPlayer.GetTime(); }
		public float GetDuration() { return videoPlayer.GetDuration(); }
		public float GetVolume() { return speaker.volume; }
		public bool GetReady() { return videoPlayer.IsReady; }
		public bool GetPlaying() { return videoPlayer.IsPlaying; }
		public bool GetStream() { return isStream; }
		public string GetName() { return playerName; }
		public AudioSource GetSpeaker() { return speaker; }

		public override void OnVideoEnd() {
			relayPoint.SetProgramVariable("relayIdentifier", identifier);
			relayPoint.SendCustomEvent("_RelayVideoEnd");
		}

		public override void OnVideoReady() {
			relayPoint.SetProgramVariable("relayIdentifier", identifier);
			relayPoint.SendCustomEvent("_RelayVideoReady");
		}

		public override void OnVideoError(VideoError videoError) {
			relayPoint.SetProgramVariable("relayIdentifier", identifier);
			relayPoint.SetProgramVariable("relayVideoError", videoError);
			relayPoint.SendCustomEvent("_RelayVideoError");
		}

		public override void OnVideoPlay() {
			relayPoint.SetProgramVariable("relayIdentifier", identifier);
			relayPoint.SendCustomEvent("_RelayVideoPlay");
		}

		public override void OnVideoStart() {
			relayPoint.SetProgramVariable("relayIdentifier", identifier);
			relayPoint.SendCustomEvent("_RelayVideoStart");
		}

		public override void OnVideoLoop() {
			relayPoint.SetProgramVariable("relayIdentifier", identifier);
			relayPoint.SendCustomEvent("_RelayVideoLoop");
		}

		public override void OnVideoPause() {
			relayPoint.SetProgramVariable("relayIdentifier", identifier);
			relayPoint.SendCustomEvent("_RelayVideoPause");
		}
	}
}
