
using UdonSharp;
using VRC.SDKBase;

namespace Synergiance.MediaPlayer {
	[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
	public class UserPlaylist : UdonSharpBehaviour {
		[UdonSynced] private string playlistName;
		[UdonSynced] private string[] videoNames;
		[UdonSynced] private VRCUrl[] videoLinks;
		[UdonSynced] private int numVideos;
		[UdonSynced] private int[] videoOffsets;
		[UdonSynced] private int[] videoLengths;

		public bool _BindPlaylist(string _name) {
			//
			return false;
		}
	}
}
