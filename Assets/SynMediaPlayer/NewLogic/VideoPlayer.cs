using Synergiance.MediaPlayer;
using UdonSharp;
using UnityEngine;

namespace Synergiance.MediaPlayer {
	/// <summary>
	/// The type of media a video can be. This is currently unused, as enums are
	/// unsupported in UdonSharp as of writing this.
	/// </summary>
	public enum MediaType {
		Video, Stream, LowLatencyStream, Music, MusicStream
	}

	/// <summary>
	/// Video player is the controlling class that makes videos play. Any video
	/// currently playing is attached to one of these. It keeps track of the
	/// time, whether its playing or paused, whether its a stream, locked, etc.
	/// It's the interface for where your play/pause calls go and will be the
	/// deciding factor on what to do with that information. This is written to
	/// run alongside other video player behaviours.
	/// </summary>
	public class VideoPlayer : VideoBehaviour {
		[SerializeField] private bool lockByDefault;
		[SerializeField] private PlayerManager playerManager;
		[SerializeField] private VideoQueue queue;
		[SerializeField] private string playerName = "Video Player";
		private PlaylistManager playlistManager;
		private VideoManager videoManager;
		private bool paused;
		[UdonSynced] private bool pausedSync;
		private float pauseTime;
		[UdonSynced] private float pauseTimeSync;
		private float beginTime;
		private int beginNetTime;
		[UdonSynced] private int beginNetTimeSync;
		private int mediaType;
		[UdonSynced] private int mediaTypeSync;
		private bool isLocked;
		[UdonSynced] private bool isLockedSync;

		private bool initialized;
		private bool isValid;
		private UdonSharpBehaviour[] callbacks;
		private int identifier;

		protected override string DebugName => "Video Player";
		protected override string DebugColor => ColorToHtmlStringRGB(new Color(0.25f, 0.65f, 0.1f));

		/// <summary>
		/// Public interface for determining whether video player is locked or not.
		/// Hides the internal mechanism for locking and unlocking the video player.
		/// </summary>
		public bool IsLocked {
			get {
				Initialize();
				return !isValid || isLocked;
			}
			private set {
				if (isLocked == value) return;
				isLocked = value;
				Log($"{(isLocked ? "Locking" : "Unlocking")} the player");
				CallCallbacks(isLocked ? "_SecurityLocked" : "_SecurityUnlocked");
			}
		}

		public bool UnlockedOrHasAccess => !IsLocked || securityManager.HasAccess;
		public float CurrentTime => paused ? pauseTime : Time.time - beginTime;
		public bool Playing => !paused;
		public float VideoLength => GetVideoLength();

		private void Start() {
			Initialize();
		}

		private void Initialize() {
			if (initialized) return;
			CheckValid();
			initialized = true;
		}

		private void CheckValid() {
			if (playerManager == null) {
				LogError("Player manager is missing!");
				return;
			}

			identifier = playerManager._RegisterVideoPlayer(this, playerName);

			videoManager = playerManager.GetVideoManager();
			if (videoManager == null) {
				LogError("Video manager is missing!");
				return;
			}

			playlistManager = playerManager.GetPlaylistManager();
			if (playlistManager == null) {
				LogError("Playlist manager is missing!");
				return;
			}

			if (queue == null) {
				LogError("Queue is missing!");
				return;
			}

			isLocked = lockByDefault && securityManager.HasSecurity;
			isValid = true;
		}

		public void _Play() {}

		public void _Pause() {}

		public void _Stop() {}

		public void _Lock() {
			if (!securityManager.HasSecurity) {
				Log("No security, cannot lock!");
				return;
			}
			if (isLocked) {
				Log("Already locked!");
				return;
			}
			if (!securityManager.HasAccess) {
				LogWarning("You don't have access to lock the player!");
				return;
			}
			IsLocked = true;
		}

		public void _Unlock() {
			if (!securityManager.HasSecurity) {
				Log("No security, already unlocked!");
				return;
			}
			if (!isLocked) {
				Log("Already unlocked!");
				return;
			}
			if (!securityManager.HasAccess) {
				LogWarning("You don't have access to unlock the player!");
				return;
			}
			IsLocked = false;
		}

		private float GetVideoLength() {
			Initialize();
			if (!isValid) return -1;
			// TODO: Implementation
			// Determine whether a video is loaded
			//
			return -1;
		}

		private void Sync() {
			//
		}

		private void CallCallbacks(string _message) {
			if (callbacks == null) return;
			Log($"Calling callbacks with method \"{_message}\"");
			foreach (UdonSharpBehaviour callback in callbacks)
				callback.SendCustomEvent(_message);
		}
	}
}
