using System;
using Synergiance.MediaPlayer.Diagnostics;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Synergiance.MediaPlayer {
	[Serializable]
	public struct Video {
		public string name;
		public string link;
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
		[SerializeField] private int[] playlistLengths;
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
		public void RebuildSerialized(bool _fullRebuild) {
			// We will make numPlaylists 0 if it's null, which will prevent null data access
			int numPlaylists = playlists?.Length ?? 0;
			// Only make new arrays if we need a full rebuild (array sizes changing)
			if (_fullRebuild) {
				playlistOffsets = new int[numPlaylists];
				playlistLengths = new int[numPlaylists];
				playlistNames = new string[numPlaylists];
				videoShortNames = Array.Empty<string>();
				videoNames = Array.Empty<string>();
				videoLinks = Array.Empty<VRCUrl>();
			}
			int dumpIndex = 0; // Iterator for non structured data
			for (int playlistIndex = 0; playlistIndex < numPlaylists; playlistIndex++) {
				// The playlist offset will be the index of its first video in the unstructured data
				playlistOffsets[playlistIndex] = dumpIndex;
				// ReSharper disable once PossibleNullReferenceException
				Playlist playlist = playlists[playlistIndex];
				playlistNames[playlistIndex] = playlist.name;
				// We will make numVideos 0 if it's null, which will prevent null data access
				int numVideos = playlist.videos?.Length ?? 0;
				// I considered leaving playlist length to be determined where needed,
				// but having that overhead in the udon behaviour is undesired
				playlistLengths[playlistIndex] = numVideos;
				if (numVideos <= 0) continue; // Cancel array resize, with free lower bound check
				if (_fullRebuild) {
					// Add playlist length on to each array
					Array.Resize(ref videoShortNames, videoShortNames.Length + numVideos);
					Array.Resize(ref videoNames, videoNames.Length + numVideos);
					Array.Resize(ref videoLinks, videoLinks.Length + numVideos);
				}
				for (int videoIndex = 0; videoIndex < numVideos; videoIndex++) {
					// ReSharper disable once PossibleNullReferenceException
					Video video = playlist.videos[videoIndex];
					videoShortNames[dumpIndex] = video.shortName;
					videoNames[dumpIndex] = video.name;
					// Creating new VRCUrl objects is legal outside of Udon, so doing it here
					videoLinks[dumpIndex] = new VRCUrl(video.link);
					dumpIndex++;
				}
			}
		}

		// This is dead code but it could come in handy in the future.
		// Seeing as it will be ignored by Udon, its harmless
		public void DumpContents() {
			string str = "Playlists:\n";
			foreach (Playlist playlist in playlists) {
				str += "Playlist: " + playlist.name + "\n";
				foreach (Video video in playlist.videos) {
					str += "Video Title: " + video.name + "\nVideo Short Name: ";
					str += video.shortName + "\nVideo Link: " + video.link + "\n";
				}
			}
			str += "Videos:\n";
			for (int i = 0; i < videoNames.Length; i++) {
				str += "Video Name: " + videoNames[i] + "\nVideo Short Name: ";
				str += videoShortNames[i] + "\nVideo Link: " + videoLinks[i] + "\n";
			}
			Debug.Log(str);
		}

		public bool LoadFrom(string _path) {
			// TODO: Implement
			Debug.LogWarning("Load not implemented!");
			return true;
		}

		public bool SaveTo(string _path) {
			// TODO: Implement
			Debug.LogWarning("Save not implemented!");
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
