using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Zodiacon.WPF;

namespace Zodiacon.HexEditControl.Commands {
    class AddByteCommand : HexEditCommandBase {
        public bool Overwrite { get; }
        byte _data;
        byte[] _oldData = new byte[1];
        int _inputIndex, _wordIndex, _wordSize;
        long _offset;


        public AddByteCommand(HexEdit hexEdit, long offset, byte data, bool overwrite, int inputIndex, int wordIndex, int wordSize) : base(hexEdit) {
            Overwrite = overwrite;
            _offset = offset;
            _data = data;
            hexEdit.Buffer.GetBytes(offset, 1, _oldData);
            _inputIndex = inputIndex;
            _wordIndex = wordIndex;
            _wordSize = wordSize;
        }

        public override void Execute() {
            var br = new ByteRange(_offset, _data);
            if (Overwrite)
                HexEdit.Buffer.Overwrite(br);
            else
                HexEdit.Buffer.Insert(br);

            HexEdit.InputIndex = _inputIndex;
            HexEdit.WordIndex = _wordIndex;
            if (_inputIndex == 1 && _wordIndex == _wordSize - 1) {
                HexEdit.CaretOffset = _offset + _wordSize;
            }

            Invalidate();
        }

        public override void Undo() {
            var br = new ByteRange(_offset, _oldData[0]);
            if (Overwrite)
                HexEdit.Buffer.Overwrite(br);
            else {
                HexEdit.Buffer.Delete(br.Range);
            }

            HexEdit.InputIndex = _inputIndex;
            HexEdit.WordIndex = _wordIndex;
            if (_inputIndex == 1 && _wordIndex == _wordSize - 1)
                HexEdit.CaretOffset = _offset;

            Invalidate();
        }
    }
}
