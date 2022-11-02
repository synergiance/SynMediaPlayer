
using System;
using Synergiance.MediaPlayer.Diagnostics;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components.Video;

namespace Synergiance.MediaPlayer {
	public enum CallbackEvent {
		MediaLoading, MediaReady, MediaStart, MediaEnd, MediaNext, MediaLoop, MediaPlay, MediaPause, QueueMediaLoading,
		QueueMediaReady, PlayerLocked, PlayerUnlocked, PlayerInitialized, GainedPermissions, PlayerError
	}

	public enum MediaError {
		RateLimited, UntrustedLink, UntrustedQueueLink, InvalidLink, InvalidQueueLink, LoadingError, LoadingErrorQueue, Unknown
	}

	public class VideoBehaviour : DiagnosticBehaviour {
		protected override string DebugName => "Video Behaviour";
		private UdonSharpBehaviour[] videoCallbacks;
		private int numVideoCallbacks;
		private const int CallbacksArrayIncrement = 16;
		private bool videoBaseInitialized;
		[SerializeField] protected SecurityManager securityManager;
		[HideInInspector] public MediaError lastError = MediaError.Unknown;

		private void InitializeVideoBase() {
			if (videoBaseInitialized) return;
			videoCallbacks = new UdonSharpBehaviour[CallbacksArrayIncrement];
			videoBaseInitialized = true;
		}

		private void ExpandVideoCallbacksArray() {
			int oldLength = videoCallbacks.Length;
			int newLength = oldLength + CallbacksArrayIncrement;
			UdonSharpBehaviour[] tmp = new UdonSharpBehaviour[newLength];
			Array.Copy(videoCallbacks, tmp, oldLength);
			videoCallbacks = tmp;
		}

		/// <summary>
		/// Registers a behaviour as a callback
		/// </summary>
		/// <param name="_callback">The behaviour to register</param>
		public void _RegisterVideoCallback(UdonSharpBehaviour _callback) {
			InitializeVideoBase();
			if (Array.IndexOf(videoCallbacks, _callback) >= 0) {
				LogWarning("Callback already exists in the callback array!", this);
				return;
			}
			if (numVideoCallbacks >= videoCallbacks.Length)
				ExpandVideoCallbacksArray();
			videoCallbacks[numVideoCallbacks++] = _callback;
		}

		public virtual void _SecurityUnlocked() {}
		public virtual void _SecurityLocked() {}
		public virtual void _GainedPrivileges() {}

		public virtual void _RelayVideoLoading() { SendVideoCallback("_RelayVideoLoading"); }
		public virtual void _RelayVideoReady() { SendVideoCallback("_RelayVideoReady"); }
		public virtual void _RelayVideoError(VideoError _error) { SendErrorCallback(_error); }
		public virtual void _RelayVideoStart() { SendVideoCallback("_RelayVideoStart"); }
		public virtual void _RelayVideoPlay() { SendVideoCallback("_RelayVideoPlay"); }
		public virtual void _RelayVideoPause() { SendVideoCallback("_RelayVideoPause"); }
		public virtual void _RelayVideoEnd() { SendVideoCallback("_RelayVideoEnd"); }
		public virtual void _RelayVideoLoop() { SendVideoCallback("_RelayVideoLoop"); }
		public virtual void _RelayVideoNext() { SendVideoCallback("_RelayVideoNext"); }
		public virtual void _RelayVideoQueueLoading() { SendVideoCallback("_RelayVideoQueueLoading"); }
		public virtual void _RelayVideoQueueReady() { SendVideoCallback("_RelayVideoQueueReady"); }
		public virtual void _RelayVideoQueueError() { SendVideoCallback("_RelayVideoQueueError"); }

		/// <summary>
		/// Sends a callback to all registered callbacks
		/// </summary>
		/// <param name="_callbackName">Name of the callback to call</param>
		protected void SendVideoCallback(string _callbackName) {
			InitializeVideoBase();
			foreach (UdonSharpBehaviour videoCallback in videoCallbacks)
				videoCallback.SendCustomEvent(_callbackName);
		}

		/// <summary>
		/// Sends an error callback to all registered callbacks
		/// </summary>
		/// <param name="_error">Relevant error</param>
		protected void SendErrorCallback(VideoError _error) {
			InitializeVideoBase();
			foreach (UdonSharpBehaviour videoCallback in videoCallbacks) {
				videoCallback.SetProgramVariable("relayVideoError", _error);
				videoCallback.SendCustomEvent("_RelayVideoError");
			}
		}
	}
}
