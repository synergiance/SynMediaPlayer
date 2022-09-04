using Synergiance.MediaPlayer.Diagnostics;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Synergiance.MediaPlayer {
	/// <summary>
	/// The video queue behaviour maintains the list of videos a video player
	/// has queued up. It can add new videos to the end of the queue, and in
	/// the future will be able to insert videos anywhere rearrange, and also
	/// remove them. It can recite what video is currently playing, as well as
	/// the next video, and any future video in the queue. If prompted it will
	/// tell you how many videos are remaining in the queue.
	/// </summary>
	[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
	public class VideoQueue : DiagnosticBehaviour {
		[SerializeField] private int maxQueueLength = 64;
		[UdonSynced] private VRCUrl[] queuedVideos; // This will be a ring buffer
		[UdonSynced] private int currentIndexSync;
		[UdonSynced] private int videosInQueueSync;
		private int currentIndex;
		private int videosInQueue;

		private bool initialized;

		/// <summary>
		/// The video that is currently playing
		/// </summary>
		public VRCUrl CurrentVideo => _GetVideoAtIndex(0);
		/// <summary>
		/// The video that will play next.
		/// </summary>
		public VRCUrl NextVideo => _GetVideoAtIndex(1);
		/// <summary>
		/// The number of videos in the queue
		/// </summary>
		public int VideosInQueue => videosInQueue;

		protected override string DebugName => "Video Queue";
		protected override Color DebugColor => new Color(0.7f, 0.65f, 0.15f);

		private void Start() {
			Initialize();
		}

		private void Initialize() {
			if (initialized) return;
			if (maxQueueLength < 4) {
				Debug.LogWarning($"Max queue length ({maxQueueLength}) is too low! Increasing to 4.");
				maxQueueLength = 4;
			}
			Log($"Initializing ring buffer of length {maxQueueLength}");
			queuedVideos = new VRCUrl[maxQueueLength];
			initialized = true;
		}

		/// <summary>
		/// Internal method for getting the pointer to the data at a given index
		/// </summary>
		/// <param name="_index">Index of the requested queue item.</param>
		/// <returns>The pointer to the data.</returns>
		private int CalcRingIndex(int _index) {
			return (_index + currentIndex) % maxQueueLength;
		}

		/// <summary>
		/// Returns the video at a given index.
		/// </summary>
		/// <param name="_index">Index of the requested queue item.</param>
		/// <returns>The link to the video at the given index.</returns>
		public VRCUrl _GetVideoAtIndex(int _index) {
			Initialize();
			return queuedVideos[CalcRingIndex(_index)];
		}

		/// <summary>
		/// Advances the queue by 1 item.
		/// </summary>
		public void _AdvanceQueue() {
			if (videosInQueue <= 0) {
				LogError("Queue empty, cannot advance queue!");
				return;
			}
			currentIndex = (currentIndex + 1) % maxQueueLength;
			videosInQueue = Mathf.Max(videosInQueue - 1, 0);
		}

		/// <summary>
		/// Adds a video to the end of the queue
		/// </summary>
		/// <param name="_link">The link to the video that will be appended to
		/// the queue</param>
		/// <returns>True if successful, false otherwise.</returns>
		public bool _AddVideo(VRCUrl _link) {
			if (videosInQueue >= maxQueueLength) {
				LogError("Cannot insert into queue, max queue length reached!");
				return false;
			}
			Log($"Adding to queue: {_link}");
			queuedVideos[CalcRingIndex(videosInQueue++)] = _link;
			return true;
		}
	}
}
