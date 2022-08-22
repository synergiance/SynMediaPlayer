
using System;
using UdonSharp;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Synergiance.MediaPlayer.Diagnostics {
	public class Diagnostics : UdonSharpBehaviour {
		[SerializeField] private bool debug;

		private string[] names;
		private string[] colors;
		private string[] prefixes;

		private const int ArrayIncrement = 16;
		private const string Template = "[<color=$color>$name</color> <color=#808080>($id)</color>] ";
		private const string TplName = "$name";
		private const string TplColor = "$color";
		private const string TplID = "$id";

		private int numRegistered;

		private bool initialized;

		private void Initialize() {
			if (initialized) return;
			names = new string[ArrayIncrement];
			colors = new string[ArrayIncrement];
			prefixes = new string[ArrayIncrement];
			initialized = true;
			if (debug) Debug.Log("[Diagnostics] Initialized arrays with length " + ArrayIncrement);
		}

		private void ExpandArrays() {
			int oldLength = prefixes.Length;
			int newLength = oldLength + ArrayIncrement;
			string[] tmp = new string[newLength];
			Array.Copy(names, tmp, oldLength);
			names = tmp;
			tmp = new string[newLength];
			Array.Copy(colors, tmp, oldLength);
			colors = tmp;
			tmp = new string[newLength];
			Array.Copy(prefixes, tmp, oldLength);
			prefixes = tmp;
			if (debug) Debug.Log("[Diagnostics] Expanded arrays from " + oldLength + " to " + newLength);
		}

		public int _Register(string _name, string _color) {
			Initialize();
			if (numRegistered >= prefixes.Length)
				ExpandArrays();
			int id = numRegistered++;
			names[id] = _name;
			colors[id] = _color;
			prefixes[id] = Template
				.Replace(TplName, _name)
				.Replace(TplColor, _color)
				.Replace(TplID, id.ToString());
			if (debug) Debug.Log("[Diagnostics] Added " + _name + " with color " + _color + " under id #" + id);
			return id;
		}

		public void _Log(int _id, string _message, Object _context = null) {
			if (!debug) return;
			LogType(0, _id, _message, _context);
		}

		public void _LogWarning(int _id, string _message, Object _context = null) {
			LogType(1, _id, _message, _context);
		}

		public void _LogError(int _id, string _message, Object _context = null) {
			LogType(2, _id, _message, _context);
		}

		private void LogType(int _type, int _id, string _message, Object _context) {
			Initialize();
			if (_id >= numRegistered || _id < 0) return;
			string message = prefixes[_id] + _message;
			switch (_type) {
				case 0:
					Debug.Log(message, _context);
					break;
				case 1:
					Debug.LogWarning(message, _context);
					break;
				case 2:
					Debug.LogError(message, _context);
					break;
			}
		}
	}
}
