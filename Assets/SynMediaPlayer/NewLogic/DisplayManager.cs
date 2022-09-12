using System;
using Synergiance.MediaPlayer.Diagnostics;
using UnityEngine;

namespace Synergiance.MediaPlayer {
	public class DisplayManager : DiagnosticBehaviour {
		private VideoManager videoManager;
		private VideoDisplay[] displays;
		private int[][] sourceDisplayMap;
		private Texture[] sourcePrimaryTextureMap;
		private Texture[] sourceSecondaryTextureMap;

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

		public void _AddSource() {
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
		}

		/// <summary>
		/// Gets audio template from display at index <paramref name="_id"/>
		/// </summary>
		/// <param name="_id">ID of the display to get</param>
		/// <param name="_sources">AudioSource array will be handed back here</param>
		/// <param name="_volume">Relative volume will be set here</param>
		/// <returns>If initialized, returns true on success</returns>
		public bool _GetAudioTemplate(int _id, ref AudioSource[] _sources, float _volume) {
			if (!initialized) {
				LogError("Not initialized!");
				return false;
			}
			if (_id < 0 || _id >= displays.Length) {
				LogError("Display index out of range!");
				return false;
			}
			return displays[_id]._GetAudioTemplate(ref _sources, ref _volume);
		}

		/// <summary>
		/// Registers a display with the display manager
		/// </summary>
		/// <param name="_display">Display to register</param>
		/// <returns>The ID to use for all future calls, -1 if an error occurred</returns>
		public int _RegisterDisplay(VideoDisplay _display) {
			return -1;
		}

		public void _SwitchStream(int _id, int _stream) {
			// TODO: Switch Stream
		}
	}
}
