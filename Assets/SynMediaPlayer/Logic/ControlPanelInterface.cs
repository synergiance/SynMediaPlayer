
using System;
using Synergiance.MediaPlayer.UI;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace Synergiance.MediaPlayer {
	public class ControlPanelInterface : UdonSharpBehaviour {
		[SerializeField] private MediaPlayer mediaPlayer;
		[SerializeField] private float updatesPerSecond = 5;
		[SerializeField] private bool enableDebug;
		[SerializeField] private bool verboseDebug;
		[SerializeField] private bool loadGapless;
		[SerializeField] private bool autoplay = true;
		[Range(5, 50)] [SerializeField] private int maxVideosInQueue = 20;

		[SerializeField] private VRCUrl[] defaultPlaylist;

		[UdonSynced] private VRCUrl[] videoQueueRemote;
		private VRCUrl[] videoQueueLocal;

		[HideInInspector] public string statusText;

		private bool initialized;
		private bool isValid;
		private bool isStream;
		private float timeBetweenUpdates;
		private float lastSlowUpdate;
		private float lastResync;
		private bool hideTime;
		private bool queueCheckURL;
		private bool reachedEnd;
		private bool hasActivated;

		[UdonSynced] private bool isDefaultPlaylist = true;
		[UdonSynced] private int currentDefaultIndex;

		private string[] modList;
		private int[] modIdList;

		private string playerStatus;
		private float currentTime;
		private float duration;
		private bool isPlaying;
		private bool isReady;
		private bool isLooping;

		private ControlPanel[] controlPanels;

		private int hardQueueCap = 100;

		private string debugPrefix = "[<color=#20C0A0>SMP Control Panel Interface</color>] ";

		public int MediaType { get; private set; }

		public bool Active {
			set {
				Initialize();
				if (!isValid) {
					LogInvalid();
					return;
				}
				if (value == mediaPlayer.Active) {
					LogVerbose("Already " + (value ? "active" : "inactive") + ", ignoring request.", this);
					return;
				}
				Log("Setting player state to " + (value ? "active" : "inactive"), this);
				mediaPlayer._SetActive(value);
				//UpdatePowerButton(); TODO: Callback
			}
			get => mediaPlayer.Active;
		} // TODO: Add caching variable

		public bool IsLocked {
			set {
				Initialize();
				if (!isValid) {
					LogInvalid();
					return;
				}
				if (value == mediaPlayer.IsLocked) {
					LogVerbose("Already " + (value ? "locked" : "unlocked") + ", ignoring request.", this);
					return;
				}
				// TODO: Refactor media player lock status
				if (value) mediaPlayer._Lock();
				else mediaPlayer._Unlock();
			}
			get => mediaPlayer.IsLocked;
		} // TODO: Add caching variable
		public float Duration => mediaPlayer.Duration; // TODO: Add caching variable
		public float CurrentTime => mediaPlayer.CurrentTime; // TODO: Add caching variable
		public float PreciseTime => mediaPlayer.PreciseTime; // TODO: Add caching variable
		public bool Ready => mediaPlayer.Ready; // TODO: Add caching variable
		public bool IsPlaying => mediaPlayer.IsPlaying; // TODO: Add caching variable
		public bool IsSyncing => mediaPlayer.IsSyncing; // TODO: Add caching variable
		public float SeekPos { get; private set; } // Normalized seek position
		public bool SeekEnable { get; private set; } // Seek bar enabled
		public bool SeekLock { get; private set; } // Seek bar locked
		public string Status => playerStatus;
		public VRCPlayerApi CurrentOwner => Networking.GetOwner(mediaPlayer.gameObject); // TODO: Add caching variable
		public bool HasPermissions => mediaPlayer.HasPermissions; // TODO: Add caching variable
		public string[] ModList => GetModList();
		public int[] ModIdList => GetModIdList();

		/// <summary>
		/// Current volume
		/// </summary>
		public float Volume { get; private set; }

		/// <summary>
		/// Mute status
		/// </summary>
		public bool Mute { get; private set; }

		/// <summary>
		/// Loop accessor and modifier
		/// </summary>
		public bool Loop {
			set {
				
				Initialize();
				if (!isValid) {
					LogInvalid();
					return;
				}
				mediaPlayer._SetLooping(isLooping = value);
				// TODO: Update UI
			}
			get => isLooping; // TODO: This will need to be set
		}

		void Start() {
			Initialize();
		}

		private void Initialize() {
			if (initialized) return;
			Log("Initializing", this);
			isValid = true;
			if (!mediaPlayer) {
				isValid = false;
				LogWarning("Media Player not set!", this);
			}
			// Keep UPS to between 50 per second to one every 10 seconds
			timeBetweenUpdates = Mathf.Max(0.02f, 1 / Mathf.Max(0.1f, updatesPerSecond));
			//if (volumeControl && isValid) volumeControl._SetVolume(mediaPlayer.Volume);
			// ReSharper disable once ConditionIsAlwaysTrueOrFalse
			if (maxVideosInQueue < 1) maxVideosInQueue = 1;
			if (maxVideosInQueue > hardQueueCap) maxVideosInQueue = hardQueueCap;
			initialized = true;
			UpdateMethods();
			RebuildModList();
			UpdateModList();
		}

		// This function is what will be called on first activation when the video player decides its right and
		// after its done all of its initialization.
		private void Activate() {
			Log("First Activation", this);
			if (!mediaPlayer.IsPlaying) InitializeDefaultPlaylist();
			SendCustomEventDelayedSeconds("_SlowUpdate", timeBetweenUpdates);
			UpdateMethods();
			hasActivated = true;
			CheckDeserialization();
		}

		public void _SlowUpdate() {
			if (Time.time < lastSlowUpdate + timeBetweenUpdates * 0.9f) return;
			lastSlowUpdate = Time.time;
			SendCustomEventDelayedSeconds("_SlowUpdate", timeBetweenUpdates);
			UpdateMethods();
		}

		private void UpdateMethods() {
			UpdateTimeAndStatus();
			//UpdateAllButtons();
			UpdateCurrentOwner();
		}

		/// <summary>
		/// If playing a stream, this will send a <c>_Stop</c> command if the
		/// stream is playing or a <c>_Play</c> command if not. Otherwise, this
		/// will send a <c>_PlayPause</c> command.
		/// </summary>
		public void _PlayPauseStop() {
			Initialize();
			if (!isValid) {
				LogInvalid();
				return;
			}
			if (mediaPlayer.MediaType == 0) { // Media Type 0 is video
				mediaPlayer._PlayPause();
			} else { // Media Type 1-2 is stream
				if (mediaPlayer.IsPlaying) mediaPlayer._Stop();
				else mediaPlayer._Play();
			}
			isPlaying = mediaPlayer.IsPlaying;
			SendRefresh("Playback");
			//UpdatePlayPauseStopButtons();
		}

		/// <summary>
		/// Sends the <c>_PlayPause</c> command to the video player
		/// </summary>
		public void _PlayPause() {
			Initialize();
			if (!isValid) {
				LogInvalid();
				return;
			}
			mediaPlayer._PlayPause();
			isPlaying = mediaPlayer.IsPlaying;
			SendRefresh("Playback");
			//UpdatePlayPauseStopButtons();
		}

		/// <summary>
		/// Sends the <c>_Play</c> command to the video player
		/// </summary>
		public void _Play() {
			Initialize();
			if (!isValid) {
				LogInvalid();
				return;
			}
			mediaPlayer._Play();
			isPlaying = mediaPlayer.IsPlaying;
			SendRefresh("Playback");
			//UpdatePlayPauseStopButtons();
		}

		/// <summary>
		/// Sends the <c>_Pause</c> command to the video player
		/// </summary>
		public void _Pause() {
			Initialize();
			if (!isValid) {
				LogInvalid();
				return;
			}
			mediaPlayer._Pause();
			isPlaying = mediaPlayer.IsPlaying;
			SendRefresh("Playback");
			//UpdatePlayPauseStopButtons();
		}

		/// <summary>
		/// Sends the <c>_Stop</c> command to the video player
		/// </summary>
		public void _Stop() {
			Initialize();
			if (!isValid) {
				LogInvalid();
				return;
			}
			mediaPlayer._Stop();
			isPlaying = mediaPlayer.IsPlaying;
			SendRefresh("Playback");
			//UpdatePlayPauseStopButtons();
		}

		public void _SetVolume(float volume, bool mute) {
			Initialize();
			if (!isValid) {
				LogInvalid();
				return;
			}
			Volume = volume;
			Mute = mute;
			SetVolume();
		}

		private void SetVolume() {
			mediaPlayer._SetVolume(Volume);
			mediaPlayer._SetMute(Mute);
			SendRefresh("Volume");
		}

		public void _RunOrCancelDiagnostics() {
			if (!isValid) return;
			if (mediaPlayer.IsLoggingDiagnostics)
				mediaPlayer._CancelDiagnostics();
			else
				mediaPlayer._StartDiagnostics();
		}

		public void _LoadUrl(VRCUrl url, int newMediaType) {
			Initialize();
			if (!isValid) {
				LogInvalid();
				return;
			}
			if (mediaPlayer.IsLocked && !mediaPlayer.HasPermissions) {
				LogWarning("Not permitted to load a new URL", this);
				return;
			}
			if (url == null || string.IsNullOrWhiteSpace(url.ToString())) {
				LogError("URL is empty!", this);
				MediaType = mediaPlayer.MediaType;
				SendRefresh("MediaType");
				return;
			}
			CancelDefaultPlaylist();
			ClearQueueInternal();
			Log("Load URL: " + url.ToString(), this);
			int loadedType = mediaPlayer._LoadURLAs(url, newMediaType);
			if (loadedType == newMediaType) return;
			MediaType = mediaPlayer.MediaType;
			SendRefresh("MediaType");
		}

		private void LogInvalid() {
			LogError("Not properly initialized!", this);
		}

		private void UpdateCurrentOwner() {
			/* Commented out but this will need to be reformed for Multi UI
			if (!isValid || !currentOwnerField) return;
			VRCPlayerApi owner = Networking.GetOwner(mediaPlayer.gameObject);
			string newOwnerName = owner != null ? owner.displayName + (owner.isLocal ? "*" : "") : "Nobody";
			string oldOwnerName = currentOwnerField.text;
			if (string.Equals(newOwnerName, oldOwnerName)) return;
			currentOwnerField.text = newOwnerName;
			*/
		}

		private void UpdateTimeAndStatus() {
			// TODO: Allow splitting time and status
			/* Commented out but I'll need to reform this for Multi UI
			string textToDisplay = "00:00:00/00:00:00";
			if (isValid) {
				if (hideTime) return;
				float duration = mediaPlayer.Duration;
				float currentTime = mediaPlayer.CurrentTime;
				textToDisplay = FormatTime(currentTime);
				if (Single.IsNaN(duration) || Single.IsInfinity(duration)) textToDisplay = "Live";
				else if (duration > 0.01f) textToDisplay += "/" + FormatTime(duration);
			}
			statusField._SetText(textToDisplay);
			*/
		}

		private string FormatTime(float time) {
			if (Single.IsInfinity(time) || Single.IsNaN(time)) return time.ToString();
			float wTime = Mathf.Abs(time);
			bool neg = time < 0;
			string str = ((int)wTime % 60).ToString("D2");
			wTime /= 60;
			bool hasHours = wTime >= 60;
			str = ((int)wTime % 60).ToString(hasHours ? "D2" : "D1") + ":" + str;
			if (!hasHours) return neg ? "-" + str : str;
			wTime /= 60;
			bool hasDays = wTime >= 24;
			str = ((int)wTime % 24).ToString(hasDays ? "D2" : "D1") + ":" + str;
			if (!hasDays) return neg ? "-" + str : str;
			wTime /= 24;
			str = (int)wTime + ":" + str;
			return neg ? "-" + str : str;
		}

		private void UpdateModList() {
			/* Commented out but I'll need to reform this for multi UI
			if (!moderatorListField) return;
			string str;
			if (Networking.LocalPlayer == null) {
				str = "Debug User";
			} else {
				if (modList == null || modList.Length == 0) RebuildModList();
				if (modList.Length <= 0) {
					str = "No Moderators Present!";
				} else {
					str = modList[0];
					for (int i = 1; i < modList.Length; i++) str += ", " + modList[i];
				}
			}
			moderatorListField.text = str;
			*/
		}

		private void UpdateUrls() {
			/* Commented out but I'll need to reform this for multi UI
			if (!isValid) return;
			VRCUrl url = mediaPlayer.CurrentUrl;
			if (url == null || string.IsNullOrWhiteSpace(url.ToString())) return;
			if (!currentUrlField) return;
			if (prevUrlField) prevUrlField.text = currentUrlField.text;
			currentUrlField.text = url.ToString();
			*/
		}

		private void SuppressSecurity() {
			if (mediaPlayer.HasPermissions) return;
			LogVerbose("Suppress Security", this);
			mediaPlayer._SuppressSecurity(Time.time);
		}

		private void UpdateUI() {
			string refreshString = "";
			int newMediaType = mediaPlayer.MediaType;
			if (newMediaType != MediaType) {
				MediaType = newMediaType;
				refreshString += "MediaType ";
			}
			// TODO: Check all remaining variables, update local copies, reduce callbacks
			SendCallbackEvent("_RefreshAll");
		}

		// --------------------- API Methods ----------------------

		/// <summary>
		/// Registers a control panel so it can receive updates.
		/// </summary>
		/// <param name="panel">The <c>ControlPanel</c> that will be registered.</param>
		/// <returns>The registration index.</returns>
		public int RegisterPanel(ControlPanel panel) {
			if (controlPanels == null || controlPanels.Length <= 0) {
				controlPanels = new ControlPanel[1];
			} else {
				for (int i = 0; i < controlPanels.Length; i++) {
					if (panel == controlPanels[i]) {
						LogWarning("Control panel already exists!", this);
						return i;
					}
				}
				ControlPanel[] tmpControlPanels = new ControlPanel[controlPanels.Length + 1];
				Array.Copy(controlPanels, tmpControlPanels, controlPanels.Length);
				controlPanels = tmpControlPanels;
			}
			int index = controlPanels.Length - 1;
			Log("Registering control panel: " + index, this);
			controlPanels[index] = panel;
			return index;
		}

		// --------------- Default Playlist Methods ---------------

		private void InitializeDefaultPlaylist() {
			if (!isValid) return;
			if (!isDefaultPlaylist) return;
			VRCPlayerApi localPlayer = Networking.LocalPlayer;
			if (localPlayer != null && !localPlayer.isMaster) return;
			if (defaultPlaylist == null || defaultPlaylist.Length < 1) {
				Log("Default playlist empty, disabling", this);
				isDefaultPlaylist = false;
				RequestSerialization();
				return;
			}
			LogVerbose("Initialize Default Playlist", this);
			if (!mediaPlayer.HasPermissions) SuppressSecurity();
			mediaPlayer._LoadURL(defaultPlaylist[0]);
			reachedEnd = false;
			if (autoplay) mediaPlayer._Play();
			mediaPlayer.EngageSecurity();
			PreloadNextDefaultItem();
			if (!isDefaultPlaylist) return;
			PreloadNextDefaultItem();
		}

		private void PreloadNextDefaultItem() {
			if (!isValid) return;
			if (!isDefaultPlaylist) {
				LogVerbose("Not preloading new default item, default playlist is false!", this);
				return;
			}
			VRCPlayerApi localPlayer = Networking.LocalPlayer;
			if (localPlayer != null && !localPlayer.isMaster) return;
			Log("Preload Next Default Item", this);
			currentDefaultIndex++;
			if (currentDefaultIndex >= defaultPlaylist.Length) currentDefaultIndex = 0;
			AddToQueueInternal(defaultPlaylist[currentDefaultIndex]);
		}

		private void CancelDefaultPlaylist() {
			if (!isDefaultPlaylist) return;
			LogVerbose("Cancel Default Playlist", this);
			isDefaultPlaylist = false;
			ClearQueueInternal();
			RequestSerialization();
		}

		// -------------------- Queue Methods ---------------------

		private void AddToQueue(VRCUrl url) {
			LogVerbose("Add To Queue", this);
			if (!isValid) return;
			if (!mediaPlayer.HasPermissions) {
				LogWarning("Cannot add video to queue! Permission denied!", this);
				return;
			}
			CancelDefaultPlaylist();
			AddToQueueInternal(url);
		}

		private void ClearQueue() {
			LogVerbose("Clear Queue", this);
			if (!isValid) return;
			if (!mediaPlayer.HasPermissions) {
				LogWarning("Cannot clear queue! Permission denied!", this);
				return;
			}
			CancelDefaultPlaylist();
			ClearQueueInternal();
		}

		private void RemoveFromQueue(int index) {
			LogVerbose("Remove From Queue", this);
			if (!isValid) return;
			if (!mediaPlayer.HasPermissions) {
				LogWarning("Cannot remove video from queue! Permission denied!", this);
				return;
			}
			CancelDefaultPlaylist();
			RemoveFromQueueInternal(index);
		}

		private void InsertToQueue(VRCUrl url, int index) {
			LogVerbose("Insert To Queue", this);
			if (!isValid) return;
			if (!mediaPlayer.HasPermissions && mediaPlayer.IsLocked) {
				LogWarning("Cannot insert video into queue! Permission denied!", this);
				return;
			}
			CancelDefaultPlaylist();
			InsertToQueueInternal(url, index);
		}

		private void AddToQueueInternal(VRCUrl url) {
			if (videoQueueLocal != null && videoQueueLocal.Length >= maxVideosInQueue) {
				LogWarning("Cannot add video to queue! Queue length exceeded!", this);
				return;
			}
			LogVerbose("Add To Queue Internal: " + url, this);
			VRCUrl[] tempUrls = new VRCUrl[videoQueueLocal == null ? 1 : videoQueueLocal.Length + 1];
			// ReSharper disable once AssignNullToNotNullAttribute
			// ReSharper disable once PossibleNullReferenceException
			if (tempUrls.Length > 1) Array.Copy(videoQueueLocal, tempUrls, videoQueueLocal.Length);
			tempUrls[tempUrls.Length - 1] = url;
			int oldLength = videoQueueLocal != null ? videoQueueLocal.Length : 0;
			string logMsg = "Old queue length: " + oldLength;
			logMsg += ", New queue length: " + tempUrls.Length;
			logMsg += ", First URL " + (oldLength == 0 ? "changed" : "unchanged");
			LogVerbose(logMsg, this);
			videoQueueLocal = tempUrls;
			if (tempUrls.Length == 1) {
				if (reachedEnd) {
					InsertNextUrl();
					videoQueueLocal = null;
				} else if (loadGapless) {
					UpdateNextUrl();
				}
			}
			Sync();
			UpdateQueueUI();
		}

		private void ClearQueueInternal() {
			if (videoQueueLocal == null || videoQueueLocal.Length == 0) {
				Log("Cannot clear queue! Queue already empty!", this);
				return;
			}
			LogVerbose("Clear Queue Internal", this);
			LogVerbose("Old queue length: " + (videoQueueLocal != null ? videoQueueLocal.Length : 0), this);
			videoQueueLocal = null;
			Sync();
			if (loadGapless) UpdateNextUrl();
			UpdateQueueUI();
		}

		private void RemoveFromQueueInternal(int index) {
			if (index < 0 || videoQueueLocal == null || index >= videoQueueLocal.Length) {
				LogWarning("Cannot remove video from queue! Index out of bounds!", this);
				return;
			}
			VRCUrl[] tempUrls;
			if (videoQueueLocal.Length == 1) {
				tempUrls = null;
			} else {
				tempUrls = new VRCUrl[videoQueueLocal.Length - 1];
				if (index > 0) Array.Copy(videoQueueLocal, tempUrls, index);
				if (index < videoQueueLocal.Length - 1) Array.Copy(videoQueueLocal, index + 1, tempUrls, index, tempUrls.Length - index);
			}
			LogVerbose("Remove From Queue Internal: " + index, this);
			int oldLength = videoQueueLocal != null ? videoQueueLocal.Length : 0;
			string logMsg = "Old queue length: " + oldLength;
			// ReSharper disable once PossibleNullReferenceException
			logMsg += ", New queue length: " + tempUrls.Length;
			logMsg += ", First URL " + (index == 0 ? "changed" : "unchanged");
			LogVerbose(logMsg, this);
			videoQueueLocal = tempUrls;
			Sync();
			if (index == 0 && loadGapless) UpdateNextUrl();
			UpdateQueueUI();
		}

		private void InsertToQueueInternal(VRCUrl url, int index) {
			VRCUrl[] tempUrls;
			if (videoQueueLocal == null || videoQueueLocal.Length == 0) {
				if (index != 0) {
					LogWarning("Cannot insert video into queue! Index out of bounds!", this);
					return;
				}
				AddToQueueInternal(url);
				return;
			}
			if (videoQueueLocal.Length >= maxVideosInQueue) {
				LogWarning("Cannot insert video into queue! Queue length exceeded!", this);
				return;
			}
			if (index > videoQueueLocal.Length || index < 0) {
				LogWarning("Cannot insert video into queue! Index out of bounds!", this);
				return;
			}
			tempUrls = new VRCUrl[videoQueueLocal.Length + 1];
			if (index > 0) Array.Copy(videoQueueLocal, tempUrls, index);
			if (index < videoQueueLocal.Length) Array.Copy(videoQueueLocal, index, tempUrls, index + 1, videoQueueLocal.Length - index);
			LogVerbose("Insert To Queue Internal: " + index + ", " + url, this);
			tempUrls[index] = url;
			int oldLength = videoQueueLocal != null ? videoQueueLocal.Length : 0;
			string logMsg = "Old queue length: " + oldLength;
			logMsg += ", New queue length: " + tempUrls.Length;
			logMsg += ", First URL " + (index == 0 ? "changed" : "unchanged");
			LogVerbose(logMsg, this);
			videoQueueLocal = tempUrls;
			Sync();
			if (index == 0 && loadGapless) UpdateNextUrl();
			UpdateQueueUI();
		}

		private void UpdateQueue() {
			LogVerbose("Update Queue", this);
			int oldLen = videoQueueLocal != null ? videoQueueLocal.Length : 0;
			int newLen = videoQueueRemote != null ? videoQueueRemote.Length : 0;
			bool firstUrlChanged = oldLen == 0 && newLen > 0 || oldLen > 0 && newLen == 0;
			// ReSharper disable twice PossibleNullReferenceException
			if (oldLen > 0 && newLen > 0) firstUrlChanged = string.Equals(videoQueueLocal[0].ToString(), videoQueueRemote[0].ToString());
			string logMsg = "Old queue length: " + oldLen;
			logMsg += ", New queue length: " + newLen;
			logMsg += ", First URL " + (firstUrlChanged ? "changed" : "unchanged");
			if (!isValid || !Networking.IsOwner(mediaPlayer.gameObject)) firstUrlChanged = false;
			logMsg += ", Updating next URL? " + (firstUrlChanged && loadGapless);
			LogVerbose(logMsg, this);
			videoQueueLocal = videoQueueRemote;
			if (firstUrlChanged && loadGapless) UpdateNextUrl();
			UpdateQueueUI();
		}

		private void UpdateNextUrl() {
			if (!isValid) return;
			if (!mediaPlayer.HasPermissions && !Networking.IsOwner(mediaPlayer.gameObject)) return;
			VRCUrl nextUrl = VRCUrl.Empty;
			if (videoQueueLocal != null && videoQueueLocal.Length > 0) {
				nextUrl = videoQueueLocal[0];
			}
			LogVerbose("Update Next URL: " + nextUrl, this);
			if (!mediaPlayer.HasPermissions) SuppressSecurity();
			mediaPlayer._LoadQueueURL(nextUrl);
			mediaPlayer.EngageSecurity();
		}

		private void InsertNextUrl() {
			if (!isValid) return;
			if (!mediaPlayer.HasPermissions && !Networking.IsOwner(mediaPlayer.gameObject)) return;
			if (videoQueueLocal == null || videoQueueLocal.Length <= 0) {
				LogWarning("Attempting to insert new URL when queue is empty!", this);
				return;
			}
			LogVerbose("Insert Next URL: " + videoQueueLocal[0], this);
			if (!mediaPlayer.HasPermissions) SuppressSecurity();
			mediaPlayer._LoadURL(videoQueueLocal[0]);
			mediaPlayer.EngageSecurity();
			reachedEnd = false;
			if (autoplay || isDefaultPlaylist) mediaPlayer._Play();
		}

		private void AdvanceQueue() {
			if (!Networking.IsOwner(mediaPlayer.gameObject)) return;
			LogVerbose("Advance Queue", this);
			if (videoQueueLocal == null || videoQueueLocal.Length <= 0) {
				Log("No more videos in queue!", this);
				if (videoQueueRemote != null) Sync();
				return;
			}
			if (videoQueueLocal.Length == 1) {
				if (!loadGapless) {
					LogVerbose("Inserting last URL", this);
					InsertNextUrl();
				}
				videoQueueLocal = null;
				if (videoQueueRemote != null) Sync();
				if (loadGapless) {
					Log("Updating next URL (Should be blank)", this);
					UpdateNextUrl();
				}
				UpdateQueueUI();
				PreloadNextDefaultItem();
				Sync();
				return;
			}
			if (!loadGapless) {
				LogVerbose("Inserting next URL", this);
				InsertNextUrl();
			}
			VRCUrl[] tempUrls = new VRCUrl[videoQueueLocal.Length - 1];
			Array.Copy(videoQueueLocal, 1, tempUrls, 0, tempUrls.Length);
			videoQueueLocal = tempUrls;
			Sync();
			if (loadGapless) {
				LogVerbose("Updating next URL (Should not be blank)", this);
				UpdateNextUrl();
			}
			UpdateQueueUI();
			PreloadNextDefaultItem();
			Sync();
		}

		private void UpdateQueueUI() {
			// TODO: Implement
			LogVerbose("Update Queue UI (Doesn't actually do anything yet)", this);
		}
		
		// ----------------- Serialization Methods ----------------

		public override void OnDeserialization() {
			if (!hasActivated) return;
			CheckDeserialization();
		}

		private void CheckDeserialization() {
			LogVerbose("Check Deserialization", this);
			if (videoQueueLocal != videoQueueRemote) UpdateQueue();
		}

		private void Sync() {
			LogVerbose("Sync", this);
			videoQueueRemote = videoQueueLocal;
			if (Networking.LocalPlayer == null) return;
			Networking.SetOwner(Networking.LocalPlayer, gameObject);
			RequestSerialization();
		}

		// --------------- Player Detection Methods ---------------

		private string[] GetModList() {
			if (modList == null || modList.Length == 0) RebuildModList();
			return modList;
		}

		private int[] GetModIdList() {
			if (modIdList == null || modIdList.Length == 0) RebuildModList();
			return modIdList;
		}

		private void RebuildModList() {
			if (Networking.LocalPlayer == null) {
				modList = new[] { "Debug User" };
				modIdList = new[] { 0 };
				return;
			}
			int numPlayers = VRCPlayerApi.GetPlayerCount();
			modList = new string[numPlayers];
			modIdList = new int[numPlayers];
			int numMods = 0;
			VRCPlayerApi[] players = new VRCPlayerApi[numPlayers];
			VRCPlayerApi.GetPlayers(players);
			for (int i = 0; i < numPlayers; i++) {
				VRCPlayerApi player = players[i];
				if (player == null || !player.IsValid()) continue;
				if (!mediaPlayer.CheckPrivileged(player)) continue;
				modList[numMods] = player.displayName + (player.isLocal ? "*" : "");
				modIdList[numMods] = player.playerId;
				numMods++;
			}
			string[] tempMods = modList;
			int[] tempIds = modIdList;
			modList = new string[numMods];
			modIdList = new int[numMods];
			Array.Copy(tempMods, modList, numMods);
			Array.Copy(tempIds, modIdList, numMods);
		}

		// ------------------- Callback Methods -------------------

		private void SendRefresh(string elements) {
			LogVerbose("Sending refresh method for elements: " + elements, this);
			foreach (ControlPanel controlPanel in controlPanels) {
				controlPanel._Refresh(elements);
			}
		}

		private void SendCallbackEvent(string eventName) {
			LogVerbose("Sending event to all callbacks: " + eventName, this);
			foreach (ControlPanel controlPanel in controlPanels) {
				controlPanel.SendCustomEvent(eventName);
			}
		}

		public override void OnPlayerJoined(VRCPlayerApi player) {
			Log("On Player Joined", this);
			if (player == null || !player.IsValid()) return;
			if (!mediaPlayer.CheckPrivileged(player)) return;
			if (Array.IndexOf(modIdList, player.playerId) >= 0) return;
			int numMods = modList.Length + 1;
			string[] tempMods = modList;
			int[] tempIds = modIdList;
			modList = new string[numMods];
			modIdList = new int[numMods];
			numMods -= 1;
			Array.Copy(tempMods, modList, numMods);
			Array.Copy(tempIds, modIdList, numMods);
			modList[numMods] = player.displayName;
			modIdList[numMods] = player.playerId;
			UpdateModList();
		}

		public override void OnPlayerLeft(VRCPlayerApi player) {
			Log("On Player Left", this);
			if (player == null || !player.IsValid()) return;
			if (Array.IndexOf(modIdList, player.playerId) < 0) return;
			RebuildModList();
			UpdateModList();
		}

		public void _Activate() {
			if (hasActivated) return;
			if (!mediaPlayer.Active) return;
			Activate();
		}

		public void _SetStatusText() {
			// Status text has been sent to us
			Log("Set Status Text", this);
			Initialize();
			playerStatus = statusText;
			if (isValid) {
				bool isPlaying = mediaPlayer.IsPlaying;
				hideTime = !string.Equals(statusText, "Playing") &&
					!string.Equals(statusText, "Stabilizing") &&
					mediaPlayer.MediaType == 0 || !mediaPlayer.Ready;
				//if (!hideTime) UpdateTimeAndStatus();
				//else statusField._SetText(statusText);
				//UpdateResyncButton();
			} else {
				//statusField._SetText(statusText);
			}
			// TODO: Finish method
		}

		public void _RefreshSeek() {
			SeekPos = mediaPlayer.SeekPos;
			SendRefresh("Seek");
		}

		public void _RecheckVideoPlayer() {
			Log("Recheck Video Player", this);
			UpdateCurrentOwner();
			UpdateUI();
		}

		public void _PlayerLocked() {
			// Video player has been locked
			Log("Player Locked", this);
			Initialize();
			bool hasPermissions = mediaPlayer.HasPermissions;
			//lockUnlockButton._SetMode(hasPermissions ? 1 : 2);
			//if (urlPlaceholderField) urlPlaceholderField.text = hasPermissions ? "Enter Video URL (Instance Moderators)..." : "Player locked!";
		}

		public void _PlayerUnlocked() {
			// Video player has been unlocked
			Log("Player Unlocked", this);
			Initialize();
			SeekLock = false;
			//lockUnlockButton._SetMode(0);
			//if (urlPlaceholderField) urlPlaceholderField.text = "Enter Video URL (Anyone)...";
		}

		public void _RelayVideoLoading() {
			// Video is beginning to load
			Log("Relay Video Loading", this);
			Initialize();
			VRCUrl currentURL = mediaPlayer.CurrentUrl;
			UpdateUI();
		}

		public void _RelayVideoReady() {
			// Video has finished loading
			Log("Relay Video Ready", this);
			Initialize();
			UpdateUI();
			UpdateUrls();
		}

		public void _RelayVideoError() {
			// Video player has thrown an error
			Log("Relay Video Error", this);
			reachedEnd = true;
			Initialize();
		}

		public void _RelayVideoStart() {
			// Video has started playing
			Log("Relay Video Start", this);
			Initialize();
			UpdateUI();
		}

		public void _RelayVideoPlay() {
			// Video has resumed playing
			Log("Relay Video Play", this);
			Initialize();
			UpdateUI();
		}

		public void _RelayVideoPause() {
			// Video has paused
			Log("Relay Video Pause", this);
			Initialize();
			UpdateUI();
		}

		public void _RelayVideoEnd() {
			// Video has finished playing
			Log("Relay Video End", this);
			reachedEnd = true;
			Initialize();
			UpdateUI();
			AdvanceQueue();
		}

		public void _RelayVideoLoop() {
			// Video has looped
			Log("Relay Video Loop", this);
			reachedEnd = false;
			Initialize();
			UpdateUI();
		}

		public void _RelayVideoNext() {
			// Queued video is starting
			Log("Relay Video Next", this);
			reachedEnd = false;
			Initialize();
			UpdateUI();
			AdvanceQueue();
			UpdateUrls();
		}

		public void _RelayVideoQueueLoading() {
			// Queued video is beginning to load
			Log("Relay Video Queue Loading", this);
			Initialize();
			UpdateUI();
		}

		public void _RelayVideoQueueReady() {
			// Queued video has loaded
			Log("Relay Video Queue Ready", this);
			Initialize();
			UpdateUI();
		}

		public void _RelayVideoQueueError() {
			// Queued video player has thrown an error
			Log("Relay Video Queue Error", this);
			Initialize();
		}

		// ----------------- Debug Helper Methods -----------------
		private void Log(string message, UnityEngine.Object context) { if (enableDebug) Debug.Log(debugPrefix + message, context); }
		private void LogVerbose(string message, UnityEngine.Object context) { if (verboseDebug && enableDebug) Debug.Log(debugPrefix + "(+v) " + message, context); }
		private void LogWarning(string message, UnityEngine.Object context) { Debug.LogWarning(debugPrefix + message, context); }
		private void LogError(string message, UnityEngine.Object context) { Debug.LogError(debugPrefix + message, context); }
	}
}
