
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

		private string debugPrefix         = "[<color=#27C048>SynMediaPlayer</color>] ";
		
		void Start() {}

		public void _SetVolume(float volume) {
			if (!isValid) return;
			masterVolume = volume;
			for (int i = 0; i < zoneNames.Length; i++) ReadjustZoneVolume(i);
		}

		public void _SetZoneVolumeByName(string zone, float volume) {
			if (!isValid) return;
			int index = Array.IndexOf(zoneNames, zone);
			if (index < 0) {
				// TODO: Log error of Zone Name Not Found
				return;
			}
			_SetZoneVolume(index, volume);
		}

		public void _SetZoneVolume(int zone, float volume) {
			if (!isValid) return;
			if (zone < 0 || zone >= zoneVolumes.Length) {
				// TODO: Log index out of bounds error
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
		public float GetZoneVolume(int zone) { return isValid && zone >= 0 && zone < zoneVolumes.Length ? zoneVolumes[zone] : 0; }
		public int GetZoneIndex(string zoneName) { return Array.IndexOf(zoneNames, zoneName); }
		
		private void LogWarning(string message, UnityEngine.Object context) { Debug.LogWarning(debugPrefix + message, context); }
		private void LogError(string message, UnityEngine.Object context) { Debug.LogError(debugPrefix + message, context); }
	}
}
