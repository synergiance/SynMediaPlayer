﻿using System;
using Synergiance.MediaPlayer.Diagnostics;
using UdonSharp;
using UnityEngine;

namespace Synergiance.MediaPlayer {
	/// <summary>
	/// Maintains a set of displays and their links
	/// </summary>
	[DefaultExecutionOrder(-1), UdonBehaviourSyncMode(BehaviourSyncMode.None)]
	public class DisplayManager : DiagnosticBehaviour {
		private VideoManager videoManager;
		private VideoDisplay[] displays;
		private string[] sourceNames;
		private int[][] sourceDisplayMap;
		private int[] displaySourceMap;
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

			Log($"Adding source \"{_name}\" to display manager");

			if (sourceDisplayMap != null) {
				int[][] mapBak = sourceDisplayMap;
				string[] nameBak = sourceNames;
				sourceDisplayMap = new int[mapBak.Length + 1][];
				sourceNames = new string[sourceDisplayMap.Length];
				Array.Copy(mapBak, sourceDisplayMap, mapBak.Length);
				Array.Copy(nameBak, sourceNames, nameBak.Length);
			} else {
				sourceDisplayMap = new int[1][];
				sourceNames = new string[1];
			}

			int sourceIndex = sourceDisplayMap.Length - 1;
			sourceNames[sourceIndex] = _name;

			if (defaultDisplaySources == null) return;
			for (int i = 0; i < defaultDisplaySources.Length; i++) {
				if (!string.Equals(defaultDisplaySources[i], _name)) continue;
				Log($"Switching display {i}'s default display to {sourceIndex}");
				displays[i]._SetDefaultSourceId(sourceIndex);
			}
		}

		/// <summary>
		/// Gets audio template from display at index <paramref name="_id"/>
		/// </summary>
		/// <param name="_id">ID of the display to get</param>
		/// <param name="_sources">AudioSource array will be handed back here</param>
		/// <param name="_volume">Relative volume will be set here</param>
		/// <returns>If initialized, returns true on success</returns>
		public bool _GetAudioTemplate(int _id, out AudioSource[] _sources, out float _volume) {
			if (!initialized) {
				LogError("Not initialized!");
				_sources = null;
				_volume = 0;
				return false;
			}

			if (_id < 0 || _id >= sourceDisplayMap.Length) {
				LogError("Display index out of range!");
				_sources = null;
				_volume = 0;
				return false;
			}

			return displays[_id]._GetAudioTemplate(out _sources, out _volume);
		}

		/// <summary>
		/// Registers a display with the display manager
		/// </summary>
		/// <param name="_display">Display to register</param>
		/// <param name="_defaultSource">Default source to use for this display</param>
		/// <returns>The ID to use for all future calls, -1 if an error occurred</returns>
		public int _RegisterDisplay(VideoDisplay _display, string _defaultSource) {
			if (_display == null) {
				LogError("Must pass in a display to register!");
				return -1;
			}

			if (string.IsNullOrWhiteSpace(_defaultSource)) {
				LogError("Default source cannot be empty!");
				return -1;
			}

			if (displays == null || displays.Length == 0) {
				displays = new VideoDisplay[1];
				defaultDisplaySources = new string[1];
				displaySourceMap = new int[1];
			} else {
				VideoDisplay[] tmpDisplays = new VideoDisplay[displays.Length + 1];
				string[] tmpDefaultDisplaySources = new string[tmpDisplays.Length];
				int[] tmpDisplaySourceMap = new int[tmpDisplays.Length];
				Array.Copy(displays, tmpDisplays, displays.Length);
				Array.Copy(defaultDisplaySources, tmpDefaultDisplaySources, displays.Length);
				Array.Copy(displaySourceMap, tmpDisplaySourceMap, displays.Length);
				displays = tmpDisplays;
				defaultDisplaySources = tmpDefaultDisplaySources;
				displaySourceMap = tmpDisplaySourceMap;
			}

			int displayIndex = displays.Length - 1;
			displays[displayIndex] = _display;
			defaultDisplaySources[displayIndex] = _defaultSource;
			displaySourceMap[displayIndex] = -1;

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
	}
}
