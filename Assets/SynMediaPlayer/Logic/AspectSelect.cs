
using Synergiance.MediaPlayer.UI;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace Synergiance.MediaPlayer.UI {
	public class AspectSelect : UdonSharpBehaviour {
		[SerializeField] private SliderTypeSelect aspectSlider;
		[SerializeField] private MultiText aspectText;
		[SerializeField] private Material screenMaterial;
		[SerializeField] private string propertyName = "_Ratio";
		[SerializeField] private string[] aspectRatioNames;
		[SerializeField] private float[] aspectRatios;
		[SerializeField] private bool enableDebug;

		[HideInInspector] public int selectedAspect;

		private string debugPrefix = "[<color=#2080C0>SMP Aspect Select</color>] ";

		void Start() {
			if (!screenMaterial) LogWarning("Screen material missing!", this);
			if (!aspectText) LogWarning("Aspect Text Field not set!", this);
			if (aspectRatios == null) LogError("Aspect ratio list is missing! This behaviour will not function", this);
			if (aspectRatioNames == null) LogWarning("Aspect ratio names are missing! Unable to display aspect ratio anywhere!", this);
			if (aspectRatios != null && aspectRatioNames != null && aspectRatios.Length != aspectRatioNames.Length)
				LogWarning("Aspect ratio name list should be the same length as aspect ratio list!", this);
			if (!aspectSlider) return;
			SwitchAspectInternal(aspectSlider.CurrentType);
		}

		public void _SwitchAspect() {
			SwitchAspectInternal(selectedAspect);
		}

		private void SwitchAspectInternal(int newAspect) {
			if (!screenMaterial) {
				LogError("Material not set!", this);
				return;
			}
			if (aspectRatios == null) {
				LogError("Aspect ratio list is null!", this);
				return;
			}
			if (newAspect < 0 || newAspect >= aspectRatios.Length) {
				LogError("Aspect Index out of bounds!", this);
				return;
			}
			float newRatio = aspectRatios[newAspect];
			Log("Setting aspect to: " + newRatio, this);
			screenMaterial.SetFloat(propertyName, newRatio);
			if (!aspectText) return;
			if (aspectRatioNames == null) return;
			if (newAspect > aspectRatioNames.Length) return;
			aspectText._SetText(aspectRatioNames[newAspect]);
		}

		// ----------------- Debug Helper Methods -----------------
		private void Log(string message, Object context) { if (enableDebug) Debug.Log(debugPrefix + message, context); }
		private void LogWarning(string message, Object context) { Debug.LogWarning(debugPrefix + message, context); }
		private void LogError(string message, Object context) { Debug.LogError(debugPrefix + message, context); }
	}
}
