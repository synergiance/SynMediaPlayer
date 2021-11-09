
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components.Video;
using VRC.SDK3.Video.Components.Base;
using VRC.SDKBase;
using VRC.Udon;

namespace Synergiance.MediaPlayer {
	public class VideoInterpolator : UdonSharpBehaviour {
		// Settings
		[SerializeField]  private VideoPlayerRelay[] mediaPlayers;
		[SerializeField]  private string[]           interpolationProps;
		[SerializeField]  private Material           interpolatorMaterial;
		[SerializeField]  private bool               gaplessSupport;
		[SerializeField]  private float              volume = 0.5f;
		[SerializeField]  private int                activeID;
		[SerializeField]  private bool               enableDebug;
		[SerializeField]  private bool               disableBlackingOut;
		
		[Header("Callback Settings")] // Settings for callback
		[SerializeField]  private UdonSharpBehaviour callback;
		[SerializeField]  private int                identifier;
		[SerializeField]  private UdonSharpBehaviour audioCallback;
		[SerializeField]  private string             audioFieldName = "audioSource";

		// Public Callback Variables
		[HideInInspector] public  int                relayIdentifier;
		[HideInInspector] public  VideoError         relayVideoError;

		public bool BlackOutPlayer {
			set {
				if (blackOutPlayer == value) return;
				blackOutPlayer = value;
				Log("Black out set to: " + blackOutPlayer);
				float visibility = blackOutPlayer && !disableBlackingOut ? 0 : 1;
				interpolatorMaterial.SetFloat(interpolationProps[activeID], visibility);
				mediaPlayers[activeID].Volume = volume * visibility;
			}
			get => blackOutPlayer;
		}

		private bool   isLooping;
		private int    nextID = -1;
		private bool   hasAudioCallback;
		private bool   initialized;
		private bool   blackOutPlayer;
		private bool   gaplessLoaded;

		private string debugPrefix = "[<color=#1FAF5F>Video Interpolator</color>] ";

		private void Start() {
			Initialize();
		}

		private void Initialize() {
			if (initialized) return;
			initialized = true;
			for (int c = 0; c < mediaPlayers.Length; c++) {
				interpolatorMaterial.SetFloat(interpolationProps[c], 0);
			}
			mediaPlayers[activeID].Volume = volume;
			interpolatorMaterial.SetFloat(interpolationProps[activeID], 1);
			mediaPlayers[activeID].Loop = isLooping;
			hasAudioCallback = audioCallback != null && !string.IsNullOrWhiteSpace(audioFieldName);
			if (gaplessSupport) nextID = mediaPlayers.Length - 1;
		}

		public void _SwitchPlayer(int id) {
			Initialize();
			if (id == activeID) return;
			if (id >= (gaplessSupport ? mediaPlayers.Length - 1 : mediaPlayers.Length) || id < 0) {
				Error("Switching Players: Identifier (" + id + ") out of range! (" + mediaPlayers.Length + ")");
				return;
			}
			Log("Switching from " + PlayerNameAndIDString(activeID) + " to " + PlayerNameAndIDString(id));
			SwitchPlayerInternal(id);
			if (gaplessSupport && id == 0) nextID = mediaPlayers.Length - 1;
		}

		private void SwitchPlayerInternal(int id) {
			if (id == activeID) return;
			int oldID = activeID;
			activeID = id;
			if (oldID >= 0) {
				mediaPlayers[oldID].Time = 0;
				mediaPlayers[oldID]._Pause();
				mediaPlayers[oldID]._Stop();
				mediaPlayers[oldID].Volume = 0;
				interpolatorMaterial.SetFloat(interpolationProps[oldID], 0);
			}
			if (id >= 0) {
				mediaPlayers[id].Loop = isLooping;
				float visibility = blackOutPlayer ? 0 : 1;
				mediaPlayers[id].Volume = volume * visibility;
				interpolatorMaterial.SetFloat(interpolationProps[id], visibility);
				if (hasAudioCallback) audioCallback.SetProgramVariable(audioFieldName, mediaPlayers[id].Speaker);
			}

			gaplessLoaded = false;
		}

		public void _PlayNext() {
			if (!gaplessSupport) {
				Error("Attempting to access unconfigured gapless player!");
				return;
			}
			Initialize();
			if (!gaplessLoaded) return;
			if (!mediaPlayers[nextID].IsReady) return;
			Log("Stopping " + PlayerNameAndIDString(activeID) + " and playing " + PlayerNameAndIDString(nextID));
			int tradesies = activeID;
			SwitchPlayerInternal(nextID);
			nextID = tradesies;
			mediaPlayers[nextID]._Play();
			SendCallback("_RelayVideoNext");
		}

		public void _Play() {
			Initialize();
			LogPlayer("Playing", activeID);
			mediaPlayers[activeID]._Play();
		}

		public void _Pause() {
			Initialize();
			LogPlayer("Pausing", activeID);
			mediaPlayers[activeID]._Pause();
		}

		public void _Stop() {
			Initialize();
			LogPlayer("Stopping", activeID);
			mediaPlayers[activeID]._Stop();
		}

		public void _PlayURL(VRCUrl url) {
			Initialize();
			LogPlayer("Playing URL: " + (url != null ? url.ToString() : "<NULL>"), activeID);
			mediaPlayers[activeID]._PlayURL(url);
		}

