using Synergiance.MediaPlayer.Diagnostics;
using UnityEngine;

namespace Synergiance.MediaPlayer {
	public class VideoDisplay : DiagnosticBehaviour {
		[SerializeField] private Material videoMaterial;
		[SerializeField] private Renderer videoRenderer;
		[SerializeField] private string textureProperty = "_MainTex";
		[SerializeField] private AudioSource[] audioSources;
		[SerializeField] private Collider activeZone;
		[SerializeField] private DisplayManager displayManager;

		protected override string DebugName => "Video Display";
		protected override string DebugColor => ColorToHtmlStringRGB(new Color(0.5f, 0.1f, 0.65f));

		private bool initialized;
		private bool isValid;

		void Start() {
			Initialize();
		}

		private void Initialize() {
			if (initialized) return;
			CheckValid();
			initialized = true;
		}

		private void CheckValid() {
			if (activeZone == null) {
				LogError("No zone assigned!");
				return;
			}

			if (activeZone.gameObject != gameObject) {
				LogError("Zone not on this game object!");
				return;
			}

			if (displayManager == null) {
				LogError("Display manager not found!");
				return;
			}

			isValid = true;
		}

		public bool _SwitchSource(int _source) {
			return true;
		}
	}
}
