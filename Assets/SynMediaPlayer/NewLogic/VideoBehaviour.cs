
using System;
using Synergiance.MediaPlayer.Diagnostics;
using Synergiance.MediaPlayer.Interfaces;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components.Video;

namespace Synergiance.MediaPlayer {
	public class VideoBehaviour : SMPMediaController {
		protected override string DebugName => "Video Behaviour";
		private SMPCallbackReceiver[] videoCallbacks;
		private int numVideoCallbacks;
		private const int CallbacksArrayIncrement = 16;
		private bool videoBaseInitialized;
		[SerializeField] protected SecurityManager securityManager;
		[HideInInspector] public MediaError lastError = MediaError.Unknown;

		private void InitializeVideoBase() {
			if (videoBaseInitialized) return;
			videoCallbacks = new SMPCallbackReceiver[CallbacksArrayIncrement];
			videoBaseInitialized = true;
		}

		private void ExpandVideoCallbacksArray() {
			int oldLength = videoCallbacks.Length;
			int newLength = oldLength + CallbacksArrayIncrement;
			SMPCallbackReceiver[] tmp = new SMPCallbackReceiver[newLength];
			Array.Copy(videoCallbacks, tmp, oldLength);
			videoCallbacks = tmp;
		}

		/// <summary>
		/// Registers a behaviour as a callback
		/// </summary>
		/// <param name="_callback">The behaviour to register</param>
		public void _RegisterVideoCallback(SMPCallbackReceiver _callback) {
			InitializeVideoBase();
			if (Array.IndexOf(videoCallbacks, _callback) >= 0) {
				LogWarning("Callback already exists in the callback array!", this);
				return;
			}
			if (numVideoCallbacks >= videoCallbacks.Length)
				ExpandVideoCallbacksArray();
			videoCallbacks[numVideoCallbacks++] = _callback;
		}

		public virtual void _SecurityUnlocked() { SendVideoCallback(CallbackEvent.PlayerUnlocked); }
		public virtual void _SecurityLocked() { SendVideoCallback(CallbackEvent.PlayerLocked); }
		public virtual void _GainedPrivileges() { SendVideoCallback(CallbackEvent.GainedPermissions); }

		public virtual void _RelayVideoLoading() { SendVideoCallback(CallbackEvent.MediaLoading); }
		public virtual void _RelayVideoReady() { SendVideoCallback(CallbackEvent.MediaReady); }
		public virtual void _RelayVideoError(MediaError _error) { SendErrorCallback(_error); }
		public virtual void _RelayVideoStart() { SendVideoCallback(CallbackEvent.MediaStart); }
		public virtual void _RelayVideoPlay() { SendVideoCallback(CallbackEvent.MediaPlay); }
		public virtual void _RelayVideoPause() { SendVideoCallback(CallbackEvent.MediaPause); }
		public virtual void _RelayVideoEnd() { SendVideoCallback(CallbackEvent.MediaEnd); }
		public virtual void _RelayVideoLoop() { SendVideoCallback(CallbackEvent.MediaLoop); }
		public virtual void _RelayVideoNext() { SendVideoCallback(CallbackEvent.MediaNext); }
		public virtual void _RelayVideoQueueLoading() { SendVideoCallback(CallbackEvent.QueueMediaLoading); }
		public virtual void _RelayVideoQueueReady() { SendVideoCallback(CallbackEvent.QueueMediaReady); }

		/// <summary>
		/// Sends a callback to all registered callbacks
		/// </summary>
		/// <param name="_event">Callback to send</param>
		protected void SendVideoCallback(CallbackEvent _event) {
			InitializeVideoBase();
			foreach (SMPCallbackReceiver videoCallback in videoCallbacks)
				videoCallback._SendCallback(_event);
		}

		/// <summary>
		/// Sends an error callback to all registered callbacks
		/// </summary>
		/// <param name="_error">Relevant error</param>
		protected void SendErrorCallback(MediaError _error) {
			lastError = _error;
			InitializeVideoBase();
			foreach (SMPCallbackReceiver videoCallback in videoCallbacks)
				videoCallback._SendError(_error);
		}

		public override void _SendCallback(CallbackEvent _event) {
			base._SendCallback(_event);
			SendVideoCallback(_event);
		}

		public override void _SendError(MediaError _err) {
			base._SendError(_err);
			SendErrorCallback(_err);
		}
	}
}
