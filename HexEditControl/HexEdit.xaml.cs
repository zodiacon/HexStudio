using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Zodiacon.WPF;

namespace Zodiacon.HexEditControl {
	/// <summary>
	/// Interaction logic for HexEdit.xaml
	/// </summary>
	public partial class HexEdit : IDisposable {
		MemoryMappedFile _memFile;
		MemoryMappedViewAccessor _accessor;
		long _size, _sizeLimit;
		readonly DispatcherTimer _timer;
		double _hexDataXPos, _hexDataWidth;
		long _startOffset = -1, _endOffset = -1;
		double _charWidth;
		int _viewLines;

		readonly Dictionary<long, Point> _offsetsPositions = new Dictionary<long, Point>(128);
		readonly Dictionary<int, long> _verticalPositions = new Dictionary<int, long>(128);
		readonly Dictionary<long, EditChange> _changes = new Dictionary<long, EditChange>(128);
		readonly AppCommandManager _commandManager = new AppCommandManager();
		readonly SortedList<long, int> _insertBlocks = new SortedList<long, int>(32);

		//long _totalLines;

		public HexEdit() {
			InitializeComponent();

			_timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(.5) };
			_timer.Tick += _timer_Tick;
			_timer.Start();

			// create new document by default
			CreateNew();

			Loaded += delegate {
				_root.Focus();
			};
		}

		static HexEdit() {
			CommandManager.RegisterClassCommandBinding(typeof(HexEdit), new CommandBinding(ApplicationCommands.SelectAll,
				(s, e) => ((HexEdit)s).ExecuteSelectAll(e)));
			CommandManager.RegisterClassCommandBinding(typeof(HexEdit), new CommandBinding(ApplicationCommands.Copy,
				(s, e) => ((HexEdit)s).ExecuteCopy(e), (s, e) => ((HexEdit)s).CanExecuteCopy(e)));
			CommandManager.RegisterClassCommandBinding(typeof(HexEdit), new CommandBinding(ApplicationCommands.Paste,
				(s, e) => ((HexEdit)s).ExecutePaste(e), (s, e) => ((HexEdit)s).CanExecutePaste(e)));
		}


		private void _timer_Tick(object sender, EventArgs e) {
			if (CaretOffset < 0)
				_caret.Visibility = Visibility.Collapsed;
			else
				_caret.Visibility = _caret.Visibility == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;
		}

		Func<byte[], int, string>[] _bitConverters = {
			(bytes, i) => bytes[i].ToString("X2"),
			(bytes, i) => BitConverter.ToUInt16(bytes, i).ToString("X4"),
			(bytes, i) => BitConverter.ToUInt32(bytes, i).ToString("X8"),
			(bytes, i) => BitConverter.ToUInt64(bytes, i).ToString("X16"),
		};

		static int[] _bitConverterIndex = { 0, 0, 1, 1, 2, 2, 2, 2, 3, 3, 3 };

		private void Refresh() {
			Recalculate();
			InvalidateVisual();
			if (CaretOffset >= 0) {
				CaretOffset = CaretOffset - CaretOffset % WordSize;
				SetCaretPosition(CaretOffset);
			}
		}

		public void CreateNew(long sizeLimit = 1 << 20) {
			Dispose();
			_memFile = MemoryMappedFile.CreateNew(null, _sizeLimit = sizeLimit);
			_size = WordSize;
			_accessor = _memFile.CreateViewAccessor();
		}

		string _filename;
		public void OpenFile(string filename) {
			if (string.IsNullOrWhiteSpace(filename))
				throw new ArgumentException("Filename is empty or null", nameof(filename));

			Dispose();

			_size = _sizeLimit = 0;
			_size = new FileInfo(filename).Length;
			_memFile = MemoryMappedFile.CreateFromFile(filename, FileMode.Open);
			_accessor = _memFile.CreateViewAccessor();
			_filename = filename;
			Refresh();
		}

		private void Recalculate() {
			_scroll.ViewportSize = ActualHeight;
			_scroll.Maximum = _size / BytesPerLine * (FontSize + VerticalSpace) - ActualHeight + VerticalSpace * 2;
		}

