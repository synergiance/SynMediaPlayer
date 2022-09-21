using System;
using Synergiance.MediaPlayer;
using UdonSharp;
using UnityEngine;
using VRC.Udon.Common;

namespace Synergiance.MediaPlayer {
	/// <summary>
	/// The type of media a video can be. This is currently unused, as enums are
	/// unsupported in UdonSharp as of writing this.
	/// </summary>
	public enum MediaType {
		Video, Stream, Music, MusicStream
	}

	/// <summary>
	/// Video player is the controlling class that makes videos play. Any video
	/// currently playing is attached to one of these. It keeps track of the
	/// time, whether its playing or paused, whether its a stream, locked, etc.
	/// It's the interface for where your play/pause calls go and will be the
	/// deciding factor on what to do with that information. This is written to
	/// run alongside other video player behaviours.
	/// </summary>
	[UdonBehaviourSyncMode(BehaviourSyncMode.Manual), DefaultExecutionOrder(-10)]
	public class VideoPlayer : VideoBehaviour {
		[SerializeField] private bool lockByDefault;
		[SerializeField] private PlayerManager playerManager;
		[SerializeField] private VideoQueue queue;
		[SerializeField] private string playerName = "Video Player";
		[Range(0, 1)] [SerializeField] private float volume = 0.55f;
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
		private int syncIndex = -1;
		[UdonSynced] private int syncIndexSync;
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
				if (isLocked == isLockedSync == value) return;
				// TODO: Permission check and sync
				isLocked = value;
				Log($"{(isLocked ? "Locking" : "Unlocking")} the player");
				//CallCallbacks(isLocked ? "_SecurityLocked" : "_SecurityUnlocked");
				Sync();
			}
		}

		public bool Paused {
			get => paused;
			private set {
				if (paused == pausedSync == value) return;
				if (!CheckValidAndAccess(value ? "pause" : "unpause")) return;
				pausedSync = value;
				Log((value ? "Pausing" : "Playing") + " the video");
				Sync();
			}
		}

		private float PauseTime {
			get => pauseTime;
			set {
				if (Mathf.Abs(value - pauseTime) + Mathf.Abs(value - pauseTimeSync) < 0.1) return;
				pauseTimeSync = value;
				Log("Setting paused time to " + value);
				Sync();
			}
		}

		private int BeginNetTime {
			get => beginNetTime;
			set {
				if (beginNetTimeSync == value && beginNetTime == value) return;
				beginNetTimeSync = value;
				Log("Setting begin net time to " + value);
				Sync();
			}
		}

		private int MediaType {
			get => mediaType;
			set {
				if (mediaType == value && mediaTypeSync == value) return;
				mediaTypeSync = value;
				Log("Setting media type to: " + value);
				Sync();
			}
		}

		private int SyncIndex {
			get => syncIndex;
			set {
				if (syncIndex == value && syncIndexSync == value) return;
				syncIndexSync = value;
				Log("Updating sync index to: " + value);
				Sync();
			}
		}

		public bool UnlockedOrHasAccess => !IsLocked || securityManager.HasAccess;
		public float CurrentTime => paused ? pauseTime : Time.time - beginTime;
		public bool Playing => !paused;
		public float VideoLength => GetVideoLength();
		public float Volume => volume;

		private void Start() {
			Initialize();
		}

		private void Initialize() {
			if (initialized) return;
			Log("Initialize!");
			CheckValid();
			initialized = true;
		}

		private void CheckValid() {
			if (playerManager == null) {
				LogError("Player manager is missing!");
				return;
			}

			identifier = playerManager._RegisterVideoPlayer(this, playerName);

			if (identifier < 0) {
				LogError("Failed to register video player!");
				return;
			}

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

			Log("Successfully validated!");

			isLocked = lockByDefault && securityManager.HasSecurity;
			isValid = true;
		}

		public void _Play() {
			if (!CheckValidAndAccess("play")) return;

			Log("Playing");
			Paused = false;
			playerManager._PlayVideo(identifier);
		}

		public void _Pause() {
			if (!CheckValidAndAccess("pause")) return;

			Log("Pausing");
			Paused = true;
			playerManager._PauseVideo(identifier);
		}

		public void _Stop() {
			if (!CheckValidAndAccess("stop")) return;

			Log("Stopping");
			Paused = true;
			// TODO: Set time to 0
			playerManager._StopVideo(identifier);
		}

		private bool CheckValidAndAccess(string _action) {
			if (!isValid) {
				LogError($"Cannot {_action}, invalid!");
				return false;
			}

			if (!UnlockedOrHasAccess) {
				LogWarning($"Cannot {_action}, not allowed to interact with video player!");
				return false;
			}

			return true;
		}

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

		private void PlayInternal() {
			//
		}

		private void PauseInternal() {
			//
		}

		private void StopInternal() {
			//
		}

		#region Sync
		private void Sync() {
			Log("Sync!");
			if (IsEditor) {
				return;
			}
		}

		public override void OnDeserialization() {
			Log("On Deserialization");
			ApplySyncData();
		}

		public override void OnPostSerialization(SerializationResult _result) {
			if (!_result.success) {
				LogWarning("Failed to serialize, byte count:" + _result.byteCount);
				return;
			}
			Log($"Successfully serialized {_result.byteCount} bytes");
			ApplySyncData();
		}

		private void ApplySyncData() {
			CheckChanges(out bool cPaused, out bool cBeginNetTime,
				out bool cPauseTime, out bool cLocked, out bool cMediaType);
			paused = pausedSync;
			beginNetTime = beginNetTimeSync;
			pauseTime = pauseTimeSync;
			isLocked = isLockedSync;
			mediaType = mediaTypeSync;
			InterpretChanges(cPaused, cBeginNetTime, cPauseTime, cLocked, cMediaType);
		}

		private void CheckChanges(out bool _cPaused, out bool _cBeginNetTime,
			out bool _cPauseTime, out bool _cLocked, out bool _cMediaType) {
			_cPaused = paused != pausedSync;
			_cBeginNetTime = beginNetTime != beginNetTimeSync;
			_cPauseTime = Math.Abs(pauseTime - pauseTimeSync) > 0.1f;
			_cLocked = isLocked != isLockedSync;
			_cMediaType = mediaType != mediaTypeSync;
		}
		#endregion

		private void InterpretChanges(bool _cPaused, bool _cBeginNetTime,
			bool _cPauseTime, bool _cLocked, bool _cMediaType) {
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
