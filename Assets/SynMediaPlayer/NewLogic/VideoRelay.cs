using System;
using Synergiance.MediaPlayer.Diagnostics;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components.Video;
using VRC.SDK3.Video.Components.Base;
using VRC.SDKBase;

namespace Synergiance.MediaPlayer {
	[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
	public class VideoRelay : DiagnosticBehaviour {
		[SerializeField] private BaseVRCVideoPlayer videoSource;
		[SerializeField] private Renderer videoRendererSource;
		[SerializeField] private int videoMaterialIndex;
		[SerializeField] private string videoTextureName = "_MainTex";
		[SerializeField] private VideoType videoType;
		[SerializeField] private string videoName;
		[SerializeField] private AudioSource[] speakers;

		protected override string DebugName => "Video Relay";
		protected override string DebugColor => ColorToHtmlStringRGB(new Color(0.7f, 0.6f, 0.15f));

		/// <summary>
		/// Video type of the video (0 for video, 1 for stream,
		/// and 2 for low latency)
		/// </summary>
		public VideoType VideoType => videoType;

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

		/// <summary>
		/// Whether the video is muted or not
		/// </summary>
		public bool Mute {
			get => !initialized || muted;
			set {
				muted = value;
				if (!initialized) return;
				foreach (AudioSource speaker in speakers)
					speaker.mute = value;
			}
		}

		/// <summary>
		/// Volume the video will play at
		/// </summary>
		public float Volume {
			get => initialized ? volume : 0;
			set {
				volume = value;
				if (!initialized) return;
				foreach (AudioSource speaker in speakers)
					speaker.volume = value * relativeVolume;
			}
		}

		/// <summary>
		/// Main speaker (front left) used by the video relay
		/// </summary>
		public AudioSource MainSpeaker => initialized ? speakers[0] : null;

		private VideoManager relayPoint;
		private int identifier;
		private Texture videoTextureCache;
		private Material videoMaterial;

		private bool muted;
		private float volume;
		private float relativeVolume;
		private int speakersActive;

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

			if (speakers == null) speakers = new AudioSource[0];

			Log($"Initialized with relay point {_relayPoint.name} with ID {_identifier}");

			relayPoint = _relayPoint;
			identifier = _identifier;
			initialized = true;
			UpdateSpeakers();
			return videoName;
		}

		/// <summary>
		/// Nullifies audio template and makes relay effectively mute
		/// </summary>
		public void _NullAudioTemplate() {
			speakersActive = 0;
			relativeVolume = 0;
			UpdateSpeakers();
		}

		/// <summary>
		/// Sets up the audio sources of this video player based on the given
		/// template. This includes location, relative volume, number of
		/// channels, and falloff properties.
		/// </summary>
		/// <param name="_sources">Array of template audio sources</param>
		/// <param name="_volume">Relative volume</param>
		/// <returns>True on success</returns>
		public bool _SetAudioTemplate(AudioSource[] _sources, float _volume) {
			if (_volume < 0) {
				LogError("Volume cannot be negative!");
				return false;
			}

			if (_sources == null || _sources.Length == 0) {
				LogError("Must contain audio sources for template!");
				return false;
			}

			int numSources = Mathf.Min(_sources.Length, speakers.Length);
			for (int i = 0; i < numSources; i++) {
				if (_sources[i] == null || speakers[i] == null) {
					LogError($"Speaker {i} is null!");
					continue;
				}
				speakers[i].bypassReverbZones = _sources[i].bypassReverbZones;
				speakers[i].dopplerLevel = _sources[i].dopplerLevel;
				speakers[i].loop = _sources[i].loop;
				speakers[i].maxDistance = _sources[i].maxDistance;
				speakers[i].minDistance = _sources[i].minDistance;
				speakers[i].panStereo = _sources[i].panStereo;
				speakers[i].pitch = _sources[i].pitch;
				speakers[i].priority = _sources[i].priority;
				speakers[i].reverbZoneMix = _sources[i].reverbZoneMix;
				speakers[i].rolloffMode = _sources[i].rolloffMode;
				speakers[i].spatialBlend = _sources[i].spatialBlend;
				speakers[i].spatialize = _sources[i].spatialize;
				speakers[i].spatializePostEffects = _sources[i].spatializePostEffects;
				speakers[i].spread = _sources[i].spread;
				speakers[i].velocityUpdateMode = _sources[i].velocityUpdateMode;
				Transform dstTransform = speakers[i].transform;
				Transform srcTransform = _sources[i].transform;
				dstTransform.position = srcTransform.position;
				dstTransform.rotation = srcTransform.rotation;
			}

			relativeVolume = _volume;
			speakersActive = _sources.Length;
			UpdateSpeakers();
			return true;
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
			relayPoint._RelayEvent(CallbackEvent.MediaLoading, identifier);
			return true;
		}

		private void Update() {
			if (!initialized) return;
			if (!videoSource.IsReady) return;
			if (!videoSource.IsPlaying) return;
			CheckTextureChange();
		}

		private void UpdateSpeakers() {
			if (!initialized) return;
			int numSpeakers = Mathf.Min(speakersActive, speakers.Length);
			Log($"Updating {numSpeakers}/{speakers.Length} speakers and muting the rest.");
			for (int i = 0; i < numSpeakers; i++) {
				speakers[i].volume = volume * relativeVolume;
				speakers[i].mute = muted;
			}
			for (int i = numSpeakers; i < speakers.Length; i++)
				speakers[i].mute = true;
		}

		// ReSharper disable Unity.PerformanceAnalysis
		private void CheckTextureChange() {
			Texture tempTexture = videoMaterial.GetTexture(videoTextureName);
			if (tempTexture != videoTextureCache) return;
			Log("New Video Texture!");
			videoTextureCache = tempTexture;
			relayPoint._RelayTextureChange(identifier, videoTextureCache);
		}

		private bool UninitializedLog(string _eventName) {
			if (initialized) return false;
			Log($"<color=#808080>(Uninitialized)</color> Event \"{_eventName}\" ignored on \"{gameObject.name}\"");
			return true;
		}

		protected override string DumpState() {
			if (!initialized) return "Uninitialized!";
			string dumpStr = $"Video Source: {videoSource}";
			if (videoRendererSource != null) dumpStr += $", Video Renderer: {videoRendererSource}, Material Index: {videoMaterialIndex}";
			dumpStr += $"\nVideo Material: {(videoMaterial == null ? "null" : videoMaterial.ToString())}, Texture Name: {videoTextureName}";
			dumpStr += $"\nVideo Type: {videoType}, Video Name: {videoName}, Automatic Resync: {AutomaticResync}\n";
			dumpStr += $"\nReady: {IsReady}, Playing: {IsPlaying}, Duration: {Duration}, Time: {Time}";
			int numSpeakers = speakers == null ? 0 : speakers.Length;
			dumpStr += $"Speakers: {(numSpeakers == 0 ? "None attached" : speakers[0] == null ? "Null" : speakers[0].name)}";
			for (int i = 1; i < numSpeakers; i++) dumpStr += $", {(speakers[i] == null ? "Null" : speakers[i].name)}";
			dumpStr += $"\nVolume: {volume}, Relative Volume: {relativeVolume}, Applied Volume: {Volume}, Muted: {muted}, Speakers Active: {speakersActive}";
			dumpStr += $"\nRelay Point: {(relayPoint == null ? "Null" : relayPoint.name)}, ID: {identifier}";
			return dumpStr;
		}

		public override void OnVideoEnd() {
			if (UninitializedLog("OnVideoEnd")) return;
			relayPoint._RelayEvent(CallbackEvent.MediaEnd, identifier);
			CheckTextureChange();
		}

		public override void OnVideoReady() {
			if (UninitializedLog("OnVideoReady")) return;
			relayPoint._RelayEvent(CallbackEvent.MediaReady, identifier);
			CheckTextureChange();
		}

		public override void OnVideoError(VideoError _videoError) {
			if (UninitializedLog("OnVideoError")) return;
			MediaError err = MediaError.Invalid;
			switch (_videoError) {
				case VideoError.RateLimited:
					err = MediaError.RateLimited;
					break;
				case VideoError.InvalidURL:
					err = MediaError.InvalidLink;
					break;
				case VideoError.AccessDenied:
					err = MediaError.UntrustedLink;
					break;
				case VideoError.PlayerError:
					err = MediaError.LoadingError;
					break;
				case VideoError.Unknown:
					err = MediaError.Unknown;
					break;
			}
			relayPoint._RelayError(identifier, err);
			CheckTextureChange();
		}

		public override void OnVideoPlay() {
			if (UninitializedLog("OnVideoPlay")) return;
			relayPoint._RelayEvent(CallbackEvent.MediaPlay, identifier);
			CheckTextureChange();
		}

		public override void OnVideoStart() {
			if (UninitializedLog("OnVideoStart")) return;
			relayPoint._RelayEvent(CallbackEvent.MediaStart, identifier);
			CheckTextureChange();
		}

		public override void OnVideoLoop() {
			if (UninitializedLog("OnVideoLoop")) return;
			relayPoint._RelayEvent(CallbackEvent.MediaLoop, identifier);
			CheckTextureChange();
		}

		public override void OnVideoPause() {
			if (UninitializedLog("OnVideoPause")) return;
			relayPoint._RelayEvent(CallbackEvent.MediaPause, identifier);
			CheckTextureChange();
		}
	}
}
