﻿
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components.Video;
using VRC.SDK3.Video.Components.Base;
using VRC.SDKBase;
using VRC.Udon;

namespace SynPhaxe {
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

		private bool   isLooping;
		private int    nextID = -1;
		private bool   hasAudioCallback;

		private string debugPrefix = "[<color=#1FAF5F>Video Interpolator</color>] ";

		private void Start() {
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
			if (id == activeID) return;
			if (id >= (gaplessSupport ? mediaPlayers.Length - 1 : mediaPlayers.Length)) {
				Error("Switching Players: Identifier (" + id + ") out of range! (" + mediaPlayers.Length + ")");
				return;
			}
			Log("Switching from " + PlayerNameAndIDString(activeID) + " to " + PlayerNameAndIDString(id));
			SwitchPlayerInternal(id);
			if (gaplessSupport && id == 0) nextID = mediaPlayers.Length - 1;
		}

		private void SwitchPlayerInternal(int id) {
			if (id == activeID) return;
			mediaPlayers[activeID]._SetTime(0);
			mediaPlayers[activeID]._Pause();
			mediaPlayers[id]._SetLoop(isLooping);
			mediaPlayers[id]._SetVolume(volume);
			interpolatorMaterial.SetFloat(interpolationProps[activeID], 0);
			interpolatorMaterial.SetFloat(interpolationProps[id], 1);
			if (hasAudioCallback) audioCallback.SetProgramVariable(audioFieldName, mediaPlayers[id].GetSpeaker());
			activeID = id;
		}

		public void _PlayNext() {
			if (!mediaPlayers[nextID].GetReady()) return;
			Log("Stopping " + PlayerNameAndIDString(activeID) + " and playing " + PlayerNameAndIDString(nextID));
			int tradesies = activeID;
			SwitchPlayerInternal(nextID);
			nextID = tradesies;
			mediaPlayers[nextID]._Play();
		}

		public void _Play() {
			LogPlayer("Playing", activeID);
			mediaPlayers[activeID]._Play();
		}

		public void _Pause() {
			LogPlayer("Pausing", activeID);
			mediaPlayers[activeID]._Pause();
		}

		public void _Stop() {
			LogPlayer("Stopping", activeID);
			mediaPlayers[activeID]._Stop();
		}

		public void _PlayURL(VRCUrl url) {
			LogPlayer("Playing URL: " + (url != null ? url.ToString() : "<NULL>"), activeID);
			mediaPlayers[activeID]._PlayURL(url);
		}

		public void _LoadURL(VRCUrl url) {
			LogPlayer("Loading URL: " + (url != null ? url.ToString() : "<NULL>"), activeID);
			mediaPlayers[activeID]._LoadURL(url);
		}

		public void _LoadNextURL(VRCUrl url) {
			if (!gaplessSupport) return;
			if (GetPublicActiveID() != 0) return;
			mediaPlayers[nextID]._LoadURL(url);
		}

		public void _SetTime(float time) {
			LogPlayer("Setting Time: " + time, activeID);
			mediaPlayers[activeID]._SetTime(time);
		}

		public void _SetLoop(bool loop) {
			LogPlayer("Setting Looping: " + loop, activeID);
			mediaPlayers[activeID]._SetLoop(isLooping = loop);
		}

		public void _SetVolume(float volume) {
			mediaPlayers[activeID]._SetVolume(this.volume = volume);
		}

		public bool GetReady() { return mediaPlayers[activeID].GetReady(); }
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
			}
			if (relayIdentifier != activeID) return;
			callback.SetProgramVariable("relayIdentifier", identifier);
			callback.SendCustomEvent("_RelayVideoEnd");
		}

		public void _RelayVideoError() {
			LogPlayer("Error (" + relayIdentifier + "," + activeID + "," + nextID + ")", relayIdentifier);
			if (gaplessSupport && relayIdentifier == nextID) {
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