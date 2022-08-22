
using System;
using Synergiance.MediaPlayer.Diagnostics;
using UdonSharp;

namespace Synergiance.MediaPlayer {
	public class VideoBehaviour : DiagnosticBehaviour {
		protected override string DebugName => "Video Behaviour";
		private UdonSharpBehaviour[] videoCallbacks;
		private int numVideoCallbacks;
		private const int CallbacksArrayIncrement = 16;
		private bool videoBaseInitialized;

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

		public void RegisterVideoCallback(UdonSharpBehaviour _callback) {
			InitializeVideoBase();
			if (Array.IndexOf(videoCallbacks, _callback) >= 0) {
				LogWarning("Callback already exists in the callback array!", this);
				return;
			}
			if (numVideoCallbacks >= videoCallbacks.Length)
				ExpandVideoCallbacksArray();
			videoCallbacks[numVideoCallbacks++] = _callback;
		}

		public virtual void _RelayVideoLoading() { SendVideoCallback("_RelayVideoLoading"); }
		public virtual void _RelayVideoReady() { SendVideoCallback("_RelayVideoLoading"); }
		public virtual void _RelayVideoError() { SendVideoCallback("_RelayVideoLoading"); }
		public virtual void _RelayVideoStart() { SendVideoCallback("_RelayVideoLoading"); }
		public virtual void _RelayVideoPlay() { SendVideoCallback("_RelayVideoLoading"); }
		public virtual void _RelayVideoPause() { SendVideoCallback("_RelayVideoLoading"); }
		public virtual void _RelayVideoEnd() { SendVideoCallback("_RelayVideoLoading"); }
		public virtual void _RelayVideoLoop() { SendVideoCallback("_RelayVideoLoading"); }
		public virtual void _RelayVideoNext() { SendVideoCallback("_RelayVideoLoading"); }
		public virtual void _RelayVideoQueueLoading() { SendVideoCallback("_RelayVideoLoading"); }
		public virtual void _RelayVideoQueueReady() { SendVideoCallback("_RelayVideoLoading"); }
		public virtual void _RelayVideoQueueError() { SendVideoCallback("_RelayVideoLoading"); }

		protected void SendVideoCallback(string _callbackName) {
			InitializeVideoBase();
			foreach (UdonSharpBehaviour videoCallback in videoCallbacks)
				videoCallback.SendCustomEvent(_callbackName);
		}
	}
}
