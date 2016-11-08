using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;

namespace Zodiacon.HexEditControl
{
    public sealed class ByteBuffer : IDisposable
    {
        MemoryMappedViewAccessor _accessor;
        MemoryMappedFile _memFile;
        byte[] _byteBuffer;
        string _filename;
        SortedList<long, DataRange> _dataRanges = new SortedList<long, DataRange>(64);

        public long Size { get; private set; }

        public event Action<long, long> SizeChanged;

        void OnSizeChanged(long oldSize) {
            SizeChanged?.Invoke(oldSize, Size);
        }

        public ByteBuffer(string filename) {
            Open(filename);
        }

        void Open(string filename) {
            _filename = filename;
            Size = new FileInfo(filename).Length;
            _memFile = MemoryMappedFile.CreateFromFile(filename);
            _accessor = _memFile.CreateViewAccessor();
            _dataRanges.Clear();
            _dataRanges.Add(0, new FileRange(Range.FromStartAndCount(0, Size), 0, _accessor));
            OnSizeChanged(0);
        }

        public ByteBuffer(long size, long limit) {
            _memFile = MemoryMappedFile.CreateNew(null, limit);
            _accessor = _memFile.CreateViewAccessor();
            Size = size;
        }

        public ByteBuffer(byte[] buffer) {
            _byteBuffer = buffer;
            Size = buffer.LongLength;
        }

        public void ApplyChanges() {
            Dispose();
            long fileSize = new FileInfo(_filename).Length;
            _memFile = MemoryMappedFile.CreateFromFile(_filename, FileMode.Open, null, Math.Max(Size, fileSize));
            _accessor = _memFile.CreateViewAccessor();

            foreach (var dr in _dataRanges.Values.OfType<FileRange>()) {
                dr.WriteData(dr.Start, _accessor);
            }
            foreach (var dr in _dataRanges.Values.OfType<ByteRange>()) {
                dr.WriteData(dr.Start, _accessor);
            }

            if (fileSize > Size) {
                Dispose();
                using (var stm = File.OpenWrite(_filename))
                    stm.SetLength(Size);
                File.SetLastWriteTime(_filename, DateTime.Now);
                _memFile = MemoryMappedFile.CreateFromFile(_filename, FileMode.Open, null, 0);
                _accessor = _memFile.CreateViewAccessor();
            }

            DiscardChanges();
        }

        public static int MoveBufferSize { get; set; } = 1 << 21;
        public IEnumerable<DataRange> DataRanges => _dataRanges.Select(item => item.Value);

        public void DiscardChanges() {
            var oldSize = Size;
            Size = new FileInfo(_filename).Length;
            _dataRanges.Clear();
            _dataRanges.Add(0, new FileRange(Range.FromStartAndCount(0, Size), 0, _accessor));
            OnSizeChanged(oldSize);
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

            DataRange dr;
            if (_dataRanges.TryGetValue(change.Start, out dr) && change.Count == dr.Count) {
                // just replace
                _dataRanges.Remove(change.Start);
                _dataRanges.Add(change.Start, change);
                return;
            }

            var ranges = _dataRanges.Values;
            int index = -1;

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
                var oldSize = Size;
                Size = change.End + 1;
                OnSizeChanged(oldSize);
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
            if (change.End >= Size) {
                var oldSize = Size;
                Size = change.End + 1;
                OnSizeChanged(oldSize);
            }

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
                var oldSize = Size;
                Size = change.End + 1;
                OnSizeChanged(oldSize);
            }
            else {
                // split current
                var left = dr.GetSubRange(Range.FromStartToEnd(dr.Start, change.Start - 1));
                var right = dr.GetSubRange(Range.FromStartToEnd(change.Start, dr.End));

                _dataRanges.Remove(dr.Start);
                i--;

                //shift the rightmost ranges in reverse order to prevent accidental overlap
                ranges = _dataRanges.Values;

                for (int j = ranges.Count - 1; j > i; --j) {
                    dr = ranges[j];
                    _dataRanges.Remove(dr.Start);
                    dr.Shift(change.Count);
                    _dataRanges.Add(dr.Start, dr);
                }

                if (!left.Range.IsEmpty)
                    _dataRanges.Add(left.Start, left);

                if (!right.Range.IsEmpty) {
                    right.Shift(change.Count);
                    _dataRanges.Add(right.Start, right);
                }

                // finally, insert the change
                _dataRanges.Add(change.Start, change);
                Size += change.Count;
                OnSizeChanged(Size - change.Count);
            }
        }

        public void Delete(Range range) {
            int i;
            DataRange exactRange;
            if ((i = _dataRanges.IndexOfKey(range.Start)) >= 0 && (exactRange = _dataRanges.Values[i]).Count == range.Count) {
                _dataRanges.RemoveAt(i);
                for (int j = i; j < _dataRanges.Count; ++j) {
                    var dr1 = _dataRanges.Values[j];
                    dr1.Shift(-range.Count);
                    _dataRanges.RemoveAt(j);
                    _dataRanges.Add(dr1.Start, dr1);
                }
                Size -= range.Count;
                OnSizeChanged(Size + range.Count);
                return;
            }

            var ranges = _dataRanges.Values;
            for (i = ranges.Count - 1; i >= 0; --i) {
                var dr = ranges[i];
                if (dr.Range.ContainsEntirely(range)) {
                    // split dr into two ranges
                    var left = dr.GetSubRange(Range.FromStartToEnd(dr.Start, range.Start - 1));
                    var right = dr.GetSubRange(Range.FromStartToEnd(range.End + 1, dr.End));

                    // remove range and replace with two other
                    _dataRanges.RemoveAt(i);

                    for (int j = _dataRanges.Count - 1; j >= i; --j) {
                        var dr1 = _dataRanges.Values[j];
                        dr1.Shift(-range.Count);
                        _dataRanges.RemoveAt(j);
                        _dataRanges.Add(dr1.Start, dr1);
                    }

                    if (!left.Range.IsEmpty)
                        _dataRanges.Add(left.Start, left);

                    if (!right.Range.IsEmpty) {
                        right.Shift(-range.Count);
                        _dataRanges.Add(right.Start, right);
                    }
                    Size -= range.Count;
                    OnSizeChanged(Size + range.Count);
                    break;
                }
                if (dr.Range.Intersects(range)) {
                    // complex overlap
                    var right = i < ranges.Count - 1 ? ranges[i + 1] : null;
                    var left = i > 0 ? ranges[i - 1] : null;
                    if (left == null && right == null) {
                        _dataRanges.Clear();
                        var oldSize = Size;
                        Size = 0;
                        OnSizeChanged(oldSize);
                        break;
                    }

                    if (left != null) {
                        _dataRanges.Remove(left.Start);
                        left = left.GetSubRange(Range.FromStartToEnd(left.Start, range.Start - 1));
                    }
                    if (right != null) {
                        _dataRanges.Remove(right.Start);
                        right = right.GetSubRange(Range.FromStartToEnd(range.Start, right.End));
                    }

                    if (left != null && !left.IsEmpty) {
                        _dataRanges.Add(left.Start, left);
                    }
                    if (right != null && !right.IsEmpty) {
                        _dataRanges.Add(right.Start, right);
                    }

                    break;
                }
            }
        }

    }
}
