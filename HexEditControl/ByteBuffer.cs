using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zodiacon.WPF;

namespace Zodiacon.HexEditControl {
	public class ByteBuffer : IDisposable {
		MemoryMappedViewAccessor _accessor;
		MemoryMappedFile _memFile;
		long _size;
		readonly List<EditChange> _changes = new List<EditChange>();
		byte[] _byteBuffer;
		string _filename;
		readonly List<IEditOperation> _operations = new List<IEditOperation>(64);
		readonly List<IDataRange> _dataRanges = new List<IDataRange>();

		public ByteBuffer(string filename) {
			Open(filename);
		}

		void Open(string filename) {
			_filename = filename;
			_size = new FileInfo(filename).Length;
			_memFile = MemoryMappedFile.CreateFromFile(filename);
			_accessor = _memFile.CreateViewAccessor();
			_dataRanges.Clear();
			_dataRanges.Add(new FileRange(0, 0, _size, _accessor));
		}

		public ByteBuffer(long size, long limit) {
			_memFile = MemoryMappedFile.CreateNew(null, limit);
			_accessor = _memFile.CreateViewAccessor();
			_size = size;
		}

		public ByteBuffer(byte[] buffer) {
			_byteBuffer = buffer;
			_size = buffer.LongLength;
		}

		public long Size => _size;

		int _lastChangeIndex = -1;
		EditChange _currentChange;
		int _lastChangeSize;

		public void AddChange(EditChange change) {
			AddChangeInternal(change);
		}

		public void AddChange(long offset, byte[] data, bool overwrite) {
			var newchange = new EditChange(offset, data) {
				Overwrite = overwrite
			};

			AddChangeInternal(newchange);
		}

		private void AddChangeInternal(EditChange newchange) {
			var offset = newchange.Offset;
			var data = newchange.Data;
			var insertion = _changes.FindIndex(0, ch => ch.Offset > offset);
			if (insertion >= 0) {
				_changes.Insert(insertion, newchange);
				_lastChangeIndex = insertion;
				if (!newchange.Overwrite) {
					for (int i = insertion + 1; i < _changes.Count; i++)
						_changes[i].UpdateOffset(data.Count);
				}
			}
			else {
				_changes.Add(newchange);
				_lastChangeIndex = _changes.Count - 1;
			}
			_currentChange = newchange;
			_lastChangeSize = newchange.Size;

			if (!newchange.Overwrite)
				_size += newchange.Size;

		}

		public int GetBytes(long offset, int size, byte[] bytes, int startIndex = 0, IList<OffsetRange> changes = null) {
			if (size > Size)
				size = (int)Size;
			// get insert type changes to this point
			long fileOffset = _changes.Where(change => !change.Overwrite).TakeWhile(change => change.Offset + change.Size < offset).Sum(change => change.Size);
			fileOffset += offset;

			long currentOffset = offset;
			int currentIndex = 0;

			var inrange = _changes.Where(ch => (ch.Offset + ch.Size >= offset && ch.Offset - ch.Size < offset) ||
				(ch.Offset - ch.Size < offset + size && ch.Offset + ch.Size > offset + size)
				|| (ch.Offset > offset && ch.Offset + ch.Size <= offset + size));

			foreach (var change in inrange) {
				var count = Math.Min((int)(change.Offset - currentOffset), size - currentIndex);
				int sourceIndex = 0;
				if (count > 0) {
					ReadData(bytes, fileOffset, currentIndex + startIndex, count);
					fileOffset += count;
					currentIndex += count;
					currentOffset += count;
				}
				else if (count < 0) {
					// change started before offset
						sourceIndex = -count;
				}

				// now get data from the change
				count = Math.Min(change.Size, size - currentIndex) - sourceIndex;
				change.Data.CopyTo(sourceIndex, bytes, currentIndex + startIndex, count);

				if (changes != null)
					changes.Add(new OffsetRange(change.Offset, change.Size));

				currentIndex += count;
				if (change.Overwrite)
					fileOffset += count;
				currentOffset += count;
			}
			if (currentIndex < size) {
				ReadData(bytes, fileOffset, currentIndex + startIndex, size - currentIndex);
				currentIndex = size;
			}
			return currentIndex;
		}

		private void ReadData(byte[] bytes, long fileOffset, int currentIndex, int count) {
			if (_accessor != null)
				_accessor.ReadArray(fileOffset, bytes, currentIndex, count);
			else
				Array.Copy(_byteBuffer, fileOffset, bytes, currentIndex, count);
		}

		public bool IsChanged(long offset, int size, ref int changeIndex) {
			for (int i = changeIndex; i < _changes.Count; i++) {
				if (_changes[i].Intersect(offset, size)) {
					changeIndex = i;
					return true;
				}
			}
			return false;
		}

		public void UpdateLastChange() {
			if (_currentChange != null && _currentChange.Size != _lastChangeSize) {
				Debug.Assert(_lastChangeIndex >= 0);

				if (!_currentChange.Overwrite) {
					// update following changes if it's an insert
					_size += _currentChange.Size - _lastChangeSize;

					foreach (var change in _changes.Skip(_lastChangeIndex + 1))
						change.UpdateOffset(1);
					_lastChangeSize = _currentChange.Size;
				}
			}
		}

