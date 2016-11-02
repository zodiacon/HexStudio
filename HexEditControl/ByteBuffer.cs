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
		byte[] _byteBuffer;
		long _size;
		string _filename;
		SortedList<long, DataRange> _dataRanges = new SortedList<long, DataRange>(64);

		public ByteBuffer(string filename) {
			Open(filename);
		}

		void Open(string filename) {
			_filename = filename;
			_size = new FileInfo(filename).Length;
			_memFile = MemoryMappedFile.CreateFromFile(filename);
			_accessor = _memFile.CreateViewAccessor();
			_dataRanges.Clear();
			_dataRanges.Add(0, new FileRange(Range.FromStartAndCount(0, Size), 0, _accessor));
			_currentRange = null;
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

		ByteRange _currentRange;
		bool _overwrite;
		public void AddChange(ByteRange change, bool overwrite) {
			_currentRange = change;
			_overwrite = overwrite;
		}

		private void ReadData(byte[] bytes, long fileOffset, int currentIndex, int count) {
			if (_accessor != null)
				_accessor.ReadArray(fileOffset, bytes, currentIndex, count);
			else
				Array.Copy(_byteBuffer, fileOffset, bytes, currentIndex, count);
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

		public void ApplyChanges() {
			Dispose();
			_memFile = MemoryMappedFile.CreateFromFile(_filename, FileMode.Open, null, Size);
			_accessor = _memFile.CreateViewAccessor();

			foreach (var dr in _dataRanges.Values) {
				dr.WriteData(dr.Start, _accessor);
			}

			DiscardChanges();
			_dataRanges.Clear();
			_dataRanges.Add(0, new FileRange(Range.FromStartAndCount(0, Size), 0, _accessor));
		}

		public static int MoveBufferSize { get; set; } = 1 << 21;
		public IEnumerable<DataRange> DataRanges => _dataRanges.Select(item => item.Value);

		public void DiscardChanges() {
			_size = new FileInfo(_filename).Length;
			_dataRanges.Clear();
			_dataRanges.Add(0, new FileRange(Range.FromStartAndCount(0, _size), 0, _accessor));
			_currentRange = null;
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
				Open(filename);
			}
			else {
				Dispose();
				File.Copy(_filename, filename, true);
				_filename = filename;
				ApplyChanges();
			}
		}

		public int GetBytes(long start, int count, byte[] buffer, IList<OffsetRange> changes = null) {
			if (start + count > Size)
				count = (int)(Size - start);

			int index = 0;
			bool first = true;
			foreach (var dr in _dataRanges.Values) {
				if (dr.Start > start)
					break;

				if (dr.End < start)
					continue;

				int n;
				if (first) {
					n = (int)Math.Min(dr.End - start + 1, count);
					dr.GetData((int)(start - dr.Start), buffer, index, n);
					first = false;
				}
				else {
					Debug.Assert(dr.Start == start);
					n = (int)Math.Min(count, dr.Count);
					if (n == 0)
						break;
					dr.GetData(0, buffer, index, n);
				}
				if (changes != null && dr is ByteRange)
					changes.Add(new OffsetRange(start, n));

				index += n;
				start += n;
				count -= n;
			}
			return index;
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

			if (index >= ranges.Count) {
				// add at the end
				_dataRanges.Add(change.Start, change);
				_size = change.End + 1;
				return;
			}

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
			if (change.End >= _size)
				_size = change.End + 1;

			if (right != null && !right.Range.IsEmpty)
				_dataRanges.Add(right.Start, right);
			if (next != null) {
				// check next range for overlap
				var isec2 = change.Range.GetIntersection(next.Range);
				if (!isec2.IsEmpty) {
					right = next.GetSubRange(Range.FromStartToEnd(change.End + 1, next.End));
					_dataRanges.Remove(next.Start);
					_dataRanges.Add(right.Start, right);
				}
			}
		}

		public void Insert(ByteRange change) {
			// find first affected range
			var ranges = _dataRanges.Values;
			DataRange dr = null;

			int i = 0;
			for (; i < ranges.Count; i++) {
				dr = ranges[i];
				if (dr.Range.Contains(change.Start))
					break;
			}
			if (i == ranges.Count) {
				// just add the change
				Debug.Assert(change.Start == Size);
				_dataRanges.Add(change.Start, change);
				_size = change.End + 1;
			}
			else {
				// split current
				var left = dr.GetSubRange(Range.FromStartToEnd(dr.Start, change.Start - 1));
				var right = dr.GetSubRange(Range.FromStartToEnd(change.Start, dr.End));

				_dataRanges.Remove(dr.Start);
				i--;

				if (left != null && !left.Range.IsEmpty)
					_dataRanges.Add(left.Start, left);

				if (right != null && !right.Range.IsEmpty) {
					if (left == null || left.Range.IsEmpty)
						right.Shift(change.Count);
					_dataRanges.Add(right.Start, right);
					i++;
				}
				//shift the rightmost ranges in reverse order to prevent accidental overlap
				ranges = _dataRanges.Values;

				//if (_dataRanges.ContainsKey(change.Start))
				//	i--;

					for (int j = ranges.Count - 1; j > i; --j) {
					dr = ranges[j];
					_dataRanges.Remove(dr.Start);
					dr.Shift(change.Count);
					_dataRanges.Add(dr.Start, dr);
				}

				// finally, insert the change
				_dataRanges.Add(change.Start, change);
				_size += change.Count;
			}
		}

		public void UpdateChange() {
			Debug.Assert(_currentRange != null);
			if (_overwrite)
				Overwrite(_currentRange);
			else {
				if (_currentRange.Count == 1)
					Insert(_currentRange);
				else
					UpdateInsert(_currentRange);
			}
		}

		private void UpdateInsert(ByteRange change) {
			Debug.Assert(_dataRanges.ContainsKey(change.Start));

			var i = _dataRanges.IndexOfKey(change.End);
			if (i > -1) {
				var ranges = _dataRanges.Values;
				for (int j = ranges.Count - 1; j >= i; --j) {
					var dr = ranges[j];
					_dataRanges.Remove(dr.Start);
					dr.Shift(1);
					_dataRanges.Add(dr.Start, dr);
				}
			}
			_size++;
		}
	}
}
