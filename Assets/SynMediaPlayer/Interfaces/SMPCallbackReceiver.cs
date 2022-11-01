
using Synergiance.MediaPlayer.Diagnostics;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace Synergiance.MediaPlayer.Interfaces {
	public class SMPCallbackReceiver : DiagnosticBehaviour {
		protected override string DebugName => "Callback Receiver";
		public void SendCallback(CallbackEvent _event, VideoBehaviour _sender) {
			string senderName = (_sender == null ? "Unknown sender" : _sender.name);
			switch (_event) {
				case CallbackEvent.MediaError:
					LogError("Error: " + (_sender == null ? "Unknown sender!" : $"{_sender.lastError} from {senderName}"));
					break;
				default:
					Log("Event: " + _event + " from " + senderName);
					break;
			}
		}
	}
}
