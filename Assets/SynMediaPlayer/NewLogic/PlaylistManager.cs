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
		[SerializeField] private string[] videoTypes;
		[SerializeField] private VRCUrl[] videoLinks;
		[SerializeField] private VRCUrl[] questVideoLinks;
		[SerializeField] private int[] videoTypeOffsets;
		[SerializeField] private int[] videoTypeLengths;

		[UdonSynced] private string[] userPlaylistNames;
		[UdonSynced] private string[] userVideoNames;
		[UdonSynced] private VRCUrl[] userVideoLinks;
		[UdonSynced] private int[] userPlaylistOffsets;
		[UdonSynced] private int[] userPlaylistLengths;
		[UdonSynced] private int numUserPlaylists;

		private const string PlaylistsBrokenError = "Playlists are broken! Disabling playlists.";

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
				questVideoLinks = Array.Empty<VRCUrl>();
				videoTypeLengths = Array.Empty<int>();
				videoTypeOffsets = Array.Empty<int>();
				videoNames = Array.Empty<string>();
				videoLinks = Array.Empty<VRCUrl>();
				videoTypes = Array.Empty<string>();
			}

			int videoDumpIndex = 0; // Iterator for non structured video data
			int linkDumpIndex = 0; // Iterator for non structured link data
			for (int playlistIndex = 0; playlistIndex < numPlaylists; playlistIndex++) {
				// The playlist offset will be the index of its first video in the unstructured data
				playlistOffsets[playlistIndex] = videoDumpIndex;
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
					Array.Resize(ref videoTypeLengths, videoTypeLengths.Length + numVideos);
					Array.Resize(ref videoTypeOffsets, videoTypeOffsets.Length + numVideos);
				}

				for (int videoIndex = 0; videoIndex < numVideos; videoIndex++) {
					// ReSharper disable once PossibleNullReferenceException
					Video video = playlist.videos[videoIndex];
					videoShortNames[videoDumpIndex] = video.shortName;
					videoNames[videoDumpIndex] = video.name;

					// We will make numLinks 0 if links is null, which will prevent null data access
					videoTypeOffsets[videoDumpIndex] = linkDumpIndex;
					int numLinks = video.links?.Length ?? 0;

					// Reduce runtime overhead by stashing length
					videoTypeLengths[videoDumpIndex] = numLinks;

					if (numLinks <= 0) continue;

					if (_fullRebuild) {
						Array.Resize(ref videoLinks, videoLinks.Length + numLinks);
						Array.Resize(ref questVideoLinks, questVideoLinks.Length + numLinks);
						Array.Resize(ref videoTypes, videoTypes.Length + numLinks);
					}

					for (int linkIndex = 0; linkIndex < numLinks; linkIndex++) {
						// ReSharper disable once PossibleNullReferenceException
						CompatLink link = video.links[linkIndex];
						videoTypes[linkDumpIndex] = link.type ?? "";
						// Creating new VRCUrl objects is legal outside of Udon, so doing it here
						videoLinks[linkDumpIndex] = new VRCUrl(link.pc?.Trim() ?? "");
						VRCUrl questLink = string.IsNullOrWhiteSpace(link.quest) ?
							videoLinks[linkDumpIndex] : new VRCUrl(link.quest.Trim());
						questVideoLinks[linkDumpIndex] = questLink;
						linkDumpIndex++;
					}
					videoDumpIndex++;
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
				str += $"{videoShortNames[i]}\nVideo Type Offset: ";
				str += $"{videoTypeOffsets[i]}, Length: {videoTypeLengths[i]}\n";
			}
			str += "Video Links:\n";
			for (int i = 0; i < videoLinks.Length; i++) {
				str += $"\"{videoLinks[i]}\", \"{questVideoLinks[i]}\" ({videoTypes[i]})";
			}
			Debug.Log(str);
		}

		public bool LoadFromJson(string _path) {
			if (!File.Exists(_path)) {
				Debug.LogError("Backup not found!");
				return false;
			}

			string fileData = File.ReadAllText(_path);

			if (string.IsNullOrWhiteSpace(fileData)) {
				Debug.LogError("Backup empty!");
				return false;
			}

			playlistData = JsonUtility.FromJson<PlaylistData>(fileData);
			Debug.Log($"Loaded backup from: {_path}");

			RebuildSerialized(true);
			return true;
		}

		public bool SaveToJson(string _path) {
			string serializedData = JsonUtility.ToJson(playlistData, true);
			Debug.Log("Data:\n" + serializedData);
			File.WriteAllText(_path, serializedData);
			Debug.Log($"Saved backup to: {_path}");
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
					type = "default",
					pc = line3,
					quest = line3
				};
				Video video = new Video {
					name = line1,
					shortName = line2,
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
				writer.WriteLine((video.links?.Length ?? 0) > 0 ? video.links[0].pc : "");
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
				LogError(PlaylistsBrokenError);
				return;
			}

			if (playlistNames.Length != playlistOffsets.Length
			    || playlistNames.Length != playlistLengths.Length
			    || videoNames.Length != videoShortNames.Length
			    || videoNames.Length != videoTypeOffsets.Length
			    || videoNames.Length != videoTypeLengths.Length
			    || videoTypes.Length != videoLinks.Length
			    || videoTypes.Length != questVideoLinks.Length
			    || videoTypeOffsets[0] != 0 || playlistOffsets[0] != 0) {
				LogError(PlaylistsBrokenError);
				return;
			}

			if (playlistOffsets.Length == 0 || videoTypeOffsets.Length == 0) {
				LogError(PlaylistsBrokenError);
				return;
			}

			int lastOffset = 0;
			foreach (int offset in playlistOffsets) {
				if (offset > videoNames.Length || offset < lastOffset) {
					LogError(PlaylistsBrokenError);
					return;
				}
				lastOffset = offset;
			}

			lastOffset = playlistOffsets[0];
			for (int i = 1; i < playlistLengths.Length; i++) {
				int playlistLength = playlistLengths[i - 1];
				lastOffset += playlistLength;
				if (lastOffset == playlistOffsets[i] && playlistLength >= 0) continue;
				LogError(PlaylistsBrokenError);
				return;
			}

			lastOffset += playlistLengths[playlistLengths.Length - 1];
			if (lastOffset != videoNames.Length) {
				LogError(PlaylistsBrokenError);
				return;
			}

			lastOffset = 0;
			foreach (int offset in videoTypeOffsets) {
				if (offset > videoLinks.Length || offset < lastOffset) {
					LogError(PlaylistsBrokenError);
					return;
				}
				lastOffset = offset;
			}

			lastOffset = videoTypeOffsets[0];
			for (int i = 1; i < videoTypeLengths.Length; i++) {
				int videoTypeLength = videoTypeLengths[i - 1];
				lastOffset += videoTypeLength;
				if (lastOffset == videoTypeOffsets[i] && videoTypeLength >= 0) continue;
				LogError(PlaylistsBrokenError);
				return;
			}

			lastOffset += videoTypeLengths[videoTypeLengths.Length - 1];
			if (lastOffset != videoLinks.Length) {
				LogError(PlaylistsBrokenError);
				return;
			}

			int currentPlaylist = 0;
			for (int i = 0; i < videoNames.Length; i++) {
				while (currentPlaylist + 1 < playlistOffsets.Length && i >= playlistOffsets[currentPlaylist + 1]) currentPlaylist++;
				bool missingLink = videoTypeLengths[i] <= 0;
				int videoIndex = i - playlistOffsets[currentPlaylist];
				if (missingLink) LogWarning($"Video {videoIndex} in playlist {currentPlaylist} is missing a link!");
				bool missingName = string.IsNullOrWhiteSpace(videoNames[i]);
				bool missingShortName = string.IsNullOrWhiteSpace(videoShortNames[i]);
				if (missingName && missingShortName) {
					videoNames[i] = videoShortNames[i] = $"Video #{i}";
				} else if (missingName) {
					videoNames[i] = videoShortNames[i];
				} else if (missingShortName) {
					videoShortNames[i] = videoNames[i];
				}
			}

			for (int i = 0; i < videoLinks.Length; i++) {
				if (videoLinks[i] == null) videoLinks[i] = VRCUrl.Empty;
				if (questVideoLinks[i] == null) questVideoLinks[i] = videoLinks[i];
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
		/// <param name="_type">Link Type</param>
		/// <param name="_name">Video Name return</param>
		/// <param name="_shortName">Video Friendly Name return</param>
		/// <param name="_link">Video Link return</param>
		/// <returns>True on success, False if video or playlist doesn't exist.</returns>
		public bool _GetWorldVideo(int _playlist, int _video, string _type, ref string _name, ref string _shortName, ref VRCUrl _link) {
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
			int linkOffset = videoTypeOffsets[videoIndex];
			int numLinks = videoTypeLengths[videoIndex];

			int videoLinkIndex;
			if (numLinks < 1) {
				LogWarning("No links in video!");
				videoLinkIndex = linkOffset - 1;
			} else if (string.IsNullOrWhiteSpace(_type)) {
				Log($"Type null, using default. ({linkOffset})");
				videoLinkIndex = linkOffset;
			} else {
				videoLinkIndex = Array.IndexOf(videoTypes, _type, linkOffset, numLinks);
				if (videoLinkIndex < linkOffset) {
					Log($"Type not found in video, using default. ({linkOffset})");
					videoLinkIndex = linkOffset;
				} else {
					Log($"Found type in index {videoLinkIndex}");
				}
			}

			_shortName = videoShortNames[videoIndex];
			_name = videoNames[videoIndex];
			_link = videoLinkIndex < linkOffset ? VRCUrl.Empty : videoLinks[videoLinkIndex];
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
		/// <param name="_type">Link type</param>
		/// <param name="_name">Video Name return</param>
		/// <param name="_shortName">Video Friendly Name return</param>
		/// <param name="_link">Video Link return</param>
		/// <returns>True on success, False if video or playlist doesn't exist.</returns>
		public bool _GetVideo(int _playlistType, int _playlist, int _video, string _type, ref string _name, ref string _shortName, ref VRCUrl _link) {
			switch (_playlistType) {
				case 0:
					Log("Getting video from world playlists");
					return _GetWorldVideo(_playlist, _video, _type, ref _name, ref _shortName, ref _link);
				case 1:
					Log("Getting video from user playlists");
					return _GetUserVideo(_playlist, _video, ref _name, ref _shortName, ref _link);
			}
			LogError($"Invalid playlist type: {_playlistType}");
			return false;
		}
	}
}