		void WriteData(long offset, byte[] data, int count = 0) {
			if (count == 0)
				count = data.Length;

			if (_accessor != null) {
				_accessor.WriteArray(offset, data, 0, count);
			}
			else {
				if (offset + count > _byteBuffer.Length) {
					Array.Resize(ref _byteBuffer, (int)offset + count);
					_size = offset + count;
				}
				Array.Copy(data, 0, _byteBuffer, offset, count);
			}
		}

		public void ApplyChanges(bool clearAfterApply = true) {
			Dispose();
			_memFile = MemoryMappedFile.CreateFromFile(_filename, FileMode.Open, null, Size);
			_accessor = _memFile.CreateViewAccessor();

			foreach (var change in _changes) {
				// apply change
				if (change.Overwrite) {
					WriteData(change.Offset, change.Data.ToArray());
				}
				else {
					// more complex, must move file forward to make room
					MoveBuffer(change.Offset, change.Size);
					WriteData(change.Offset, change.Data.ToArray());
				} 
			}
			if (clearAfterApply) {
				_changes.Clear();
				_currentChange = null;
				_lastChangeIndex = -1;
			}
		}

		public static int MoveBufferSize { get; set; } = 1 << 21;

		static byte[] _moveBuffer;
		private void MoveBuffer(long offset, int size) {
			if (_moveBuffer == null)
				_moveBuffer = new byte[MoveBufferSize];

			var count = _size - offset;

			while (count > 0) {
				var read = Math.Min(_moveBuffer.Length, count);
				ReadData(_moveBuffer, offset, 0, (int)read);
				WriteData(offset + size, _moveBuffer, (int)read);
				count -= read;
				offset += read;
			}
		}

		public void DiscardChanges() {
			_changes.Clear();
			_currentChange = null;
			_lastChangeIndex = -1;
			_size = new FileInfo(_filename).Length;
			_dataRanges.RemoveRange(1, _dataRanges.Count - 1);
		}

		public void Dispose() {
			if (_accessor != null) {
				_accessor.Dispose();
				_accessor = null;
			}
			if (_memFile != null) {
				_memFile.Dispose();
				_memFile = null;
			}

		}

		public void SaveToFile(string filename) {
			if (string.IsNullOrEmpty(_filename)) {
				// new file, just get everything out

				byte[] bytes = _byteBuffer ?? new byte[Size];
				if (_byteBuffer == null)
					GetBytes(0, (int)Size, bytes);
				File.WriteAllBytes(filename, bytes);
			}
			else {
				Dispose();
				File.Copy(_filename, filename);
				_filename = filename;
				ApplyChanges();
			}
			DiscardChanges();
		}

		public void AddOperation(IEditOperation operation) {
			_operations.Add(operation);
			UpdateDataRanges(operation);			
		}

		private void UpdateDataRanges(IEditOperation operation) {
			long startOffset = operation.Offset;

			for(int i = 0; i < _dataRanges.Count; i++) {
				var dr = _dataRanges[i];
				if (dr.Offset + dr.Size < operation.Offset)
					continue;

				var range = dr.ToRange();
				var op_range = operation.ToRange();

				if (op_range.GetIntersection(range) == range) {
					// range is completely inside the range of the new operation. remove the range
					_dataRanges.RemoveAt(i);
					i--;
					continue;
				}
				else if (range.GetIntersection(op_range) == op_range) {
					// completely within the existing one
					// split into three data regions
					SplitRegion(dr, i, operation);
					break;
				}
				else {
					// partial overlap
					// find overlap extent - may have more regions completely covered
					while (i + 1 < _dataRanges.Count) {
						var r = _dataRanges[i + 1].ToRange();
						if (op_range.GetIntersection(r) == r) {
							// can remove region
							_dataRanges.RemoveAt(i);
						}
						else
							break;
					}
					// region i and i+1 are to be treated (i+1 may not exist)
					SplitOverlapRegions(i, operation);
					break;
				}
			}
		}

		private void SplitOverlapRegions(int i, IEditOperation operation) {
			// get the regions
			var dr1 = _dataRanges[i];

			// first region (left)
			var r1 = dr1.GetSubRange(dr1.Offset, operation.Offset - dr1.Offset);

			switch (operation.Type) {
			}
		}

		private void SplitRegion(IDataRange dr, int index, IEditOperation operation) {
			switch (operation.Type) {
				case OperationType.OverwriteData:
					// simplest, create the regions
					var dr1 = dr.GetSubRange(dr.Offset, operation.Offset - dr.Offset);
					var dr3 = dr.GetSubRange(operation.Offset + operation.Count, dr.Size - operation.Count - dr1.Size);
					var dr2 = new ByteRange(operation.Offset, operation.Data.ToArray());
					_dataRanges.RemoveAt(index);
					_dataRanges.InsertRange(index, new IDataRange[] { dr1, dr2, dr3 });
					break;

				case OperationType.InsertData:
					var dr4 = dr.GetSubRange(dr.Offset, operation.Offset - dr.Offset);
					var dr5 = dr.GetSubRange(operation.Offset, dr.Size - dr4.Size);
					var dr6 = new ByteRange(operation.Offset, operation.Data.ToArray());
					_dataRanges.RemoveAt(index);
					_dataRanges.InsertRange(index, new IDataRange[] { dr4, dr5, dr6 });
					_size += operation.Count;
					break;
			}
		}

		public void PopOperation() {
		}

	}
}
