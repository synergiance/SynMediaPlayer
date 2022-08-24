using Synergiance.MediaPlayer.Diagnostics;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Synergiance.MediaPlayer {
	[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
	public class VideoQueue : DiagnosticBehaviour {
		[SerializeField] private int maxQueueLength = 64;
		[UdonSynced] private VRCUrl[] queuedVideos; // This will be a ring buffer
		[UdonSynced] private int currentIndexSync;
		[UdonSynced] private int videosInQueueSync;
		private int currentIndex;
		private int videosInQueue;

		private bool initialized;

		public VRCUrl CurrentVideo => _GetVideoAtIndex(0);
		public VRCUrl NextVideo => _GetVideoAtIndex(1);
		public int VideosInQueue => videosInQueue;

		protected override string DebugName => "Video Queue";
		protected override string DebugColor => "#A08040";

		private void Start() {
			Initialize();
		}

		private void Initialize() {
			if (initialized) return;
			if (maxQueueLength < 4) {
				Debug.LogWarning("Max queue length (" + maxQueueLength + ") is too low! Increasing to 4.");
				maxQueueLength = 4;
			}
			Log("Initializing ring buffer of length " + maxQueueLength);
			queuedVideos = new VRCUrl[maxQueueLength];
			initialized = true;
		}

		private int CalcRingIndex(int _index) {
			return (_index + currentIndex) % maxQueueLength;
		}

		public VRCUrl _GetVideoAtIndex(int _index) {
			Initialize();
			return queuedVideos[CalcRingIndex(_index)];
		}

		public void _AdvanceQueue() {
			if (videosInQueue <= 0) {
				LogError("Queue empty, cannot advance queue!");
				return;
			}
			currentIndex = (currentIndex + 1) % maxQueueLength;
			videosInQueue = Mathf.Max(videosInQueue - 1, 0);
		}

		public bool _AddVideo(VRCUrl _link) {
			if (videosInQueue >= maxQueueLength) {
				LogError("Cannot insert into queue, max queue length reached!");
				return false;
			}
			Log("Adding to queue: " + _link);
			queuedVideos[CalcRingIndex(videosInQueue++)] = _link;
			return true;
		}
	}
}
