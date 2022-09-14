using System;
using Synergiance.MediaPlayer.Diagnostics;
using UnityEngine;

namespace Synergiance.MediaPlayer {
	public class DisplayManager : DiagnosticBehaviour {
		private VideoManager videoManager;
		private VideoDisplay[] displays;
		private string[] displayNames;
		private int[][] sourceDisplayMap;
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

			if (sourceDisplayMap != null) {
				int[][] mapBak = sourceDisplayMap;
				sourceDisplayMap = new int[mapBak.Length + 1][];
				Array.Copy(mapBak, sourceDisplayMap, mapBak.Length);
			} else {
				sourceDisplayMap = new int[1][];
			}

			// TODO: Search default display sources for name string
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
			// TODO: Insert into display list
			// TODO: Search through display names for the default source
			return -1;
		}

		public void _SwitchSource(int _id, int _source) {
			// TODO: Switch Stream
		}
	}
}
