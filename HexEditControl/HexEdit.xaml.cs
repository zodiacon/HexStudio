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
using Zodiacon.HexEditControl.Commands;
using Zodiacon.WPF;

namespace Zodiacon.HexEditControl {
	/// <summary>
	/// Interaction logic for HexEdit.xaml
	/// </summary>
	public partial class HexEdit : IDisposable {
		long _sizeLimit;
		readonly DispatcherTimer _timer;
		double _hexDataXPos, _hexDataWidth;
		long _startOffset = -1, _endOffset = -1;
		double _charWidth;
		int _viewLines;

		ByteBuffer _hexBuffer;
		readonly Dictionary<long, Point> _offsetsPositions = new Dictionary<long, Point>(128);
		readonly Dictionary<int, long> _verticalPositions = new Dictionary<int, long>(128);

		public ByteBuffer Buffer => _hexBuffer;

		public HexEdit() {
			InitializeComponent();

			_timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(.5) };
			_timer.Tick += _timer_Tick;
			_timer.Start();

			// create new document by default
			CreateNew();

			Loaded += delegate {
				UpdateCaretWidth();
				_root.Focus();
			};
		}

		static HexEdit() {
			RegisterCommands();
		}

		private void _timer_Tick(object sender, EventArgs e) {
			if (CaretOffset < 0 && _hexBuffer.Size > 0)
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
				ClearChange();
				CaretOffset -= CaretOffset % WordSize;
				SetCaretPosition(CaretOffset);
			}
		}

		public void CreateNew(long sizeLimit = 1 << 20) {
			Dispose();
			_hexBuffer = new ByteBuffer(0, _sizeLimit = sizeLimit);
		}

		public void OpenFile(string filename) {
			if (string.IsNullOrWhiteSpace(filename))
				throw new ArgumentException("Filename is empty or null", nameof(filename));

			Dispose();

			_sizeLimit = 0;
			_hexBuffer = new ByteBuffer(filename);
			Refresh();
		}

		private void Recalculate() {
			_scroll.ViewportSize = ActualHeight;
			_scroll.Maximum = _hexBuffer.Size / BytesPerLine * (FontSize + VerticalSpace) - ActualHeight + VerticalSpace * 2;
		}

		private void _scroll_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
			InvalidateVisual();
		}

		byte[] _readBuffer = new byte[1 << 16];
		StringBuilder _hexString = new StringBuilder(256);       // hex string
		StringBuilder _hexChangesString = new StringBuilder(256);     // changes string

		protected override void OnRender(DrawingContext dc) {
			if (_hexBuffer == null || ActualHeight < 5) return;

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
			if (end > _hexBuffer.Size)
				end = _hexBuffer.Size;

			var maxWidth = 0.0;
			bool empty = _hexBuffer.Size == 0;

			for (long i = start; i < end || empty; i += BytesPerLine) {
				var pos = 2 + (i / BytesPerLine) * lineHeight + y;
				var text = new FormattedText(i.ToString("X8") + ": ", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, FontSize, Foreground);
				if (text.Width > maxWidth)
					maxWidth = text.Width;
				dc.DrawText(text, new Point(2, pos));
				if (empty)
					break;
			}

			var x = maxWidth + 8;

			int readSize = (int)(end - start + 1 + 7);

			List<OffsetRange> changes = new List<OffsetRange>();

			var read = _hexBuffer.GetBytes(start, readSize, _readBuffer, changes);
			if (start + read < end)
				end = start + read;

			_hexDataXPos = x;

			var singleChar = new FormattedText("8", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, FontSize, Foreground);

			maxWidth = 0;
			_offsetsPositions.Clear();
			_verticalPositions.Clear();

			var bitConverter = _bitConverters[_bitConverterIndex[WordSize]];
			_viewLines = 0;
			var space = new string(' ', WordSize * 2 + 1);
			int len = 0;

			int readIndex = 0;
			for (long i = start; i < end; i += BytesPerLine) {
				var pos = 2 + (i / BytesPerLine) * lineHeight + y;
				_hexString.Clear();
				_hexChangesString.Clear();

				for (var j = i; j < i + BytesPerLine && j < end; j += WordSize) {
					int bufIndex = (int)(j - start);

					if (j >= SelectionStart && j <= SelectionEnd) {
						// j is selected
						dc.DrawRectangle(SelectionBackground, null, new Rect(x + (j - i) / WordSize * (2 * WordSize + 1) * _charWidth, pos, _charWidth * WordSize * 2, FontSize));
					}

					var changed = changes.Any(change => j >= change.Offset && j < change.Offset + change.Count);
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

			if (empty) {
				_offsetsPositions[0] = new Point(x, 2 + y);
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

					var changed = changes.Any(change => j >= change.Offset && j < change.Offset + change.Count);

					ch = (char)_readBuffer[j - start];
					if (changed) {
						_hexChangesString.Append(char.IsControl(ch) ? '.' : ch);
						_hexString.Append(' ');
					}
					else {
						_hexString.Append(char.IsControl(ch) ? '.' : ch);
						_hexChangesString.Append(' ');
					}
				}

				var pt = new Point(x, pos);

				var text = new FormattedText(_hexString.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, FontSize, Foreground);
				dc.DrawText(text, pt);

				text = new FormattedText(_hexChangesString.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, FontSize, EditForeground);
				dc.DrawText(text, pt);
			}

			_endOffset = end;
			_startOffset = start;

			dc.Pop();

		}

		Point GetPositionByOffset(long offset) {
			if (offset < 0 && _hexBuffer.Size == 0)
				offset = 0;
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
			while (!_verticalPositions.TryGetValue(y, out offset) && y > -5)
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
					case Key.Insert:
						OverwriteMode = !OverwriteMode;
						break;

					case Key.Down:
						CaretOffset += BytesPerLine;
						break;

					case Key.Up:
						CaretOffset -= BytesPerLine;
						break;

					case Key.Right:
						//if (!_selecting && CaretOffset + WordSize >= _hexBuffer.Size) {
						//	_hexBuffer.Size = CaretOffset + WordSize * 2;
						//	_accessor.WriteArray(_hexBuffer.Size - WordSize, _zeros, 0, WordSize);
						//}
						CaretOffset += WordSize;
						break;

					case Key.Left:
						CaretOffset -= WordSize;
						break;

					case Key.PageDown:
						if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
							CaretOffset = _hexBuffer.Size - _hexBuffer.Size % WordSize - 1;
						else
							CaretOffset += BytesPerLine * _viewLines;
						break;

					case Key.PageUp:
						if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
							CaretOffset = 0;
						else
							CaretOffset -= BytesPerLine * _viewLines;
						break;

					case Key.Back:				
						if (CaretOffset < WordSize)
							break;
						CaretOffset -= WordSize;
						goto case Key.Delete;

					case Key.Delete:
						ClearChange();

						if (SelectionLength > 0) {
                            var cmd = new DeleteBulkTextCommand(this, Range.FromStartToEnd(SelectionStart, SelectionEnd));
                            _commandManager.AddCommand(cmd);
                            ClearSelection();
						}
						else {
							// delete
							_hexBuffer.Delete(Range.FromStartAndCount(CaretOffset, WordSize));
						}
						if (!IsModified)
							IsModified = true;
						InvalidateVisual();
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
					ClearChange();
				}
				_root.Focus();
			}
		}

		void ClearChange() {
			_currentChange = null;
			_inputIndex = _wordIndex = 0;
			_lastValue = 0;
			CaretOffset -= CaretOffset % WordSize;
		}

		int _inputIndex = 0, _wordIndex = 0;
		byte _lastValue = 0;
		ByteRange _currentChange;

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

			if (_currentChange == null) {
				// create a new change set
				var overwrite = OverwriteMode;
				if (CaretOffset == _hexBuffer.Size)
					overwrite = false;

				_currentChange = new ByteRange(CaretOffset);
				_hexBuffer.AddChange(_currentChange, OverwriteMode);
			}

			if (_inputIndex == 0) {
				_currentChange.AddData(_lastValue);
				_hexBuffer.UpdateChange();
			}
			else {
				_currentChange.SetData((int)_currentChange.Count - 1, _lastValue);
			}
			if (++_inputIndex == 2) {
				_inputIndex = 0;
				if (_wordIndex % 2 == 1)
					_currentChange.SwapLastBytes(_wordIndex + 1);

				if (++_wordIndex == WordSize) {
					CaretOffset += WordSize;
					_wordIndex = 0;
				}
			}
			InvalidateVisual();
		}

		private void ClearSelection() {
			SelectionStart = SelectionEnd = -1;
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
			var pt = e.GetPosition(_root);
			CaretOffset = GetOffsetByCursorPosition(pt);
			_root.Focus();
			_root.CaptureMouse();
			_currentChange = null;
			_mouseLeftButtonDown = true;
			ClearSelection();
		}

		private void Grid_MouseMove(object sender, MouseEventArgs e) {
			// change cursor 
			var pt = e.GetPosition(_root);
			if (pt.X >= _hexDataXPos - 2 && pt.X < _hexDataXPos + _hexDataWidth) {
				Cursor = Cursors.IBeam;
				if (_mouseLeftButtonDown) {
					var offset = CaretOffset;
					CaretOffset = GetOffsetByCursorPosition(pt);
					if (offset != CaretOffset && !_selecting) {
						_selecting = true;
						SelectionStart = SelectionEnd = offset;
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
			_hexBuffer.ApplyChanges();
			ClearChange();
			IsModified = false;
			InvalidateVisual();
		}

		private void _root_MouseRightButtonDown(object sender, MouseButtonEventArgs e) {
			var pt = e.GetPosition(this);
			CaretOffset = GetOffsetByCursorPosition(pt);
			_root.Focus();
			_currentChange = null;
		}

		public void SaveChangesAs(string newfilename) {
			_hexBuffer.SaveToFile(newfilename);
			ClearChange();
			IsModified = false;
			InvalidateVisual();
		}

		public void DiscardChanges() {
			_hexBuffer.DiscardChanges();
			IsModified = false;
			InvalidateVisual();
		}

		public void Dispose() {
			if (_hexBuffer != null) {
				_hexBuffer.Dispose();
				_hexBuffer = null;
			}
		}
	}
}
