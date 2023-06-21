
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Synergiance.MediaPlayer.Diagnostics {
	public class DiagnosticBehaviour : UdonSharpBehaviour {
		/// <summary>
		/// This is the color that the name of the behaviour will show as in the
		/// debug log
		/// </summary>
		protected virtual string DebugColor => ColorToHtmlStringRGB(new Color(0.5f, 0.5f, 0.5f));
		/// <summary>
		/// This is the display name of the behaviour in the debug log
		/// </summary>
		protected virtual string DebugName => "Diagnostic Behaviour";

		[SerializeField] private bool debug = true;
		[SerializeField] private Diagnostics diagnostics;
		private bool hasDiagnostics;
		private bool diagnosticsInitialized;
		private string debugPrefix;
		private int diagnosticsID = -1;

		private bool hasCheckedEditor;
		private bool isEditor;

		/// <summary>
		/// Accessor for whether we're in editor
		/// </summary>
		protected bool IsEditor {
			get {
				if (hasCheckedEditor) return isEditor;
				isEditor = Networking.LocalPlayer == null;
				hasCheckedEditor = true;
				return isEditor;
			}
		}

		/// <summary>
		/// Sets diagnostics mode. Set to true to put behaviour into diagnostic
		/// mode. Just remember to set back to false, since diagnostics are a
		/// heavy performance overhead.
		/// </summary>
		public bool DiagnosticMode { set; protected get; }

		/// <summary>
		/// Retrieves whether the Log method will print anything to screen. You
		/// can use this to spare yourself from performing expensive string
		/// operations when they will not see the light of day.
		/// </summary>
		protected bool DiagnosticLogEnabled => DiagnosticMode || debug;

		/// <summary>
		/// This method will initialize the diagnostic behaviour. It can either
		/// be called from the initialization method, or it will fire the first
		/// time its needed.
		/// </summary>
		protected void InitializeDiagnostics() {
			if (diagnosticsInitialized) return;
			hasDiagnostics = diagnostics != null;
			if (hasDiagnostics) RegisterDiagnostics();
			else debugPrefix = $"[<color={DebugColor}>{DebugName}</color> <color=#B06060>(!)</color>] ";
			diagnosticsInitialized = true;
		}

		private void RegisterDiagnostics() {
			// Register with diagnostic class and get an ID
			diagnosticsID = diagnostics._Register(DebugName, DebugColor, this);
		}

		/// <summary>
		/// This converts <paramref name="_color"/> from a color to a hexadecimal HTML style color code
		/// </summary>
		/// <param name="_color">The color that needs to be converted</param>
		/// <returns>Returns an HTML style color code representing <paramref name="_color"/>.</returns>
		protected string ColorToHtmlStringRGB(Color _color) {
			return $"#{ToByte(_color.r):X2}{ToByte(_color.g):X2}{ToByte(_color.b):X2}";
		}

		/// <summary>
		/// This converts <paramref name="_color"/> from a color to a hexadecimal HTML style color code with alpha
		/// </summary>
		/// <param name="_color">The color that needs to be converted</param>
		/// <returns>Returns an HTML style color code representing <paramref name="_color"/>.</returns>
		protected string ColorToHtmlStringRGBA(Color _color) {
			return $"#{ToByte(_color.r):X2}{ToByte(_color.g):X2}{ToByte(_color.b):X2}{ToByte(_color.a):X2}";
		}

		private byte ToByte(float _f) {
			return (byte)(Mathf.Clamp01(_f) * 255);
		}

		/// <summary>
		/// Internal interface for dumping behaviour state.
		/// </summary>
		/// <returns>A string representing the state of the behaviour.</returns>
		protected virtual string DumpState() {
			return "This behaviour is not coded to support dumping state";
		}

		/// <summary>
		/// Dumps the current state of the behaviour. This will only work while
		/// the behaviour is in diagnostic mode.
		/// </summary>
		/// <param name="_state">String to be returned by behaviour. This will
		/// be null if diagnostics mode is not enabled.</param>
		/// <returns>If diagnostics mode, true, otherwise false.</returns>
		public bool _DumpState(out string _state) {
			if (!DiagnosticMode) {
				_state = null;
				return false;
			}
			_state = DumpState();
			return true;
		}

		// ReSharper disable Unity.PerformanceAnalysis
		/// <summary>
		/// Logs a message to the debug log if the debug checkbox is enabled.
		/// </summary>
		/// <param name="_message">Message to log</param>
		/// <param name="_context">Object to attach as context, uses current object if null</param>
		protected void Log(string _message, Object _context = null) {
			if (!debug && !DiagnosticMode) return;
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