		public void _LoadURL(VRCUrl url) {
			Initialize();
			LogPlayer("Loading URL: " + (url != null ? url.ToString() : "<NULL>"), activeID);
			mediaPlayers[activeID]._LoadURL(url);
		}

		public void _LoadNextURL(VRCUrl url) {
			Initialize();
			if (!gaplessSupport) return;
			if (GetPublicActiveID() != 0) return;
			LogPlayer("Loading Next URL: " + (url != null ? url.ToString() : "<NULL"), nextID);
			if (url == null || url == VRCUrl.Empty) {
				gaplessLoaded = false;
				return;
			}
			mediaPlayers[nextID]._LoadURL(url);
			gaplessLoaded = true;
		}

		public void _RollQueuedPlayer() {}

		public void _StopQueuedPlayer() {}

		public float Time {
			set {
				Initialize();
				LogPlayer("Setting Time: " + value, activeID);
				mediaPlayers[activeID].Time = value;
			}
			get => mediaPlayers[activeID].Time;
		}

		public bool Loop {
			set {
				Initialize();
				LogPlayer("Setting Looping: " + value, activeID);
				mediaPlayers[activeID].Loop = isLooping = value;
			}
			get => mediaPlayers[activeID].Loop;
		}

		public float Volume {
			set {
				Initialize();
				volume = value;
				mediaPlayers[activeID].Volume = blackOutPlayer ? 0f : volume;
			}
			get => volume;
		}
		
		public bool IsReady => mediaPlayers[activeID].IsReady;
		public bool NextReady => gaplessLoaded && mediaPlayers[nextID].IsReady;
		public bool IsPlaying => mediaPlayers[activeID].IsPlaying;
		public bool IsStream => mediaPlayers[activeID].IsStream;
		public float Duration => mediaPlayers[activeID].Duration;
		public int ActivePlayerID => GetPublicActiveID();
		public string ActivePlayer => mediaPlayers[ActivePlayerID].PlayerName;
		public string CurrentPlayerName => mediaPlayers[activeID].PlayerName;

		public string GetPlayerName(int id) { return mediaPlayers[id].PlayerName; }
		private string PlayerNameAndIDString(int id) { return mediaPlayers[id].PlayerName + " (" + id + ")"; }
		private int GetPublicActiveID() { return gaplessSupport && activeID == mediaPlayers.Length - 1 ? 0 : activeID; }
		private void LogPlayer(string message, int id) { Log("(" + mediaPlayers[id].PlayerName + ") " + message); }
		private void Log(string message) { if (enableDebug) Debug.Log(debugPrefix + message, this); }
		private void Error(string message) { Debug.LogError(debugPrefix + message, this); }

		// ------------------- Callback Methods -------------------

		private void SendCallback(string eventName) {
			callback.SetProgramVariable("relayIdentifier", identifier);
			callback.SendCustomEvent(eventName);
		}

		public void _RelayVideoReady() {
			LogPlayer("Ready (" + relayIdentifier + "," + activeID + "," + nextID + ")", relayIdentifier);
			if (gaplessSupport && relayIdentifier == nextID) {
				SendCallback("_RelayVideoQueueReady");
				return;
			}
			if (relayIdentifier != activeID) return;
			SendCallback("_RelayVideoReady");
		}

		public void _RelayVideoEnd() {
			LogPlayer("End (" + relayIdentifier + "," + activeID + "," + nextID + ")", relayIdentifier);
			if (gaplessSupport && gaplessLoaded && GetPublicActiveID() == 0) {
				_PlayNext();
				return;
			}
			if (relayIdentifier != activeID) return;
			SendCallback("_RelayVideoEnd");
		}

		public void _RelayVideoError() {
			LogPlayer("Error (" + relayIdentifier + "," + activeID + "," + nextID + ")", relayIdentifier);
			if (gaplessSupport && gaplessLoaded && relayIdentifier == nextID) {
				callback.SetProgramVariable("relayVideoError", relayVideoError);
				SendCallback("_RelayVideoQueueError");
				return;
			}
			if (relayIdentifier != activeID) return;
			callback.SetProgramVariable("relayVideoError", relayVideoError);
			SendCallback("_RelayVideoError");
		}

		public void _RelayVideoStart() {
			LogPlayer("Start (" + relayIdentifier + "," + activeID + "," + nextID + ")", relayIdentifier);
			if (relayIdentifier != activeID) return;
			SendCallback("_RelayVideoStart");
		}

		public void _RelayVideoPlay() {
			LogPlayer("Play (" + relayIdentifier + "," + activeID + "," + nextID + ")", relayIdentifier);
			if (relayIdentifier != activeID) return;
			SendCallback("_RelayVideoPlay");
		}

		public void _RelayVideoPause() {
			LogPlayer("Pause (" + relayIdentifier + "," + activeID + "," + nextID + ")", relayIdentifier);
			if (relayIdentifier != activeID) return;
			SendCallback("_RelayVideoPause");
		}

		public void _RelayVideoLoop() {
			LogPlayer("Loop (" + relayIdentifier + "," + activeID + "," + nextID + ")", relayIdentifier);
			if (relayIdentifier != activeID) return;
			SendCallback("_RelayVideoLoop");
		}
	}
}
