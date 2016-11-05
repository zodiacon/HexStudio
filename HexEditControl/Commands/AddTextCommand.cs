using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zodiacon.WPF;

namespace Zodiacon.HexEditControl.Commands {
    class AddTextCommand : HexEditCommandBase {
        ByteRange _bytes;
        byte[] _data;

        public AddTextCommand(HexEdit hexEdit, long offset, byte[] data, bool overwrite) : base(hexEdit) {
            _bytes = new ByteRange(offset, data);
            Overwrite = overwrite;
            // save old data
            _data = new byte[data.Length];
            hexEdit.Buffer.GetBytes(offset, data.Length, _data);
        }

        public bool Overwrite { get; }

        public override void Execute() {
            if (Overwrite)
                HexEdit.Buffer.Overwrite(_bytes);
            else
                HexEdit.Buffer.Insert(_bytes);
        }

        public override void Undo() {
            HexEdit.Buffer.Delete(_bytes.Range);
        }
    }
}
