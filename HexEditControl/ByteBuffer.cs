using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zodiacon.HexEditControl.DataStructures;
using Zodiacon.WPF;

namespace Zodiacon.HexEditControl {
	public class ByteBuffer : IDisposable {
		MemoryMappedViewAccessor _accessor;
		MemoryMappedFile _memFile;
		long _size;
		readonly List<EditChange> _changes = new List<EditChange>();
		byte[] _byteBuffer;
		string _filename;
		SortedList<long, DataRange> _dataRanges = new SortedList<long, DataRange>(32);

		public ByteBuffer(string filename) {
			Open(filename);
		}

		void Open(string filename) {
			_filename = filename;
			_size = new FileInfo(filename).Length;
			_memFile = MemoryMappedFile.CreateFromFile(filename);
			_accessor = _memFile.CreateViewAccessor();
			_dataRanges.Add(0, new FileRange(Range.FromStartAndCount(0, Size), 0, _accessor));
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
		public IEnumerable<DataRange> DataRanges => _dataRanges.Select(item => item.Value);

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
			_dataRanges.Clear();
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

		public void Overwrite(ByteRange change) {
			var ranges = _dataRanges.Values;
			int index = -1;
			DataRange dr;

			for (int i = 0; i < ranges.Count; i++) {
				dr = ranges[i];

				// are we off the grid?
				if (change.End < dr.Start)
					break;

				// skip ranges eariler than the change
				if (change.Start > dr.End)
					continue;

				if (index < 0)
					index = i;
				if (change.Range.ContainsEntirely(dr.Range)) {
					// range can be removed
					_dataRanges.RemoveAt(i);
					i--;
					continue;
				}
			}
			if (index < 0)
				return;

			dr = ranges[index];

			// some non trivial intersection
			var isec = change.Range.GetIntersection(dr.Range);
			var left = dr.GetSubRange(Range.FromStartToEnd(dr.Start, change.Start - 1));
			var right = dr.GetSubRange(Range.FromStartToEnd(change.End + 1, dr.End));

			var next = index < ranges.Count - 1 ? ranges[index + 1] : null;

			_dataRanges.RemoveAt(index);
			if (left != null && !left.Range.IsEmpty)
				_dataRanges.Add(left.Start, left);
			_dataRanges.Add(change.Start, change);
			if (right != null && !right.Range.IsEmpty)
				_dataRanges.Add(right.Start, right);
			if (next != null) {
				// check next range for overlap
				var isec2 = change.Range.GetIntersection(next.Range);
				right = next.GetSubRange(Range.FromStartToEnd(change.End + 1, next.End));
				_dataRanges.Remove(next.Start);
				_dataRanges.Add(right.Start, right);
			}
		}

		public void Insert(ByteRange change) {
		}
	}
}
