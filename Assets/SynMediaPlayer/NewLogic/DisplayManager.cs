using System;
using Synergiance.MediaPlayer.Diagnostics;
using UdonSharp;
using UnityEngine;

namespace Synergiance.MediaPlayer {
	/// <summary>
	/// Maintains a set of displays and their links
	/// </summary>
	[DefaultExecutionOrder(-20), UdonBehaviourSyncMode(BehaviourSyncMode.None)]
	public class DisplayManager : DiagnosticBehaviour {
		private VideoManager videoManager;
		private VideoDisplay[] displays;
		private string[] sourceNames;
		private int[][] sourceDisplayMap;
		private int[] displaySourceMap;
		private float[] displayWeights;
		private Texture[] sourcePrimaryTextureMap;
		private Texture[] sourceSecondaryTextureMap;
		private string[] defaultDisplaySources;

		protected override string DebugName => "Display Manager";
		protected override string DebugColor => ColorToHtmlStringRGB(new Color(0.4f, 0.1f, 0.5f));

		private bool initialized;

		public void _Initialize(VideoManager _videoManager) {
			if (initialized) {
				LogError("Already initialized!");
				return;
			}

			if (_videoManager == null) {
				LogError("Cannot initialize with null video manager!");
				return;
			}

			videoManager = _videoManager;
			initialized = true;
		}

		/// <summary>
		/// Adds a source to the display map
		/// </summary>
		/// <param name="_name">Name of the source</param>
		public void _AddSource(string _name) {
			if (!initialized) {
				LogError("Cannot add source, not initialized!");
				return;
			}

			if (string.IsNullOrWhiteSpace(_name)) {
				LogError("Name cannot be empty!");
				return;
			}

			Log($"Adding source \"{_name}\" to display manager");

			if (sourceDisplayMap != null) {
				Log("Expanding source display map arrays");
				int[][] mapBak = sourceDisplayMap;
				string[] nameBak = sourceNames;
				sourceDisplayMap = new int[mapBak.Length + 1][];
				sourceNames = new string[sourceDisplayMap.Length];
				Array.Copy(mapBak, sourceDisplayMap, mapBak.Length);
				Array.Copy(nameBak, sourceNames, nameBak.Length);
			} else {
				Log("Creating source display map arrays");
				sourceDisplayMap = new int[1][];
				sourceNames = new string[1];
			}

			int sourceIndex = sourceDisplayMap.Length - 1;
			sourceNames[sourceIndex] = _name;
			Log("Source added at index " + sourceIndex);

			if (defaultDisplaySources == null) return;
			for (int i = 0; i < defaultDisplaySources.Length; i++) {
				if (!string.Equals(defaultDisplaySources[i], _name)) continue;
				Log($"Switching display {i}'s default display to {sourceIndex}");
				displays[i]._SetDefaultSourceId(sourceIndex);
			}
		}

		/// <summary>
		/// Gets optimal audio template from source at index <paramref name="_source"/>
		/// </summary>
		/// <param name="_source">ID of the source to get</param>
		/// <param name="_templateSources">AudioSource array will be handed back here</param>
		/// <param name="_templateVolume">Relative volume will be set here</param>
		/// <returns>If initialized, returns true on success</returns>
		public bool _GetAudioTemplate(int _source, out AudioSource[] _templateSources, out float _templateVolume) {
			if (!initialized) {
				LogError("Not initialized!");
				_templateSources = null;
				_templateVolume = 0;
				return false;
			}

			if (_source < 0 || _source >= sourceDisplayMap.Length) {
				LogError("Display index out of range!");
				_templateSources = null;
				_templateVolume = 0;
				return false;
			}

			if (sourceDisplayMap[_source] == null || sourceDisplayMap[_source].Length == 0) {
				Log("Source has no displays");
				_templateSources = null;
				_templateVolume = 0;
				return false;
			}

			float bestWeight = Single.NegativeInfinity;
			int bestDisplay = -1;
			foreach (int display in sourceDisplayMap[_source]) {
				if (display < 0) continue;

				if (display >= displays.Length) {
					LogWarning($"Display {display} in map is invalid!");
					continue;
				}

				if (displayWeights[display] < bestWeight) continue;

				if (!displays[display].HasAudio) continue;

				if (!displays[display].AudioActive) continue;

				bestDisplay = display;
				bestWeight = displayWeights[display];
			}

			if (bestDisplay < 0) {
				Log("No capable displays found");
				_templateSources = null;
				_templateVolume = 0;
				return false;
			}

			return displays[bestDisplay]._GetAudioTemplate(out _templateSources, out _templateVolume);
		}

