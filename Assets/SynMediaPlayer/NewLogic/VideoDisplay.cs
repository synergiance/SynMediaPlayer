using Synergiance.MediaPlayer.Diagnostics;
using UnityEngine;

namespace Synergiance.MediaPlayer {
	public class VideoDisplay : DiagnosticBehaviour {
		[SerializeField] private Material videoMaterial;
		[SerializeField] private Renderer videoRenderer;
		[SerializeField] private string textureProperty = "_MainTex";
		[SerializeField] private AudioSource[] audioSources;
		[SerializeField] private Collider activeZone;

		protected override string DebugName => "Security Manager";
		protected override string DebugColor => ColorToHtmlStringRGB(new Color(0.5f, 0.1f, 0.65f));

		private bool initialized;
		void Start() {}

		private void Initialize() {
			if (initialized) return;
			// Initialization Code
			initialized = true;
		}
	}
}
