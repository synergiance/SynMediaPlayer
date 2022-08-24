using Synergiance.MediaPlayer;
using UdonSharp;

namespace Synergiance.MediaPlayer {
	public enum MediaType {
		Video, Stream, LowLatencyStream, Music, MusicStream
	}
	public class VideoPlayer : VideoBehaviour {
		private bool paused;
		[UdonSynced] private bool pausedSync;
		private int pauseTime;
		[UdonSynced] private int pauseTimeSync;
		private float beginTime;
		private int beginNetTime;
		[UdonSynced] private int beginNetTimeSync;
		private int mediaType;
		[UdonSynced] private int mediaTypeSync;
		void Start() {}

		public void Play() {}

		public void Pause() {}

		public void Stop() {}
	}
}
