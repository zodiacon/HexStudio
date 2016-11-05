using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zodiacon.HexEditControl.Commands {
    class DeleteBulkTextCommand : HexEditCommandBase {
        ByteRange _byteRange;

        public DeleteBulkTextCommand(HexEdit hexEdit, Range range) : base(hexEdit) {
            var data = new byte[range.Count];
            hexEdit.Buffer.GetBytes(range.Start, (int)range.Count, data);
            _byteRange = new ByteRange(range.Start, data);
        }

        public override void Execute() {
            HexEdit.Buffer.Delete(_byteRange.Range);
            Invalidate();
        }

        public override void Undo() {
            HexEdit.Buffer.Insert(_byteRange);
            Invalidate();
        }
    }
}
