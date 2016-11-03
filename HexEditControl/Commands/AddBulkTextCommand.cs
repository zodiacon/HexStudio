using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zodiacon.HexEditControl.Commands {
	class AddBulkTextCommand : HexEditCommandBase {
		ByteRange _data;
		bool _overwrite;

		public AddBulkTextCommand(HexEdit hexEdit, ByteRange data, bool overwrite) : base(hexEdit) {
			_data = data;
			_overwrite = overwrite;
		}

		public override void Execute() {
			if (_overwrite)
				HexEdit.Buffer.Overwrite(_data);
			else
				HexEdit.Buffer.Insert(_data);
			Invalidate();
		}

		public override void Undo() {
			HexEdit.Buffer.Delete(_data.Range);
			Invalidate();
		}
	}
}
