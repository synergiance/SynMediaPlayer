using System;
using System.Collections.Generic;
using System.IO;
using Synergiance.MediaPlayer.Diagnostics;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Synergiance.MediaPlayer {
	/// <summary>
	/// A struct containing PC and Quest optimized links to the same video, as
	/// well as a type name for flexible configuration.
	/// </summary>
	[Serializable]
	public struct CompatLink {
		public string type;
		public string pc;
		public string quest;
	}

	/// <summary>
	/// A struct containing the name, link, and a friendly name for a video.
	/// </summary>
	[Serializable]
	public struct Video {
		public string name;
		public string link;
		public string shortName;
		public CompatLink[] links;
	}

	/// <summary>
	/// A struct containing a playlist name and an array of videos
	/// </summary>
	[Serializable]
	public struct Playlist {
		public string name;
		public Video[] videos;
	}

	/// <summary>
	/// A wrapper allowing us to serialize the playlists array
	/// </summary>
	[Serializable]
	public struct PlaylistData {
		public Playlist[] playlists;
	}

	/// <summary>
	/// A class containing any number of playlists containing any number of videos.
	/// </summary>
	[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
	public class PlaylistManager : DiagnosticBehaviour {
		[SerializeField] private PlaylistData playlistData;// Currently just a helper for the UI
		[SerializeField] private string playlistBackup; // Saves playlist backup name

		[SerializeField] private int[] playlistOffsets;
		[SerializeField] private int[] playlistLengths;
		[SerializeField] private string[] playlistNames;
		[SerializeField] private string[] videoShortNames;
		[SerializeField] private string[] videoNames;
		[SerializeField] private VRCUrl[] videoLinks;
		[SerializeField] private VRCUrl[] questVideoLinks;
		[SerializeField] private int[] videoOffsets;
		[SerializeField] private int[] videoTypes;

		[UdonSynced] private string[] userPlaylistNames;
		[UdonSynced] private string[] userVideoNames;
		[UdonSynced] private VRCUrl[] userVideoLinks;
		[UdonSynced] private int[] userPlaylistOffsets;
		[UdonSynced] private int[] userPlaylistLengths;
		[UdonSynced] private int numUserPlaylists;

		protected override string DebugName => "Playlist Manager";
		protected override string DebugColor => ColorToHtmlStringRGB(new Color(0.65f, 0.15f, 0.15f));

		private bool initialized;
		private bool playlistsValid;

		/// <summary>
		/// The user playlist allocation increment size, also the initial allocation
		/// </summary>
		private const int ArrayIncrement = 16;

		#if !COMPILER_UDONSHARP && UNITY_EDITOR
		public void RebuildSerialized(bool _fullRebuild) {
			// We will make numPlaylists 0 if it's null, which will prevent null data access
			int numPlaylists = playlistData.playlists?.Length ?? 0;
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
				Playlist playlist = playlistData.playlists[playlistIndex];
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

		/// <summary>
		/// This dumps the contents of the serialized playlists.
		/// 
		/// This is dead code but it could come in handy in the future. Seeing
		/// as it will be ignored by Udon, it's harmless.
		/// </summary>
		public void DumpContents() {
			string str = "Playlists:\n";
			foreach (Playlist playlist in playlistData.playlists) {
				str += $"Playlist: {playlist.name}\n";
				foreach (Video video in playlist.videos) {
					str += $"Video Title: {video.name}\nVideo Short Name: ";
					str += $"{video.shortName}\nVideo Links:\n";
					foreach (CompatLink link in video.links)
						str += $"- \"{link.pc}\", \"{link.quest}\" ({link.type})\n";
				}
			}
			str += "Videos:\n";
			for (int i = 0; i < videoNames.Length; i++) {
				str += $"Video Name: {videoNames[i]}\nVideo Short Name: ";
				str += $"{videoShortNames[i]}\nVideo Link: {videoLinks[i]}\n";
			}
			Debug.Log(str);
		}

		public bool LoadFromJson(string _path) {
			return true;
		}

		public bool SaveToJson(string _path) {
			string test = JsonUtility.ToJson(playlistData, true);
			Debug.Log("Data:\n" + test);
			return true;
		}

		/// <summary>
		/// Helper function to load playlists from disk.
		/// </summary>
		/// <param name="_path">The path of the directory to load the playlists from.</param>
		/// <returns>True on success, False on failure.</returns>
		public bool LoadFrom(string _path) {
			Debug.Log($"Load path: {_path}");
			if (!Directory.Exists(_path)) {
				Debug.LogWarning("Backup doesn't exist in this project.");
				return false;
			}
			string[] files = Directory.GetFiles(_path, "*.txt");
			if (files.Length < 1) {
				Debug.LogWarning("Backup doesn't exist in this project.");
				return false;
			}
			string basePath = _path + "/";
			Debug.Log($"Working Directory: {basePath}");
			playlistData.playlists = new Playlist[files.Length];
			for (int i = 0; i < files.Length; i++) {
				string playlistName = files[i].Substring(files[i].LastIndexOf('\\') + 1);
				playlistName = playlistName.Substring(0, playlistName.Length - 4);
				playlistData.playlists[i] = new Playlist {
					name = playlistName,
					videos = LoadPlaylistFrom(files[i])
				};
				Debug.Log($"Loaded playlist: {playlistName}.txt");
			}
			RebuildSerialized(true);
			return true;
		}

		/// <summary>
		/// Helper function to save playlists to disk.
		/// </summary>
		/// <param name="_path">The path of the directory to save the playlists to.</param>
		/// <returns>True on success, False on failure.</returns>
		public bool SaveTo(string _path) {
			bool createDirectory = !Directory.Exists(_path);
			if (createDirectory) Directory.CreateDirectory(_path);
			string basePath = _path + "/";
			Debug.Log((createDirectory ? "Created working directory: " : "Working directory: ") + basePath);
			foreach (Playlist playlist in playlistData.playlists)
				if (SavePlaylistTo(playlist.videos, basePath + playlist.name + ".txt"))
					Debug.Log($"Saved playlist: {playlist.name}.txt");
			return true;
		}

		/// <summary>
		/// Helper function to load a single playlist from file.
		/// </summary>
		/// <param name="_path">Full path of the file to load the playlist from.</param>
		/// <returns>An array containing all the videos that were loaded.</returns>
		private Video[] LoadPlaylistFrom(string _path) {
			List<Video> videos = new List<Video>();
			StreamReader reader = new StreamReader(_path);
			bool reachedEnd = false;
			string line1 = null, line2 = null, line3 = null;
			while (!reachedEnd) {
				if (!ReadLine(ref line1, reader)) break;
				if (!ReadLine(ref line2, reader)) break;
				if (!ReadLine(ref line3, reader)) break;
				CompatLink[] links = new CompatLink[1];
				links[0] = new CompatLink {
					type = "",
					pc = line3
				};
				Video video = new Video {
					name = line1,
					shortName = line2,
					link = line3,
					links = links
				};
				videos.Add(video);
			}
			reader.Close();
			return videos.ToArray();
		}

		/// <summary>
		/// Helper function to save a single playlist to file.
		/// </summary>
		/// <param name="_videos">An array containing all the videos to save.</param>
		/// <param name="_path">Full path of the file to save the playlist to.</param>
		/// <returns>True on success, false on failure.</returns>
		private bool SavePlaylistTo(Video[] _videos, string _path) {
			if (_videos == null) return false;
			StreamWriter writer = new StreamWriter(_path, false);
			foreach (Video video in _videos) {
				writer.WriteLine(video.name);
				writer.WriteLine(video.shortName);
				writer.WriteLine(video.link);
			}
			writer.Close();
			return true;
		}

		/// <summary>
		/// Internal function that will read the next line from a stream
		/// </summary>
		/// <param name="_str">String to store the line that was read.</param>
		/// <param name="_reader">The stream reader connected to the stream.</param>
		/// <returns>True when it reads a line successfully, False when there's no more data.</returns>
		private bool ReadLine(ref string _str, TextReader _reader) {
			bool found = false;
			while (!found) {
				_str = _reader.ReadLine();
				if (_str == null) break;
				_str = _str.Trim();
				found = true;
			}
			return found;
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

		/// <summary>
		/// Checks integrity of serialized playlists. Sets playlistsValid to
		/// true if all checks pass.
		/// </summary>
		private void CheckPlaylists() {
			Log("Checking playlist integrity");
			if (playlistNames == null
			    || videoLinks == null
			    || videoNames == null
			    || videoShortNames == null
			    || playlistOffsets == null
			    || playlistLengths == null) {
				LogError("No playlists or playlists are broken! Disabling playlists.");
				return;
			}

			if (playlistNames.Length != playlistOffsets.Length
			    || playlistNames.Length != playlistLengths.Length
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

			lastOffset = playlistOffsets[0];
			for (int i = 1; i < playlistLengths.Length; i++) {
				int playlistLength = playlistLengths[i - 1];
				lastOffset += playlistLength;
				if (lastOffset == playlistOffsets[i] && playlistLength >= 0) continue;
				LogError("Lengths and offsets don't match!");
				return;
			}

			lastOffset += playlistLengths[playlistLengths.Length - 1];
			if (lastOffset != videoNames.Length) {
				LogError("Lengths don't add up!");
				return;
			}

			int currentPlaylist = 0;
			for (int i = 0; i < videoNames.Length; i++) {
				while (currentPlaylist + 1 < playlistOffsets.Length && i >= playlistOffsets[currentPlaylist + 1]) currentPlaylist++;
				if (videoLinks[i] == null) videoLinks[i] = VRCUrl.Empty;
				bool missingLink = String.IsNullOrWhiteSpace(videoLinks[i].ToString());
				int videoIndex = i - playlistOffsets[currentPlaylist];
				if (missingLink) LogWarning($"Video {videoIndex} in playlist {currentPlaylist} is missing a link!");
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

		/// <summary>
		/// Initializes the user playlist arrays.
		/// </summary>
		private void InitializeUserPlaylists() {
			numUserPlaylists = 0;
			Log("Initializing user playlists...");
			userPlaylistNames = new string[ArrayIncrement];
			userPlaylistLengths = new int[ArrayIncrement];
			userPlaylistOffsets = new int[ArrayIncrement];
			userVideoLinks = new VRCUrl[ArrayIncrement];
			userVideoNames = new string[ArrayIncrement];
		}

		/// <summary>
		/// Gets a video from any of the serialized world playlists.
		/// </summary>
		/// <param name="_playlist">Playlist index</param>
		/// <param name="_video">Video index</param>
		/// <param name="_name">Video Name return</param>
		/// <param name="_shortName">Video Friendly Name return</param>
		/// <param name="_link">Video Link return</param>
		/// <returns>True on success, False if video or playlist doesn't exist.</returns>
		public bool _GetWorldVideo(int _playlist, int _video, ref string _name, ref string _shortName, ref VRCUrl _link) {
			if (_playlist >= playlistNames.Length || _playlist < 0) {
				LogError("Playlist out of bounds!");
				return false;
			}
			if (_video >= playlistLengths[_playlist] || _video < 0) {
				LogError("Video out of bounds!");
				return false;
			}
			int videoIndex = playlistOffsets[_playlist] + _video;
			Log($"Fetching video information from index {videoIndex}");
			_shortName = videoShortNames[videoIndex];
			_name = videoNames[videoIndex];
			_link = videoLinks[videoIndex];
			Log($"Name: \"{_name}\", Short Name: \"{_shortName}\", Link: \"{_link}\"");
			return true;
		}

		/// <summary>
		/// Gets a video from any of the unserialized user playlists.
		/// </summary>
		/// <param name="_playlist">Playlist index</param>
		/// <param name="_video">Video index</param>
		/// <param name="_name">Video Name return</param>
		/// <param name="_shortName">Video Friendly Name return</param>
		/// <param name="_link">Video Link return</param>
		/// <returns>True on success, False if video or playlist doesn't exist.</returns>
		public bool _GetUserVideo(int _playlist, int _video, ref string _name, ref string _shortName, ref VRCUrl _link) {
			// TODO: Implement
			LogError("User videos not implemented!");
			return false;
		}

		/// <summary>
		/// Gets a video from any of the playlists.
		/// </summary>
		/// <param name="_playlistType">Type of playlist to fetch from</param>
		/// <param name="_playlist">Playlist index</param>
		/// <param name="_video">Video index</param>
		/// <param name="_name">Video Name return</param>
		/// <param name="_shortName">Video Friendly Name return</param>
		/// <param name="_link">Video Link return</param>
		/// <returns>True on success, False if video or playlist doesn't exist.</returns>
		public bool _GetVideo(int _playlistType, int _playlist, int _video, ref string _name, ref string _shortName, ref VRCUrl _link) {
			switch (_playlistType) {
				case 0:
					Log("Getting video from world playlists");
					return _GetWorldVideo(_playlist, _video, ref _name, ref _shortName, ref _link);
				case 1:
					Log("Getting video from user playlists");
					return _GetUserVideo(_playlist, _video, ref _name, ref _shortName, ref _link);
			}
			LogError($"Invalid playlist type: {_playlistType}");
			return false;
		}
	}
}
