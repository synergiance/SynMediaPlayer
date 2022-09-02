using Synergiance.MediaPlayer.Diagnostics;
using UnityEngine;

namespace Synergiance.MediaPlayer {
	public enum VideoTypes {
		Video, Stream, LowLatency
	}
	public class VideoManager : DiagnosticBehaviour {
		[SerializeField] private VideoRelay[] relays;
		private string[] videoNames;
		private int[] playerHandles;
		private int[] secondaryHandles;
		private int[] relayHandles;

		private bool initialized;
		private bool isValid;

		void Start() {
			Initialize();
		}

		private void Initialize() {
			if (initialized) return;
			InitialSetUp();
			initialized = true;
		}

		private void InitialSetUp() {
			if (relays == null || relays.Length < 1) {
				LogError("No relays set!");
				return;
			}

			videoNames = new string[relays.Length];
			playerHandles = new int[relays.Length];
			secondaryHandles = new int[relays.Length];
			relayHandles = new int[relays.Length];

			for (int i = 0; i < relays.Length; i++) {
				videoNames[i] = relays[i].InitializeRelay(this, i);
				secondaryHandles[i] = relayHandles[i] = playerHandles[i] = -1;
				if (videoNames[i] == null)
					LogWarning("Video player " + i + " isn't initialized!");
				else
					Log("Video player " + i + " (" + videoNames[i] + ") is now initialized!");
			}

			isValid = true;
		}

		/// <summary>
		/// Interface for getting a handle to control a video. Will not do anything
		/// until a successful request is made.
		/// </summary>
		/// <param name="_videoType">The type of video that will be playing (0 for
		/// video, 1 for stream, 2 for low latency)</param>
		/// <returns>The handle for performing actions on a video.</returns>
		public int _RequestVideoPlayer(int _videoType) {
			// cycle through relay handles to see what's open and whether it suits the needs
			return -1;
		}

		public bool _RebindVideoHandle(int _handle, int _videoType) {
			return true;
		}
	}
}