		/// <summary>
		/// Registers a display with the display manager
		/// </summary>
		/// <param name="_display">Display to register</param>
		/// <param name="_defaultSource">Default source to use for this display</param>
		/// <param name="_priority">Audio priority. Higher values will take precedence.</param>
		/// <returns>The ID to use for all future calls, -1 if an error occurred</returns>
		public int _RegisterDisplay(VideoDisplay _display, string _defaultSource, float _priority) {
			if (_display == null) {
				LogError("Must pass in a display to register!");
				return -1;
			}

			if (string.IsNullOrWhiteSpace(_defaultSource)) {
				LogError("Default source cannot be empty!");
				return -1;
			}

			if (displays == null || displays.Length == 0) {
				Log("Creating display arrays");
				displays = new VideoDisplay[1];
				defaultDisplaySources = new string[1];
				displaySourceMap = new int[1];
				displayWeights = new float[1];
			} else {
				for (int i = 0; i < displays.Length; i++) {
					if (displays[i] != _display) continue;
					LogError("Display already registered!");
					return i;
				}

				Log("Expanding display arrays");
				VideoDisplay[] tmpDisplays = new VideoDisplay[displays.Length + 1];
				string[] tmpDefaultDisplaySources = new string[tmpDisplays.Length];
				int[] tmpDisplaySourceMap = new int[tmpDisplays.Length];
				float[] tmpDisplayWeights = new float[tmpDisplays.Length];
				Array.Copy(displays, tmpDisplays, displays.Length);
				Array.Copy(defaultDisplaySources, tmpDefaultDisplaySources, displays.Length);
				Array.Copy(displaySourceMap, tmpDisplaySourceMap, displays.Length);
				Array.Copy(displayWeights, tmpDisplayWeights, displays.Length);
				displays = tmpDisplays;
				defaultDisplaySources = tmpDefaultDisplaySources;
				displaySourceMap = tmpDisplaySourceMap;
				displayWeights = tmpDisplayWeights;
			}

			int displayIndex = displays.Length - 1;
			Log("Registering display to index " + displayIndex);
			displays[displayIndex] = _display;
			defaultDisplaySources[displayIndex] = _defaultSource;
			displaySourceMap[displayIndex] = -1;
			displayWeights[displayIndex] = _priority;

			if (sourceNames == null) return displayIndex;
			for (int i = 0; i < sourceNames.Length; i++) {
				if (string.Equals(sourceNames[i], _defaultSource)) {
					Log($"Found default source at index {i}");
					displays[displayIndex]._SetDefaultSourceId(i);
					break;
				}
			}
			return displayIndex;
		}

		/// <summary>
		/// Switches a display from one source to another
		/// </summary>
		/// <param name="_id">ID of the display we're switching</param>
		/// <param name="_source">Source to switch to</param>
		public bool _SwitchSource(int _id, int _source) {
			if (!initialized) {
				LogError("Display manager not initialized!");
				return false;
			}

			if (_id < 0 || displays == null || _id >= displays.Length) {
				LogError("Display ID does not exist!");
				return false;
			}

			if (_source < 0 || sourceNames == null || _source >= sourceNames.Length) {
				LogError("Source does not exist!");
				return false;
			}

			if (displaySourceMap[_id] == _source) {
				LogWarning("Already using this source!");
				return true;
			}

			int previousSource = displaySourceMap[_id];
			displaySourceMap[_id] = _source;

			if (previousSource >= 0 && sourceDisplayMap[previousSource] != null) {
				for (int i = 0; i < sourceDisplayMap[previousSource].Length; i++) {
					if (sourceDisplayMap[previousSource][i] == _id)
						sourceDisplayMap[previousSource][i] = -1;
				}
			}

			if (sourceDisplayMap[_source] == null || sourceDisplayMap[_source].Length == 0) {
				sourceDisplayMap[_source] = new int[1];
				sourceDisplayMap[_source][0] = _id;
				return true;
			}

			for (int i = 0; i < sourceDisplayMap[_source].Length; i++) {
				if (sourceDisplayMap[_source][i] >= 0) continue;
				sourceDisplayMap[_source][i] = _id;
				return true;
			}

			int[] tmpDisplayMap = new int[sourceDisplayMap[_source].Length + 1];
			Array.Copy(sourceDisplayMap[_source], tmpDisplayMap, sourceDisplayMap[_source].Length);
			tmpDisplayMap[sourceDisplayMap[_source].Length] = _id;
			sourceDisplayMap[_source] = tmpDisplayMap;
			return true;
		}

		/// <summary>
		/// Updates the texture of displays bound to source
		/// </summary>
		/// <param name="_source">Source ID to use for updating</param>
		/// <param name="_texture">Texture to submit to displays</param>
		/// <param name="_type">Specifies which texture to update on the display</param>
		public bool _SetVideoTexture(int _source, Texture _texture, int _type = 0) {
			if (!initialized) {
				LogError("Not initialized!");
				return false;
			}

			if (_source < 0 || sourceDisplayMap == null || _source >= sourceDisplayMap.Length) {
				LogError("Source does not exist!");
				return false;
			}

			int[] displayMap = sourceDisplayMap[_source];
			if (displayMap == null || displayMap.Length == 0) {
				Log("No displays bound to source, returning early");
				return false;
			}

			Log($"Updating textures for source {_source}");
			for (int i = 0; i < displayMap.Length; i++) {
				if (displayMap[i] < 0) continue;

				if (displayMap[i] >= displays.Length) {
					LogWarning($"Display {displayMap[i]} is out of range!");
					displayMap[i] = -1;
					continue;
				}

				Log($"Updating texture on display {displayMap[i]}");
				displays[displayMap[i]]._SetVideoTexture(_type, _texture);
			}
			return true;
		}

		/// <summary>
		/// Event receiver for when an display audio zone is activated or
		/// deactivated.
		/// </summary>
		/// <param name="_id">Display ID</param>
		/// <param name="_active">Whether zone is activating or deactivating</param>
		public void _AudioZoneActiveCallback(int _id) {
			if (!initialized) {
				LogError("Not initialized!");
				return;
			}

			if (_id < 0 || displays == null || _id >= displays.Length) {
				LogError($"Display {_id} is out of range");
				return;
			}

			videoManager._GetUpdatedAudioTemplate(displaySourceMap[_id]);
		}
	}
}
