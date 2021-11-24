
using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Serialization.OdinSerializer.Utilities;

namespace Synergiance.MediaPlayer.UI {
	public class MultiText : UdonSharpBehaviour {
		[SerializeField] private Text[] texts;
		[SerializeField] private string text;
		[SerializeField] private bool shrinkTextAfterLength;
		[SerializeField] private int cutoffLength;
		[SerializeField] private int largeSize = 24;
		[SerializeField] private int smallSize = 16;

		private void Start() { SetTextInternal(text); }
		public void _SetText(string newText) { SetTextInternal(text = newText); }
		public string GetText() { return text; }

		private void SetTextInternal(string newText) {
			string str = string.IsNullOrEmpty(newText) ? "" : newText;
			int size = 10;
			if (shrinkTextAfterLength) size = str.Length > cutoffLength ? smallSize : largeSize;
			foreach (Text uiText in texts) {
				uiText.text = str;
				if (shrinkTextAfterLength) uiText.fontSize = size;
			}
		}
	}
}
