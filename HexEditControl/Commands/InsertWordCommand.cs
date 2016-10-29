using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using Zodiacon.WPF;

namespace Zodiacon.HexEditControl.Commands {
	class InsertWordCommand : IAppCommand {
		ulong _data, _oldData;
		long _offset;
		bool _overwrite;
		int _size;
		MemoryMappedViewAccessor _accessor;

		public string Description => Name;

		public string Name => "Insert Word";

		public InsertWordCommand(MemoryMappedViewAccessor accessor, long offset, ulong data, int size, bool overwrite) {
			_accessor = accessor;
			_offset = offset;
			_data = data;
			_size = size;
			_overwrite = overwrite;
		}

		public void Execute() {
			// save old word

			switch (_size) {
				case 1:
					_oldData = _accessor.ReadByte(_offset);
					break;

				case 2:
					_oldData = _accessor.ReadUInt16(_offset);
					break;

				case 4:
					_oldData = _accessor.ReadUInt32(_offset);
					break;

				case 8:
					_oldData = _accessor.ReadUInt64(_offset);
					break;
			}

			// make the change

			switch (_size) {
				case 1:
					_accessor.Write(_offset, (byte)_data);
					break;

				case 2:
					_accessor.Write(_offset, (ushort)_data);
					break;

				case 4:
					_accessor.Write(_offset, (uint)_data);
					break;

				case 8:
					_accessor.Write(_offset, _data);
					break;

				default:
					Debug.Assert(false, "bad data size, must be 1, 2, 4 or 8");
					break;
			}
		}

		public void Undo() {
			Execute();
		}
	}
}
