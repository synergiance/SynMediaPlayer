
using System;
using Synergiance.MediaPlayer.Interfaces;
using UnityEngine;

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
