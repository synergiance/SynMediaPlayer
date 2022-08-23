using System;
using Synergiance.MediaPlayer.Diagnostics;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Synergiance.MediaPlayer {
	[Serializable]
	public struct Video {
		public string name;
		public VRCUrl link;
		public string shortName;
	}

	[Serializable]
	public struct Playlist {
		public string name;
		public Video[] videos;
	}

	[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
	public class PlaylistManager : DiagnosticBehaviour {
		[SerializeField] private Playlist[] playlists; // Currently just a helper for the UI
		[SerializeField] private string playlistBackup; // Saves playlist backup name

		[SerializeField] private int[] playlistOffsets;
		[SerializeField] private string[] playlistNames;
		[SerializeField] private string[] videoShortNames;
		[SerializeField] private string[] videoNames;
		[SerializeField] private VRCUrl[] videoLinks;

		[UdonSynced] private string[] userPlaylistNames;
		[UdonSynced] private string[] userVideoNames;
		[UdonSynced] private VRCUrl[] userVideoLinks;
		[UdonSynced] private int[] userPlaylistOffsets;
		[UdonSynced] private int[] userPlaylistLengths;
		[UdonSynced] private int numUserPlaylists;

		private bool initialized;
		private bool playlistsValid;

		private const int ArrayIncrement = 16;

		#if !COMPILER_UDONSHARP && UNITY_EDITOR
		public void RebuildSerialized() {
			int numPlaylists = playlists?.Length ?? 0;
			playlistOffsets = new int[numPlaylists];
			playlistNames = new string[numPlaylists];
			videoShortNames = Array.Empty<string>();
			videoNames = Array.Empty<string>();
			videoLinks = Array.Empty<VRCUrl>();
			int dumpIndex = 0;
			for (int playlistIndex = 0; playlistIndex < numPlaylists; playlistIndex++) {
				playlistOffsets[playlistIndex] = dumpIndex;
				// ReSharper disable once PossibleNullReferenceException
				Playlist playlist = playlists[playlistIndex];
				playlistNames[playlistIndex] = playlist.name;
				int numVideos = playlist.videos?.Length ?? 0;
				if (numVideos <= 0) continue;
				Array.Resize(ref videoShortNames, videoShortNames.Length + numVideos);
				Array.Resize(ref videoNames, videoNames.Length + numVideos);
				Array.Resize(ref videoLinks, videoLinks.Length + numVideos);
				for (int videoIndex = 0; videoIndex < numVideos; videoIndex++) {
					// ReSharper disable once PossibleNullReferenceException
					Video video = playlist.videos[videoIndex];
					videoShortNames[dumpIndex] = video.shortName;
					videoNames[dumpIndex] = video.name;
					videoLinks[dumpIndex] = video.link;
					dumpIndex++;
				}
			}
		}

		public bool LoadSerialized(string _path) {
			// TODO: Implement
			return true;
		}

		public bool Serialize(string _path) {
			// TODO: Implement
			return true;
		}
		#endif

		void Start() {
			Initialize();
		}

		private void Initialize() {
			if (initialized) return;
			CheckPlaylists();
			InitializeUserPlaylists();
			initialized = true;
		}

		private void CheckPlaylists() {
			Log("Checking playlist integrity");
			if (playlistNames == null
			    || videoLinks == null
			    || videoNames == null
			    || videoShortNames == null
			    || playlistOffsets == null) {
				LogError("No playlists or playlists are broken! Disabling playlists.");
				return;
			}

			if (playlistNames.Length != playlistOffsets.Length
			    || videoNames.Length != videoLinks.Length
			    || videoNames.Length != videoShortNames.Length
			    || playlistOffsets[0] != 0) {
				LogError("Playlists are broken! Disabling playlists.");
				return;
			}

			int lastOffset = 0;
			foreach (int offset in playlistOffsets) {
				if (offset > videoNames.Length || offset < lastOffset) {
					LogError("Playlists are broken! Disabling playlists.");
					return;
				}
				lastOffset = offset;
			}

			int currentPlaylist = 0;
			for (int i = 0; i < videoNames.Length; i++) {
				while (currentPlaylist + 1 < playlistOffsets.Length && i >= playlistOffsets[currentPlaylist + 1]) currentPlaylist++;
				if (videoLinks[i] == null) videoLinks[i] = VRCUrl.Empty;
				bool missingLink = String.IsNullOrWhiteSpace(videoLinks[i].ToString());
				int videoIndex = i - playlistOffsets[currentPlaylist];
				if (missingLink) LogWarning("Video " + videoIndex + " in playlist " + currentPlaylist + " is missing a link!");
				bool missingName = string.IsNullOrWhiteSpace(videoNames[i]);
				bool missingShortName = string.IsNullOrWhiteSpace(videoShortNames[i]);
				if (missingName && missingShortName) {
					videoNames[i] = videoShortNames[i] = missingLink ? "<Link missing!>" : videoLinks[i].ToString();
				} else if (missingName) {
					videoNames[i] = videoShortNames[i];
				} else if (missingShortName) {
					videoShortNames[i] = videoNames[i];
				}
			}

			playlistsValid = true;
		}

		private void InitializeUserPlaylists() {
			numUserPlaylists = 0;
			userPlaylistNames = new string[ArrayIncrement];
			userPlaylistLengths = new int[ArrayIncrement];
			userPlaylistOffsets = new int[ArrayIncrement];
			userVideoLinks = new VRCUrl[ArrayIncrement];
			userVideoNames = new string[ArrayIncrement];
		}
	}
}
