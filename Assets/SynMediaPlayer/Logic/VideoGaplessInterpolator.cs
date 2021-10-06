
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components.Video;
using VRC.SDKBase;
using VRC.Udon;

namespace Synergiance.MediaPlayer {
	public class VideoGaplessInterpolator : UdonSharpBehaviour {

		// Settings
		[SerializeField]  private VideoInterpolator  alphaInterpolator;
		[SerializeField]  private VideoInterpolator  betaInterpolator;
		[SerializeField]  private float              volume = 0.5f;
		[SerializeField]  private int                activeID;
		[SerializeField]  private UdonSharpBehaviour callback;

		// Public Callback Variables
		[HideInInspector] public  int                relayIdentifier;
		[HideInInspector] public  VideoError         relayVideoError;

		private bool                isLooping;
		private int                 gaplessID;
		private int                 nextID;
		private VideoInterpolator[] mediaPlayers;

		private string debugPrefix = "[<color=#1FAF7F>Video Gapless Interpolator</color>] ";

		private void Start() {
			mediaPlayers = new VideoInterpolator[2];
			mediaPlayers[0] = alphaInterpolator;
			mediaPlayers[1] = betaInterpolator;
			mediaPlayers[gaplessID]._SetVolume(volume);
			mediaPlayers[gaplessID]._SetLoop(isLooping);
		}

		public void _SwitchPlayer(int id) {
			mediaPlayers[gaplessID]._SwitchPlayer(nextID = activeID = id);
		}

		public void _Play() {
			mediaPlayers[gaplessID]._Play();
		}

		public void _Pause() {
			mediaPlayers[gaplessID]._Pause();
		}

		public void _Stop() {
			mediaPlayers[gaplessID]._Stop();
		}

		public void _PlayURL(VRCUrl url) {
			mediaPlayers[gaplessID]._PlayURL(url);
		}

		public void _LoadURL(VRCUrl url) {
			mediaPlayers[gaplessID]._LoadURL(url);
		}

		public void _LoadNextURL(VRCUrl url) {
			mediaPlayers[gaplessID^1]._LoadURL(url);
		}

		public void _SetTime(float time) {
			mediaPlayers[gaplessID]._SetTime(time);
		}

		public void _SetLoop(bool loop) {
			mediaPlayers[gaplessID]._SetLoop(isLooping = loop);
		}

		public void _SetVolume(float volume) {
			mediaPlayers[gaplessID]._SetVolume(this.volume = volume);
		}

		public bool GetReady() { return mediaPlayers[gaplessID].GetReady(); }
		public bool GetPlaying() { return mediaPlayers[gaplessID].GetPlaying(); }
		public bool GetLoop() { return mediaPlayers[gaplessID].GetLoop(); }
		public float GetTime() { return mediaPlayers[gaplessID].GetTime(); }
		public float GetDuration() { return mediaPlayers[gaplessID].GetDuration(); }
		public int GetActiveID() { return activeID; }
		public string GetActive() { return mediaPlayers[gaplessID].GetActive(); }
		public float GetVolume() { return volume; }
		public bool GetIsStream() { return mediaPlayers[gaplessID].GetIsStream(); }
		public string GetCurrentPlayerName() { return mediaPlayers[gaplessID].GetCurrentPlayerName(); }
		public string GetPlayerName(int id) { return mediaPlayers[gaplessID].GetPlayerName(id); }
		private void Log(string message) { Debug.Log(debugPrefix + message, this); }
		private void Error(string message) { Debug.LogError(debugPrefix + message, this); }

		// ------------------- Callback Methods -------------------

		public void _RelayVideoReady() {
			if (relayIdentifier > 1) return;
			callback.SetProgramVariable("relayIdentifier", 0);
			callback.SendCustomEvent(relayIdentifier == gaplessID ? "_RelayVideoReady" : "_RelayVideoQueueReady");
		}

		public void _RelayVideoEnd() {
			if (relayIdentifier != activeID) return;
			callback.SetProgramVariable("relayIdentifier", 0);
			callback.SendCustomEvent("_RelayVideoEnd");
		}

		public void _RelayVideoError() {
			if (relayIdentifier != activeID) return;
			callback.SetProgramVariable("relayIdentifier", 0);
			callback.SetProgramVariable("relayVideoError", relayVideoError);
			callback.SendCustomEvent("_RelayVideoError");
		}

		public void _RelayVideoStart() {
			if (relayIdentifier != activeID) return;
			callback.SetProgramVariable("relayIdentifier", 0);
			callback.SendCustomEvent("_RelayVideoStart");
		}

		public void _RelayVideoPlay() {
			if (relayIdentifier != activeID) return;
			callback.SetProgramVariable("relayIdentifier", 0);
			callback.SendCustomEvent("_RelayVideoPlay");
		}

		public void _RelayVideoPause() {
			if (relayIdentifier != activeID) return;
			callback.SetProgramVariable("relayIdentifier", 0);
			callback.SendCustomEvent("_RelayVideoPause");
		}

		public void _RelayVideoLoop() {
			if (relayIdentifier != activeID) return;
			callback.SetProgramVariable("relayIdentifier", 0);
			callback.SendCustomEvent("_RelayVideoLoop");
		}
	}
}
