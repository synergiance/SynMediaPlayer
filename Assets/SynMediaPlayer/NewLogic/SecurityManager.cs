
using Synergiance.MediaPlayer.Diagnostics;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Synergiance.MediaPlayer {
	public class SecurityManager : DiagnosticBehaviour {
		[SerializeField] private string[] moderators;
		[SerializeField] private bool allowWorldMaster = true;
		[SerializeField] private bool allowWorldOwner = true;

		private UdonSharpBehaviour[] callbacks;

		private bool hasAccess;
		private bool hasSecurity;

		private bool initialized;

		/// <summary>
		/// Property telling whether the current user is able to manipulate the
		/// video player while locked.
		/// </summary>
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

		void Start() {
			Initialize();
		}

		private void Initialize() {
			if (initialized) return;
			hasSecurity = !((moderators == null || moderators.Length <= 0) && !allowWorldMaster && !allowWorldOwner);
			if (!hasSecurity) Log("No moderators, no owner and master control, security disengaged.");
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

		private void CallCallbacks(string _message) {
			if (callbacks == null) return;
			Log("Calling callbacks with method " + _message);
			foreach (UdonSharpBehaviour callback in callbacks)
				callback.SendCustomEvent(_message);
		}
	}
}
