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
		[SerializeField] private SecurityManager securityManager;
		[SerializeField] private bool lockByDefault;
		private bool paused;
		[UdonSynced] private bool pausedSync;
		private int pauseTime;
		[UdonSynced] private int pauseTimeSync;
		private float beginTime;
		private int beginNetTime;
		[UdonSynced] private int beginNetTimeSync;
		private int mediaType;
		[UdonSynced] private int mediaTypeSync;
		private bool isLocked;
		[UdonSynced] private bool isLockedSync;

		private bool initialized;
		private UdonSharpBehaviour[] callbacks;

		public bool IsLocked {
			get {
				Initialize();
				return isLocked;
			}
			private set {
				if (isLocked == value) return;
				isLocked = value;
				Log((isLocked ? "Locking" : "Unlocking") + " the player");
				CallCallbacks(isLocked ? "_SecurityLocked" : "_SecurityUnlocked");
			}
		}

		public bool UnlockedOrHasAccess => !IsLocked || securityManager.HasAccess;

		private void Start() {
			Initialize();
		}

		private void Initialize() {
			if (initialized) return;
			isLocked = lockByDefault && securityManager.HasSecurity;
			initialized = true;
		}

		public void Play() {}

		public void Pause() {}

		public void Stop() {}

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

		private void CallCallbacks(string _message) {
			if (callbacks == null) return;
			Log("Calling callbacks with method " + _message);
			foreach (UdonSharpBehaviour callback in callbacks)
				callback.SendCustomEvent(_message);
		}
	}
}