		private void _scroll_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
			InvalidateVisual();
		}

		byte[] _readBuffer = new byte[1 << 16];
		StringBuilder _hexString = new StringBuilder(256);       // hex string
		StringBuilder _hexChangesString = new StringBuilder(256);     // changes string
		int _insertOffset;

		protected override void OnRender(DrawingContext dc) {
			if (_accessor == null || _size == 0 || ActualHeight < 1) return;

			dc.PushClip(new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)));

			var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
			int lineHeight = (int)(FontSize + VerticalSpace);
			var y = -_scroll.Value;
			var viewport = _scroll.ViewportSize;
			long start = (long)(BytesPerLine * (0 - VerticalSpace - y) / lineHeight);

			if (start < 0)
				start = 0;
			else
				start = start / BytesPerLine * BytesPerLine;
			long end = start + BytesPerLine * ((long)viewport / lineHeight + 1);
			if (end > _size)
				end = _size;

			var maxWidth = 0.0;
			for (long i = start; i < end; i += BytesPerLine) {
				var pos = 2 + (i / BytesPerLine) * lineHeight + y;
				var text = new FormattedText(i.ToString("X8") + ": ", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, FontSize, Foreground);
				if (text.Width > maxWidth)
					maxWidth = text.Width;
				dc.DrawText(text, new Point(2, pos));
			}

			var x = maxWidth + 8;

			int readSize = (int)(end - start + 1 + 7);

			_insertOffset = _insertBlocks.TakeWhile(block => block.Key + block.Value < start).Sum(pair => pair.Value);

			if (start != _startOffset || end != _endOffset) {
				var read = _accessor.ReadArray(start - _insertOffset, _readBuffer, 0, readSize);
				if (start + read < end)
					end = start + read;
			}
			else {
				end = _endOffset;
			}

			_hexDataXPos = x;

			var singleChar = new FormattedText("8", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, FontSize, Foreground);

			maxWidth = 0;
			_offsetsPositions.Clear();
			_verticalPositions.Clear();

			var bitConverter = _bitConverters[_bitConverterIndex[WordSize]];
			_viewLines = 0;
			var space = new string(' ', WordSize * 2 + 1);
			EditChange change;
			int len = 0;

			int readIndex = 0;

			for (long i = start; i < end; i += BytesPerLine) {
				var pos = 2 + (i / BytesPerLine) * lineHeight + y;
				_hexString.Clear();
				_hexChangesString.Clear();

				for (var j = i; j < i + BytesPerLine && j < end; j += WordSize) {
					int bufIndex = (int)(j - start);

					bool changed = false;

					if (j >= SelectionStart && j <= SelectionEnd) {
						// j is selected
						dc.DrawRectangle(SelectionBackground, null, new Rect(x + (j - i) / WordSize * (2 * WordSize + 1) * _charWidth, pos, _charWidth * WordSize * 2, FontSize));
					}

					for (int k = 0; k < WordSize; k++) {
						if (_changes.TryGetValue(j + k, out change)) {
							_readBuffer[bufIndex + k] = change.Value;
							changed = true;
						}
					}
					if (changed) {
						_hexChangesString.Append(bitConverter(_readBuffer, bufIndex)).Append(" ");
						_hexString.Append(space);
					}
					else {
						_hexString.Append(bitConverter(_readBuffer, bufIndex + readIndex)).Append(" ");
						_hexChangesString.Append(space);
					}
				}

				var pt = new Point(x, pos);

				var text = new FormattedText(_hexString.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, FontSize, Foreground);
				if (len == 0)
					len = _hexString.Length;

				dc.DrawText(text, pt);
				if (text.WidthIncludingTrailingWhitespace > maxWidth) {
					maxWidth = text.WidthIncludingTrailingWhitespace;
					if (_charWidth < 1) {
						_charWidth = maxWidth / len;
					}
				}

				text = new FormattedText(_hexChangesString.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, FontSize, Brushes.Red);
				dc.DrawText(text, pt);
				if (text.Width > maxWidth)
					maxWidth = text.Width;

				_offsetsPositions[i] = pt;
				_verticalPositions[(int)pt.Y] = i;

				_viewLines++;
			}

			_hexDataWidth = maxWidth;

			x = _hexDataXPos + _hexDataWidth + 10;
			maxWidth = 0;
			char ch;

			for (long i = start; i < end; i += BytesPerLine) {
				var pos = 2 + (i / BytesPerLine) * lineHeight + y;
				_hexString.Clear();
				_hexChangesString.Clear();

				for (var j = i; j < i + BytesPerLine && j < end; j++) {
					if (SelectionStart <= j && j <= SelectionEnd) {
						// j is selected
						dc.DrawRectangle(SelectionBackground, null, new Rect(x + _charWidth * (j - i), pos, _charWidth, FontSize));
					}

					if (_changes.TryGetValue(j, out change)) {
						ch = (char)change.Value;
						_hexChangesString.Append(char.IsControl(ch) ? '.' : ch);
						_hexString.Append(' ');
					}
					else {
						ch = Encoding.ASCII.GetChars(_readBuffer, (int)(j - start), 1)[0];
						_hexString.Append(char.IsControl(ch) ? '.' : ch);
						_hexChangesString.Append(' ');
					}
				}

				var pt = new Point(x, pos);

				var text = new FormattedText(_hexString.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, FontSize, Foreground);
				dc.DrawText(text, pt);

				text = new FormattedText(_hexChangesString.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, FontSize, Brushes.Red);
				dc.DrawText(text, pt);
			}

			_endOffset = end;
			_startOffset = start;

			dc.Pop();

		}

		Point GetPositionByOffset(long offset) {
			if (offset >= 0) {
				var offset2 = offset / BytesPerLine * BytesPerLine;
				Point pt;
				if (_offsetsPositions.TryGetValue(offset2, out pt)) {
					return new Point(pt.X + (offset - offset2) * _charWidth * (WordSize * 2 + 1) / WordSize, pt.Y);
				}
			}
			return new Point(-100, -100);
		}

		long GetOffsetByCursorPosition(Point pt) {
			var xp = pt.X - _hexDataXPos;
			long offset = -1;
			var y = (int)pt.Y;
			while (!_verticalPositions.TryGetValue(y, out offset) && y > -10)
				y--;
			if (offset < 0)
				return offset;

			int x = (int)(xp / (_charWidth * (WordSize * 2 + 1))) * WordSize;
			return offset + x;
		}

		private void This_SizeChanged(object sender, SizeChangedEventArgs e) {
			Refresh();
		}

		private void Grid_MouseWheel(object sender, MouseWheelEventArgs e) {
			_scroll.Value -= e.Delta;
		}

		static byte[] _zeros = new byte[8];
		bool _selecting;
		private void Grid_KeyDown(object sender, KeyEventArgs e) {
			if (CaretOffset < 0) {
			}
			else {
				var modifiers = e.KeyboardDevice.Modifiers;
				bool shiftDown = modifiers == ModifierKeys.Shift;
				if (shiftDown && !_selecting) {
					SelectionStart = SelectionEnd = CaretOffset;
					_selecting = true;
				}
				e.Handled = true;
				bool arrowKey = true;
				var offset = CaretOffset;

				switch (e.Key) {
					case Key.Down:
						CaretOffset += BytesPerLine;
						break;

					case Key.Up:
						CaretOffset -= BytesPerLine;
						break;

					case Key.Right:
						if (!_selecting && CaretOffset + WordSize >= _size) {
							_size = CaretOffset + WordSize * 2;
							_accessor.WriteArray(_size - WordSize, _zeros, 0, WordSize);
						}
						CaretOffset += WordSize;
						break;

					case Key.Left:
						CaretOffset -= WordSize;
						break;

					case Key.PageDown:
						if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
							CaretOffset = _size - _size % WordSize - 1;
						else
							CaretOffset += BytesPerLine * _viewLines;
						break;

					case Key.PageUp:
						if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
							CaretOffset = 0;
						else
							CaretOffset -= BytesPerLine * _viewLines;
						break;

					default:
						arrowKey = false;
						if (modifiers == ModifierKeys.None)
							HandleTextEdit(e);
						else
							e.Handled = false;
						break;
				}
				if (shiftDown && arrowKey) {
					bool expanding = CaretOffset > offset;    // higher addresses
					UpdateSelection(expanding);
				}
				else {
					_selecting = false;
				}
				if (arrowKey) {
					_insertBlockStart = -1;
				}
				_root.Focus();
			}
		}

		int _inputIndex = 0, _wordIndex = 0;
		byte _lastValue = 0;
		EditChange _currentChange;
		long _insertBlockStart = -1;
		int _insertBlockSize;

		private void HandleTextEdit(KeyEventArgs e) {
			if (IsReadOnly) return;

			if ((e.Key < Key.D0 || e.Key > Key.D9) && (e.Key < Key.A || e.Key > Key.F))
				return;

			// make a change
			byte value = e.Key >= Key.A ? (byte)(e.Key - Key.A + 10) : (byte)(e.Key - Key.D0);
			_lastValue = (byte)(_lastValue * 16 + value);

			if (!IsModified)
				IsModified = true;

			ClearSelection();

			if (_inputIndex == 0) {
				_currentChange = new EditChange { Offset = CaretOffset + _wordIndex, Overwrite = OverwriteMode };
				_changes[CaretOffset + _wordIndex] = _currentChange;
				if (_wordIndex == 0 && !OverwriteMode) {
					int start = (int)(CaretOffset - _startOffset);
					Array.Copy(_readBuffer, start, _readBuffer, start + WordSize, _readBuffer.Length - (start + WordSize));
					_size += WordSize;
					// insert block
					if (_insertBlockStart < 0) {
						// new insert block
						_insertBlockStart = CaretOffset;
						_insertBlocks.Add(CaretOffset, _insertBlockSize = WordSize);
					}
					else {
						_insertBlockSize += WordSize;
						_insertBlocks[_insertBlockStart] = _insertBlockSize;
					}

					//InsertWord(CaretOffset, WordSize);
					InvalidateVisual();
				}
			}
			_currentChange.Value = _lastValue;
			if (++_inputIndex == 2) {
				_currentChange = null;
				_inputIndex = 0;
				if (++_wordIndex == WordSize) {
					if (CaretOffset + WordSize >= _size)
						_size = CaretOffset + 2 * WordSize;
					CaretOffset += WordSize;
					_wordIndex = 0;
				}
			}
			InvalidateVisual();
		}

		private void ClearSelection() {
			SelectionStart = SelectionEnd = -1;
		}

		public static int MoveBufferSize { get; set; } = 1 << 21;

		static byte[] _moveBuffer;
		private void InsertWord(long offset, int wordSize) {
			if (_moveBuffer == null)
				_moveBuffer = new byte[MoveBufferSize];
			var count = _size - offset;
			if (wordSize > 0)
				_size += wordSize;

			while (count > 0) {
				long read = Math.Min(_moveBuffer.Length, count);
				_accessor.ReadArray(offset, _moveBuffer, 0, (int)read);
				_accessor.WriteArray(offset + wordSize, _moveBuffer, 0, (int)read);
				count -= read;
			}
		}

		public void MakeVisible(long offset) {
			if (offset >= _startOffset + BytesPerLine && offset <= _endOffset - BytesPerLine)
				return;

			var start = offset - _viewLines * BytesPerLine / 2;
			if (start < 0)
				start = 0;

			_scroll.Value = VerticalSpace + start * (FontSize + VerticalSpace) / BytesPerLine;
			InvalidateVisual();
		}

		bool _mouseLeftButtonDown;
		private void _scroll_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
			var pt = e.GetPosition(this);
			CaretOffset = GetOffsetByCursorPosition(pt);
			_root.Focus();
			_root.CaptureMouse();
			_mouseLeftButtonDown = true;
		}

		private void Grid_MouseMove(object sender, MouseEventArgs e) {
			// change cursor 
			var pt = e.GetPosition(this);
			if (pt.X >= _hexDataXPos && pt.X < _hexDataXPos + _hexDataWidth) {
				Cursor = Cursors.IBeam;
				if (_mouseLeftButtonDown) {
					var offset = CaretOffset;
					CaretOffset = GetOffsetByCursorPosition(pt);
					if (offset != CaretOffset && !_selecting) {
						_selecting = true;
						SelectionStart = SelectionEnd = CaretOffset;
					}
					else if (_selecting) {
						UpdateSelection(CaretOffset >= offset);
					}
				}
			}
			else
				Cursor = null;
		}

		private void Grid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
			_selecting = _mouseLeftButtonDown = false;
			_root.ReleaseMouseCapture();
			InvalidateVisual();
		}

		private void UpdateSelection(bool expanding) {
			if (expanding) {
				if (CaretOffset >= SelectionStart && CaretOffset > SelectionEnd)
					SelectionEnd = CaretOffset - WordSize;
				else
					SelectionStart = CaretOffset;
			}
			else {
				if (CaretOffset > SelectionStart && CaretOffset <= SelectionEnd)
					SelectionEnd = CaretOffset - WordSize;
				else
					SelectionStart = CaretOffset;
			}

			InvalidateVisual();
		}

		public void SaveChanges() {
			foreach (var change in _changes.Values) {
				_accessor.Write(change.Offset, change.Value);
			}
			_changes.Clear();
			IsModified = false;
			InvalidateVisual();
		}

		public void SaveChangesAs(string newfilename) {
			if (_filename == null) {
				var offset = _changes.Max(change => change.Key);
				SaveChanges();

				// new file
				var bytes = new byte[offset];
				_accessor.ReadArray(0, bytes, 0, bytes.Length);
				File.WriteAllBytes(newfilename, bytes);
				_filename = newfilename;
			}
			else {
			}
		}

		public void DiscardChanges() {
			_changes.Clear();
			IsModified = false;
			InvalidateVisual();
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
	}
}
