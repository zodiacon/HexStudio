using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zodiacon.WPF;

namespace Zodiacon.HexEditControl.Commands {
	class AddTextCommand : IAppCommand {
		int _undoIndex;
		ByteBuffer _hexBuffer;

		public string Description => Name;
		public bool Overwrite { get; }
		public List<byte> Data { get; private set; }

		public AddTextCommand(ByteBuffer hexBuffer, byte[] data, bool overwrite) {
			_hexBuffer = hexBuffer;
			Overwrite = overwrite;
			Data = new List<byte>(data);
		}

		public string Name => Overwrite ? "Overwrite" : "Insert";

		public void Execute() {

		}

		public void Undo() {
		}
	}
}
