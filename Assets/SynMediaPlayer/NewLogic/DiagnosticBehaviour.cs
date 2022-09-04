
using UdonSharp;
using UnityEngine;

namespace Synergiance.MediaPlayer.Diagnostics {
	public class DiagnosticBehaviour : UdonSharpBehaviour {
		/// <summary>
		/// This is the color that the name of the behaviour will show as in the
		/// debug log
		/// </summary>
		protected virtual Color DebugColor => new Color(0.5f, 0.5f, 0.5f);
		/// <summary>
		/// This is the display name of the behaviour in the debug log
		/// </summary>
		protected virtual string DebugName => "Diagnostic Behaviour";
		private const string DebugTemplate = "[<color=$color>$name</color>] ";
		private const string DebugTplName = "$name";
		private const string DebugTplColor = "$color";

		[SerializeField] private bool debug = true;
		[SerializeField] private Diagnostics diagnostics;
		private bool hasDiagnostics;
		private bool diagnosticsInitialized;
		private string debugPrefix;
		private int diagnosticsID = -1;

		/// <summary>
		/// This method will initialize the diagnostic behaviour. It can either
		/// be called from the initialization method, or it will fire the first
		/// time its needed.
		/// </summary>
		protected void InitializeDiagnostics() {
			if (diagnosticsInitialized) return;
			hasDiagnostics = diagnostics != null;
			if (hasDiagnostics) RegisterDiagnostics();
			else debugPrefix = DebugTemplate
				.Replace(DebugTplName, DebugName)
				.Replace(DebugTplColor, ColorToHtml(DebugColor));
			diagnosticsInitialized = true;
		}

		private void RegisterDiagnostics() {
			// Register with diagnostic class and get an ID
			diagnosticsID = diagnostics._Register(DebugName, ColorToHtml(DebugColor));
		}

		private string ColorToHtml(Color _color) {
			return $"#{ToByte(_color.r):X2}{ToByte(_color.g):X2}{ToByte(_color.b):X2}";
		}

		private byte ToByte(float _f) {
			return (byte)(Mathf.Clamp01(_f) * 255);
		}

		/// <summary>
		/// Logs a message to the debug log if the debug checkbox is enabled.
		/// </summary>
		/// <param name="_message">Message to log</param>
		/// <param name="_context">Object to attach as context, uses current object if null</param>
		protected void Log(string _message, Object _context = null) {
			if (!debug) return;
			InitializeDiagnostics();
			if (_context == null) _context = this;
			if (hasDiagnostics) {
				diagnostics._Log(diagnosticsID, _message, _context);
				return;
			}
			Debug.Log(debugPrefix + _message, _context);
		}

		/// <summary>
		/// Logs a warning message to the debug log.
		/// </summary>
		/// <param name="_message">Message to log</param>
		/// <param name="_context">Object to attach as context, uses current object if null</param>
		protected void LogWarning(string _message, Object _context = null) {
			InitializeDiagnostics();
			if (_context == null) _context = this;
			if (hasDiagnostics) {
				diagnostics._LogWarning(diagnosticsID, _message, _context);
				return;
			}
			Debug.LogWarning(debugPrefix + _message, _context);
		}

		/// <summary>
		/// Logs an error message to the debug log.
		/// </summary>
		/// <param name="_message">Message to log</param>
		/// <param name="_context">Object to attach as context, uses current object if null</param>
		protected void LogError(string _message, Object _context = null) {
			InitializeDiagnostics();
			if (_context == null) _context = this;
			if (hasDiagnostics) {
				diagnostics._LogError(diagnosticsID, _message, _context);
				return;
			}
			Debug.LogError(debugPrefix + _message, _context);
		}
	}
}
