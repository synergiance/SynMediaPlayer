
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
				float visibility = blackOutPlayer ? 0 : 1;
				interpolatorMaterial.SetFloat(interpolationProps[activeID], visibility);
				mediaPlayers[activeID]._SetVolume(volume * visibility);
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
			mediaPlayers[activeID]._SetVolume(volume);
			interpolatorMaterial.SetFloat(interpolationProps[activeID], 1);
			mediaPlayers[activeID]._SetLoop(isLooping);
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
				mediaPlayers[oldID]._SetTime(0);
				mediaPlayers[oldID]._Pause();
				mediaPlayers[oldID]._Stop();
				mediaPlayers[oldID]._SetVolume(0);
				interpolatorMaterial.SetFloat(interpolationProps[oldID], 0);
			}
			if (id >= 0) {
				mediaPlayers[id]._SetLoop(isLooping);
				float visibility = blackOutPlayer ? 0 : 1;
				mediaPlayers[id]._SetVolume(volume * visibility);
				interpolatorMaterial.SetFloat(interpolationProps[id], visibility);
				if (hasAudioCallback) audioCallback.SetProgramVariable(audioFieldName, mediaPlayers[id].GetSpeaker());
			}
			gaplessLoaded = false;
		}

		public void _PlayNext() {
			if (!gaplessSupport) {
				Error("Attempting to access unconfigured gapless player!");
				return;
			}
			Initialize();
			if (!mediaPlayers[nextID].GetReady()) return;
			Log("Stopping " + PlayerNameAndIDString(activeID) + " and playing " + PlayerNameAndIDString(nextID));
			int tradesies = activeID;
			SwitchPlayerInternal(nextID);
			nextID = tradesies;
			mediaPlayers[nextID]._Play();
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
			mediaPlayers[nextID]._LoadURL(url);
			gaplessLoaded = true;
		}

		public void _SetTime(float time) {
			Initialize();
			LogPlayer("Setting Time: " + time, activeID);
			mediaPlayers[activeID]._SetTime(time);
		}

		public void _SetLoop(bool loop) {
			Initialize();
			LogPlayer("Setting Looping: " + loop, activeID);
			mediaPlayers[activeID]._SetLoop(isLooping = loop);
		}

		public void _SetVolume(float volume) {
			Initialize();
			this.volume = volume;
			mediaPlayers[activeID]._SetVolume(blackOutPlayer ? 0f : volume);
		}

		public bool GetReady() { return mediaPlayers[activeID].GetReady(); }

		public bool GetNextReady() { return gaplessLoaded && mediaPlayers[nextID].GetReady(); }
		public bool GetPlaying() { return mediaPlayers[activeID].GetPlaying(); }
		public bool GetLoop() { return mediaPlayers[activeID].GetLoop(); }
		public float GetTime() { return mediaPlayers[activeID].GetTime(); }
		public float GetDuration() { return mediaPlayers[activeID].GetDuration(); }
		public int GetActiveID() { return GetPublicActiveID(); }
		public string GetActive() { return mediaPlayers[GetPublicActiveID()].GetName(); }
		public float GetVolume() { return volume; }
		public bool GetIsStream() { return mediaPlayers[activeID].GetStream(); }
		public string GetCurrentPlayerName() { return mediaPlayers[activeID].GetName(); }
		public string GetPlayerName(int id) { return mediaPlayers[id].GetName(); }
		private string PlayerNameAndIDString(int id) { return mediaPlayers[id].GetName() + " (" + id + ")"; }

		private int GetPublicActiveID() { return gaplessSupport && activeID == mediaPlayers.Length - 1 ? 0 : activeID; }
		private void LogPlayer(string message, int id) { Log("(" + mediaPlayers[id].GetName() + ") " + message); }
		private void Log(string message) { if (enableDebug) Debug.Log(debugPrefix + message, this); }
		private void Error(string message) { Debug.LogError(debugPrefix + message, this); }

		// ------------------- Callback Methods -------------------

		public void _RelayVideoReady() {
			LogPlayer("Ready (" + relayIdentifier + "," + activeID + "," + nextID + ")", relayIdentifier);
			if (gaplessSupport && relayIdentifier == nextID) {
				callback.SetProgramVariable("relayIdentifier", identifier);
				callback.SendCustomEvent("_RelayVideoQueueReady");
				return;
			}
			if (relayIdentifier != activeID) return;
			callback.SetProgramVariable("relayIdentifier", identifier);
			callback.SendCustomEvent("_RelayVideoReady");
		}

		public void _RelayVideoEnd() {
			LogPlayer("End (" + relayIdentifier + "," + activeID + "," + nextID + ")", relayIdentifier);
			if (gaplessSupport && GetPublicActiveID() == 0) {
				callback.SetProgramVariable("relayIdentifier", identifier);
				callback.SendCustomEvent("_RelayVideoNext");
				_PlayNext();
			} else if (relayIdentifier != activeID) return;
			callback.SetProgramVariable("relayIdentifier", identifier);
			callback.SendCustomEvent("_RelayVideoEnd");
		}

		public void _RelayVideoError() {
			LogPlayer("Error (" + relayIdentifier + "," + activeID + "," + nextID + ")", relayIdentifier);
			if (gaplessSupport && gaplessLoaded && relayIdentifier == nextID) {
				callback.SetProgramVariable("relayIdentifier", identifier);
				callback.SetProgramVariable("relayVideoError", relayVideoError);
				callback.SendCustomEvent("_RelayVideoQueueError");
				return;
			}
			if (relayIdentifier != activeID) return;
			callback.SetProgramVariable("relayIdentifier", identifier);
			callback.SetProgramVariable("relayVideoError", relayVideoError);
			callback.SendCustomEvent("_RelayVideoError");
		}

		public void _RelayVideoStart() {
			LogPlayer("Start (" + relayIdentifier + "," + activeID + "," + nextID + ")", relayIdentifier);
			if (relayIdentifier != activeID) return;
			callback.SetProgramVariable("relayIdentifier", identifier);
			callback.SendCustomEvent("_RelayVideoStart");
		}

		public void _RelayVideoPlay() {
			LogPlayer("Play (" + relayIdentifier + "," + activeID + "," + nextID + ")", relayIdentifier);
			if (relayIdentifier != activeID) return;
			callback.SetProgramVariable("relayIdentifier", identifier);
			callback.SendCustomEvent("_RelayVideoPlay");
		}

		public void _RelayVideoPause() {
			LogPlayer("Pause (" + relayIdentifier + "," + activeID + "," + nextID + ")", relayIdentifier);
			if (relayIdentifier != activeID) return;
			callback.SetProgramVariable("relayIdentifier", identifier);
			callback.SendCustomEvent("_RelayVideoPause");
		}

		public void _RelayVideoLoop() {
			LogPlayer("Loop (" + relayIdentifier + "," + activeID + "," + nextID + ")", relayIdentifier);
			if (relayIdentifier != activeID) return;
			callback.SetProgramVariable("relayIdentifier", identifier);
			callback.SendCustomEvent("_RelayVideoLoop");
		}
	}
}
