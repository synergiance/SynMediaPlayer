
using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

namespace Synergiance.MediaPlayer.UI {
	public class MultiText : UdonSharpBehaviour {
		[SerializeField] private Text[] texts;
		[SerializeField] private string text;

		private void Start() { SetTextInternal(text); }
		public void _SetText(string newText) { SetTextInternal(text = newText); }
		public string GetText() { return text; }
		private void SetTextInternal(string newText) { foreach (Text uiText in texts) uiText.text = newText; }
	}
}
