
using Synergiance.MediaPlayer.Diagnostics;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Synergiance.MediaPlayer {
	public class SecurityManager : DiagnosticBehaviour {
		[SerializeField] private string[] moderators;
		[SerializeField] private bool allowWorldMaster = true;
		[SerializeField] private bool allowWorldOwner = true;
		[SerializeField] private bool lockByDefault;

		private UdonSharpBehaviour[] callbacks;
		private bool isLocked;
		[UdonSynced] private bool isLockedSync;

		private bool hasAccess;
		private bool hasSecurity;

		private bool initialized;

		public bool HasAccess {
			get {
				if (hasAccess) return true;
				VRCPlayerApi localPlayer = Networking.LocalPlayer;
				if (localPlayer == null) {
					hasAccess = true;
					return true;
				}
				hasAccess = TestAccess(localPlayer);
				if (hasAccess) CallCallbacks("_GainedPrivileges");
				return hasAccess;
			}
		}

		public bool HasSecurity {
			get {
				Initialize();
				return hasSecurity;
			}
		}

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

		public bool UnlockedOrHasAccess => !IsLocked || HasAccess;

		void Start() {
			Initialize();
		}

		private void Initialize() {
			if (initialized) return;
			hasSecurity = !((moderators == null || moderators.Length <= 0) && !allowWorldMaster && !allowWorldOwner);
			if (!hasSecurity) Log("No moderators, no owner and master control, security disengaged.");
			isLocked = lockByDefault && hasSecurity;
			if (hasSecurity) hasAccess = true;
			initialized = true;
		}

		private bool TestAccess(VRCPlayerApi _player) {
			Log("Testing access");
			if ((_player.isMaster && allowWorldMaster) || (_player.isInstanceOwner && allowWorldOwner)) {
				Log("Is master or owner");
				return true;
			}
			if (moderators == null) {
				Log("No moderators");
				return false;
			}
			string playerName = _player.displayName;
			foreach (string moderator in moderators) {
				if (string.Equals(playerName, moderator)) {
					Log("Is moderator");
					return true;
				}
			}
			Log("Has no access");
			return false;
		}

		public void _Lock() {
			if (!hasSecurity) {
				Log("No security, cannot lock!");
				return;
			}
			if (isLocked) {
				Log("Already locked!");
				return;
			}
			if (!HasAccess) {
				LogWarning("You don't have access to lock the player!");
				return;
			}
			IsLocked = true;
		}

		public void _Unlock() {
			if (!hasSecurity) {
				Log("No security, already unlocked!");
				return;
			}
			if (!isLocked) {
				Log("Already unlocked!");
				return;
			}
			if (!HasAccess) {
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
