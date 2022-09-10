
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
		private const string DiagnosticPrefix = "[<color=#808080>Diagnostics</color>]";

		private int numRegistered;

		private bool initialized;

		private void Initialize() {
			if (initialized) return;
			names = new string[ArrayIncrement];
			colors = new string[ArrayIncrement];
			prefixes = new string[ArrayIncrement];
			initialized = true;
			if (debug) Debug.Log($"{DiagnosticPrefix} Initialized arrays with length {ArrayIncrement}");
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
			if (debug) Debug.Log($"{DiagnosticPrefix} Expanded arrays from {oldLength} to {newLength}");
		}

		/// <summary>
		/// Register a new behaviour with the diagnostics behaviour. This gives
		/// the behaviour an ID to load into the diagnostic behaviour, and the
		/// diagnostics behaviour takes all the load off.
		/// </summary>
		/// <param name="_name">The name of the behaviour to be registered. This
		/// will show up to the left of any diagnostic messages sent with this
		/// registration id.</param>
		/// <param name="_color">The HTML format color code that the name will
		/// show up as when sending diagnostic messages.</param>
		/// <returns>The registration ID of this particular behaviour.</returns>
		public int _Register(string _name, string _color) {
			Initialize();
			if (numRegistered >= prefixes.Length)
				ExpandArrays();
			int id = numRegistered++;
			names[id] = _name;
			colors[id] = _color;
			prefixes[id] = $"[<color={_color}>{_name}</color> <color=#808080>({id.ToString()})</color>] ";
			if (debug) Debug.Log($"{DiagnosticPrefix} Added {_name} with color {_color} under id #{id}");
			return id;
		}

		/// <summary>
		/// Logs a message to the debug log if the debug checkbox is enabled.
		/// </summary>
		/// <param name="_id">ID to use for the log. This assigns a name and color.</param>
		/// <param name="_message">Message to log</param>
		/// <param name="_context">Object to attach as context, uses current object if null</param>
		public void _Log(int _id, string _message, Object _context = null) {
			if (!debug) return;
			LogType(0, _id, _message, _context);
		}

		/// <summary>
		/// Logs a warning message to the debug log.
		/// </summary>
		/// <param name="_id">ID to use for the log. This assigns a name and color.</param>
		/// <param name="_message">Message to log</param>
		/// <param name="_context">Object to attach as context, uses current object if null</param>
		public void _LogWarning(int _id, string _message, Object _context = null) {
			LogType(1, _id, _message, _context);
		}

		/// <summary>
		/// Logs an error message to the debug log.
		/// </summary>
		/// <param name="_id">ID to use for the log. This assigns a name and color.</param>
		/// <param name="_message">Message to log</param>
		/// <param name="_context">Object to attach as context, uses current object if null</param>
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
