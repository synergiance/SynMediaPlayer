
using UdonSharp;
using UnityEngine;

namespace Synergiance.MediaPlayer.Diagnostics {
	public class DiagnosticBehaviour : UdonSharpBehaviour {
		protected virtual string DebugColor => "#808080";
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

		protected void InitializeDiagnostics() {
			if (diagnosticsInitialized) return;
			hasDiagnostics = diagnostics != null;
			if (hasDiagnostics) RegisterDiagnostics();
			else debugPrefix = DebugTemplate
				.Replace(DebugTplName, DebugName)
				.Replace(DebugTplColor, DebugColor);
			diagnosticsInitialized = true;
		}

		private void RegisterDiagnostics() {
			// Register with diagnostic class and get an ID
			diagnosticsID = diagnostics._Register(DebugName, DebugColor);
		}

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

		protected void LogWarning(string _message, Object _context = null) {
			InitializeDiagnostics();
			if (_context == null) _context = this;
			if (hasDiagnostics) {
				diagnostics._LogWarning(diagnosticsID, _message, _context);
				return;
			}
			Debug.LogWarning(debugPrefix + _message, _context);
		}

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
