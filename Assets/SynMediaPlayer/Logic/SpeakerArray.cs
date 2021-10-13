
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace Synergiance.MediaPlayer {
	public class SpeakerArray : UdonSharpBehaviour {
		[SerializeField] private string[] zoneNames;
		[SerializeField] private AudioSource[] speakers;
		[SerializeField] private int[] speakerZones;

		private float masterVolume;
		private float[] zoneVolumes;
		private int[][] zoneArrays;

		private bool initialized;
		private bool isValid;

		private string debugPrefix = "[<color=#27C048>Speaker Array</color>] ";

		void Start() {
			Initialize();
		}

		private void Initialize() {
			if (initialized) return;
			initialized = true;
			if (speakers.Length != speakerZones.Length) {
				LogError("Malformed data!");
				return;
			}
			zoneArrays = new int[zoneNames.Length][];
			zoneVolumes = new float[zoneNames.Length];
			int i;
			int[] zoneLengths = new int[zoneNames.Length];
			for (i = 0; i < zoneArrays.Length; i++) zoneArrays[i] = new int[speakerZones.Length];
			for (i = 0; i < speakerZones.Length; i++) {
				int zone = speakerZones[i];
				if (zone >= zoneArrays.Length || zone < 0) zone = 0;
				zoneArrays[zone][zoneLengths[zone]++] = i;
			}
			for (i = 0; i < zoneArrays.Length; i++) {
				int[] zoneArray = zoneArrays[i];
				zoneArrays[i] = new int[zoneLengths[i]];
				Array.Copy(zoneArray, zoneArrays[i], zoneLengths[i]);
			}
			isValid = true;
		}

		public void _SetVolume(float volume) {
			Initialize();
			if (!isValid) return;
			masterVolume = volume;
			for (int i = 0; i < zoneNames.Length; i++) ReadjustZoneVolume(i);
		}

		public void _SetZoneVolumeByName(string zone, float volume) {
			Initialize();
			if (!isValid) return;
			int index = Array.IndexOf(zoneNames, zone);
			if (index < 0) {
				LogError("Zone with name " + zone + " does not exist!");
				return;
			}
			_SetZoneVolume(index, volume);
		}

		public void _SetZoneVolume(int zone, float volume) {
			Initialize();
			if (!isValid) return;
			if (zone < 0 || zone >= zoneVolumes.Length) {
				LogError("Volume index " + zone + " out of bounds!");
				return;
			}
			zoneVolumes[zone] = volume;
			ReadjustZoneVolume(zone);
		}

		private void ReadjustZoneVolume(int zone) {
			float volume = zoneVolumes[zone] * masterVolume;
			foreach (int index in zoneArrays[zone]) {
				speakers[index].volume = volume;
			}
		}

		public int GetNumZones() { return isValid ? zoneNames.Length : 0; }
		public float GetVolume() { return isValid ? masterVolume : 0; }
		public float GetZoneVolume(int zone) { return isValid && zone >= 0 && zone < zoneVolumes.Length ? zoneVolumes[zone] : 0; }
		public int GetZoneIndex(string zoneName) { return Array.IndexOf(zoneNames, zoneName); }

		private void LogWarning(string message) { Debug.LogWarning(debugPrefix + message, this); }
		private void LogError(string message) { Debug.LogError(debugPrefix + message, this); }
	}
}
