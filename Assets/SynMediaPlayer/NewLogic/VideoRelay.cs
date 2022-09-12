using Synergiance.MediaPlayer.Diagnostics;
using UnityEngine;
using VRC.SDK3.Components.Video;
using VRC.SDK3.Video.Components.Base;
using VRC.SDKBase;

namespace Synergiance.MediaPlayer {
	public class VideoRelay : DiagnosticBehaviour {
		[SerializeField] private BaseVRCVideoPlayer videoSource;
		[SerializeField] private Renderer videoRendererSource;
		[SerializeField] private int videoMaterialIndex;
		[SerializeField] private string videoTextureName = "_MainTex";
		[SerializeField] private AudioSource[] speakers;
		[SerializeField] private int videoType;
		[SerializeField] private string videoName;

		protected override string DebugName => "Video Relay";
		protected override string DebugColor => ColorToHtmlStringRGB(new Color(0.7f, 0.6f, 0.15f));

		/// <summary>
		/// Video type of the video (0 for video, 1 for stream,
		/// and 2 for low latency)
		/// </summary>
		public int VideoType => videoType;

		/// <summary>
		/// Accessor for whether there is a video playing
		/// </summary>
		public bool IsPlaying => initialized && videoSource.IsPlaying;

		/// <summary>
		/// Accessor for whether a video is ready to be played
		/// </summary>
		public bool IsReady => initialized && videoSource.IsReady;
		
		/// <summary>
		/// Accessor for duration of the current video. Will be NaN if relay is
		/// not initialized.
		/// </summary>
		public float Duration => initialized ? videoSource.GetDuration() : float.NaN;

		/// <summary>
		/// Specifies whether a video will loop or not.
		/// </summary>
		public bool Loop {
			get => initialized && videoSource.Loop;
			set { if (initialized) videoSource.Loop = value; }
		}

		/// <summary>
		/// Specifies whether automatic video/audio sync will be enabled.
		/// </summary>
		public bool AutomaticResync {
			get => initialized && videoSource.EnableAutomaticResync;
			set { if (initialized) videoSource.EnableAutomaticResync = value; }
		}

		/// <summary>
		/// Current time in a video. Will be NaN if relay is not initialized.
		/// </summary>
		public float Time {
			get => initialized ? videoSource.GetTime() : float.NaN;
			set { if (initialized) videoSource.SetTime(value); }
		}

		private VideoManager relayPoint;
		private int identifier;
		private Texture videoTextureCache;
		private Material videoMaterial;

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
			if (videoRendererSource == null) {
				LogError("No renderer!");
				return null;
			}
			if (videoRendererSource.materials == null || videoMaterialIndex >= videoRendererSource.materials.Length || videoMaterialIndex < 0) {
				LogError("Video Material Index out of bounds!");
				return null;
			}
			videoMaterial = videoRendererSource.materials[videoMaterialIndex];
			if (videoMaterial == null) {
				LogError("There is no material in that slot!");
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

		private void Update() {
			if (!initialized) return;
			if (!videoSource.IsReady) return;
			if (!videoSource.IsPlaying) return;
			CheckTextureChange();
		}

		// ReSharper disable Unity.PerformanceAnalysis
		private void CheckTextureChange() {
			Texture tempTexture = videoMaterial.GetTexture(videoTextureName);
			if (tempTexture != videoTextureCache) return;
			Log("New Video Texture!");
			videoTextureCache = tempTexture;
			relayPoint._RelayVideoTextureChange(identifier, videoTextureCache);
		}

		private bool UninitializedLog(string _eventName) {
			if (initialized) return false;
			Log($"<color=#808080>(Uninitialized)</color> Event \"{_eventName}\" ignored on \"{gameObject.name}\"");
			return true;
		}

		public override void OnVideoEnd() {
			if (UninitializedLog("OnVideoEnd")) return;
			relayPoint._RelayVideoEnd(identifier);
			CheckTextureChange();
		}

		public override void OnVideoReady() {
			if (UninitializedLog("OnVideoReady")) return;
			relayPoint._RelayVideoReady(identifier);
			CheckTextureChange();
		}

		public override void OnVideoError(VideoError _videoError) {
			if (UninitializedLog("OnVideoError")) return;
			relayPoint._RelayVideoError(identifier, _videoError);
			CheckTextureChange();
		}

		public override void OnVideoPlay() {
			if (UninitializedLog("OnVideoPlay")) return;
			relayPoint._RelayVideoPlay(identifier);
			CheckTextureChange();
		}

		public override void OnVideoStart() {
			if (UninitializedLog("OnVideoStart")) return;
			relayPoint._RelayVideoStart(identifier);
			CheckTextureChange();
		}

		public override void OnVideoLoop() {
			if (UninitializedLog("OnVideoLoop")) return;
			relayPoint._RelayVideoLoop(identifier);
			CheckTextureChange();
		}

		public override void OnVideoPause() {
			if (UninitializedLog("OnVideoPause")) return;
			relayPoint._RelayVideoPause(identifier);
			CheckTextureChange();
		}
	}
}
