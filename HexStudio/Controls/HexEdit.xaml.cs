using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace HexStudio.Controls {
	/// <summary>
	/// Interaction logic for HexEdit.xaml
	/// </summary>
	public partial class HexEdit {
		MemoryMappedFile _memFile;
		MemoryMappedViewAccessor _accessor;
		long _size;
		DispatcherTimer _timer;
		double _hexDataXPos, _hexDataWidth;
		long _startOffset, _endOffset;
		Dictionary<long, Point> _offsetsPositions = new Dictionary<long, Point>(128);
		Dictionary<int, long> _verticalPositions = new Dictionary<int, long>(128);
		Dictionary<long, EditChange> _changes = new Dictionary<long, EditChange>(128);
		double _charWidth;
		int _viewLines;
		//long _totalLines;

		public HexEdit() {
			InitializeComponent();

			_timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(.5) };
			_timer.Tick += _timer_Tick;
			_timer.Start();
		}

		private void _timer_Tick(object sender, EventArgs e) {
			if (CaretOffset < 0)
				_caret.Visibility = Visibility.Collapsed;
			else
				_caret.Visibility = _caret.Visibility == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;
		}


		public string Filename {
			get { return (string)GetValue(FilenameProperty); }
			set { SetValue(FilenameProperty, value); }
		}

		public static readonly DependencyProperty FilenameProperty =
			 DependencyProperty.Register(nameof(Filename), typeof(string), typeof(HexEdit), new PropertyMetadata(null, (s, e) => ((HexEdit)s).OnFilenameChanged(e)));


		public long CaretOffset {
			get { return (long)GetValue(CaretOffsetProperty); }
			set { SetValue(CaretOffsetProperty, value); }
		}

		public static readonly DependencyProperty CaretOffsetProperty =
			 DependencyProperty.Register(nameof(CaretOffset), typeof(long), typeof(HexEdit), new PropertyMetadata(-1L, (s, e) => ((HexEdit)s).OnCaretOffsetChanged(e), (s, e) => ((HexEdit)s).CoerceCaretOffset(e)));

		private object CoerceCaretOffset(object value) {
			var offset = (long)value;
			if (offset < 0)
				offset = 0;
			else if (offset >= _size)
				offset = _size - 1;
			return offset;
		}

		private void OnCaretOffsetChanged(DependencyPropertyChangedEventArgs e) {
			SetCaretPosition(CaretOffset);
			MakeVisible(CaretOffset);
			_inputIndex = _wordIndex = 0;
			_lastValue = 0;
		}

		private void SetCaretPosition(long caretOffset) {
			var pt = GetPositionByOffset(caretOffset);
			_caretPosition.X = pt.X;
			_caretPosition.Y = pt.Y;
		}

		public long SelectionStart {
			get { return (long)GetValue(SelectionStartProperty); }
			set { SetValue(SelectionStartProperty, value); }
		}

		public static readonly DependencyProperty SelectionStartProperty =
			 DependencyProperty.Register(nameof(SelectionStart), typeof(long), typeof(HexEdit), new PropertyMetadata(-1L));


		public long SelectionEnd {
			get { return (long)GetValue(SelectionEndProperty); }
			set { SetValue(SelectionEndProperty, value); }
		}

		public static readonly DependencyProperty SelectionEndProperty =
			 DependencyProperty.Register(nameof(SelectionEnd), typeof(long), typeof(HexEdit), new PropertyMetadata(-1L));

		public int BytesPerLine {
			get { return (int)GetValue(BytesPerLineProperty); }
			set { SetValue(BytesPerLineProperty, value); }
		}

		public static readonly DependencyProperty BytesPerLineProperty =
			 DependencyProperty.Register(nameof(BytesPerLine), typeof(int), typeof(HexEdit), new PropertyMetadata(32, (s, e) => ((HexEdit)s).Refresh()), ValidateBytesPerLine);

		private static bool ValidateBytesPerLine(object value) {
			var bytes = (int)value;
			if (bytes < 8 || bytes > 128)
				return false;
			return bytes % 8 == 0;
		}

		public double VerticalSpace {
			get { return (double)GetValue(VerticalSpaceProperty); }
			set { SetValue(VerticalSpaceProperty, value); }
		}

		public static readonly DependencyProperty VerticalSpaceProperty =
			 DependencyProperty.Register("VerticalSpace", typeof(double), typeof(HexEdit), new PropertyMetadata(2.0, (s, e) => ((HexEdit)s).Refresh()));

		public int WordSize {
			get { return (int)GetValue(WordSizeProperty); }
			set { SetValue(WordSizeProperty, value); }
		}

		public static readonly DependencyProperty WordSizeProperty =
			 DependencyProperty.Register("WordSize", typeof(int), typeof(HexEdit), new PropertyMetadata(1, (s, e) => ((HexEdit)s).Refresh()), ValidateWordSize);

		public Brush SelectionBackground {
			get { return (Brush)GetValue(SelectionBackgroundProperty); }
			set { SetValue(SelectionBackgroundProperty, value); }
		}

		public static readonly DependencyProperty SelectionBackgroundProperty =
			 DependencyProperty.Register("SelectionBackground", typeof(Brush), typeof(HexEdit),
				 new FrameworkPropertyMetadata(Brushes.Yellow, FrameworkPropertyMetadataOptions.AffectsRender));


		public Brush SelectionForeground {
			get { return (Brush)GetValue(SelectionForegroundProperty); }
			set { SetValue(SelectionForegroundProperty, value); }
		}

		public static readonly DependencyProperty SelectionForegroundProperty =
			 DependencyProperty.Register("SelectionForeground", typeof(Brush), typeof(HexEdit),
				 new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

		public bool IsReadOnly {
			get { return (bool)GetValue(IsReadOnlyProperty); }
			set { SetValue(IsReadOnlyProperty, value); }
		}

		public static readonly DependencyProperty IsReadOnlyProperty =
			 DependencyProperty.Register("IsReadOnly", typeof(bool), typeof(HexEdit), new PropertyMetadata(false));



		public bool IsModified {
			get { return (bool)GetValue(IsModifiedProperty); }
			private set { SetValue(IsModifiedProperty, value); }
		}

		public static readonly DependencyProperty IsModifiedProperty =
			 DependencyProperty.Register(nameof(IsModified), typeof(bool), typeof(HexEdit), new PropertyMetadata(false));


		private static bool ValidateWordSize(object value) {
			var wordSize = (int)value;
			return wordSize == 1 || wordSize == 2 || wordSize == 4 || wordSize == 8;
		}

		Func<byte[], int, string>[] _bitConverters = {
			(bytes, i) => bytes[i].ToString("X2"),
			(bytes, i) => BitConverter.ToUInt16(bytes, i).ToString("X4"),
			(bytes, i) => BitConverter.ToUInt32(bytes, i).ToString("X8"),
			(bytes, i) => BitConverter.ToUInt64(bytes, i).ToString("X16"),
		};

		int[] _bitConverterIndex = { 0, 0, 1, 1, 2, 2, 2, 2, 3, 3, 3 };

		private void Refresh() {
			Recalculate();
			InvalidateVisual();
			if (CaretOffset >= 0) {
				CaretOffset = CaretOffset - CaretOffset % WordSize;
				SetCaretPosition(CaretOffset);
			}
		}

		private void OnFilenameChanged(DependencyPropertyChangedEventArgs e) {
			if (_accessor != null)
				_accessor.Dispose();
			if (_memFile != null)
				_memFile.Dispose();

			var filename = (string)e.NewValue;
			_size = 0;
			if (filename != null) {
				_size = new FileInfo(filename).Length;
				_memFile = MemoryMappedFile.CreateFromFile(filename, FileMode.Open);
				_accessor = _memFile.CreateViewAccessor();
			}
			Refresh();
		}

		private void Recalculate() {
			_scroll.ViewportSize = ActualHeight;
			_scroll.Maximum = _size / BytesPerLine * (FontSize + VerticalSpace) - ActualHeight + VerticalSpace * 2;
		}

		private void _scroll_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
			InvalidateVisual();
		}

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

			var buf = new byte[end - start + 1];
			var read = _accessor.ReadArray(start, buf, 0, buf.Length);
			if (start + read < end)
				end = start + read;

			var sb = new StringBuilder(256); // entire row
			var changed_sb = new StringBuilder(256);     // changes

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

			for (long i = start; i < end; i += BytesPerLine) {
				var pos = 2 + (i / BytesPerLine) * lineHeight + y;
				sb.Clear();
				changed_sb.Clear();

				for (var j = i; j < i + BytesPerLine && j < end; j += WordSize) {
					int bufIndex = (int)(j - start);
					bool changed = false;
					for(int k = 0; k < WordSize; k++) {
						if (_changes.TryGetValue(j + k, out change)) {
							buf[bufIndex + k] = change.Value;
							changed = true;
						}
					}
					if (changed) {
						changed_sb.Append(bitConverter(buf, bufIndex)).Append(" ");
						sb.Append(space);
					}
					else {
						sb.Append(bitConverter(buf, bufIndex)).Append(" ");
						changed_sb.Append(space);
					}
				}

				var pt = new Point(x, pos);

				var text = new FormattedText(sb.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, FontSize, Foreground);
				if (len == 0)
					len = sb.Length;

				dc.DrawText(text, pt);
				if (text.WidthIncludingTrailingWhitespace > maxWidth) {
					maxWidth = text.WidthIncludingTrailingWhitespace;
					if (_charWidth < 1) {
						_charWidth = maxWidth / len;
					}
				}

				text = new FormattedText(changed_sb.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, FontSize, Brushes.Red);
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
				sb.Clear();
				changed_sb.Clear();

				for (var j = i; j < i + BytesPerLine && j < end; j++) {
					if (_changes.TryGetValue(j, out change)) {
						ch = (char)change.Value;
						changed_sb.Append(char.IsControl(ch) ? '.' : ch);
						sb.Append(' ');
					}
					else {
						ch = Encoding.ASCII.GetChars(buf, (int)(j - start), 1)[0];
						sb.Append(char.IsControl(ch) ? '.' : ch);
						changed_sb.Append(' ');
					}
				}

				var pt = new Point(x, pos);

				var text = new FormattedText(sb.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, FontSize, Foreground);
				dc.DrawText(text, pt);

				text = new FormattedText(changed_sb.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, FontSize, Brushes.Red);
				dc.DrawText(text, pt);
			}

			if (SelectionStart >= 0) {
				// draw current selection

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
			long offset;
			var y = (int)pt.Y;
			while (!_verticalPositions.TryGetValue(y, out offset))
				y--;
			int x = (int)(xp / (_charWidth * (WordSize * 2 + 1))) * WordSize;
			return offset + x;
		}

		private void This_SizeChanged(object sender, SizeChangedEventArgs e) {
			Refresh();
		}

		private void Grid_MouseWheel(object sender, MouseWheelEventArgs e) {
			_scroll.Value -= e.Delta;
		}

		private void _scroll_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
			var pt = e.GetPosition(this);
			CaretOffset = GetOffsetByCursorPosition(pt);
			Focus();
		}

		private void Grid_KeyDown(object sender, KeyEventArgs e) {
			if (CaretOffset < 0) {
			}
			else {
				switch (e.Key) {
					case Key.Down:
						CaretOffset += BytesPerLine;
						break;
					case Key.Up:
						CaretOffset -= BytesPerLine;
						break;
					case Key.Right:
						CaretOffset++;
						break;
					case Key.Left:
						CaretOffset--;
						break;
					case Key.PageDown:
						CaretOffset += BytesPerLine * _viewLines;
						break;
					case Key.PageUp:
						CaretOffset -= BytesPerLine * _viewLines;
						break;

					default:
						HandleTextEdit(e);
						break;
				}
			}
			e.Handled = true;
		}

		int _inputIndex = 0, _wordIndex = 0;
		byte _lastValue = 0;
		EditChange _currentChange;

		private void HandleTextEdit(KeyEventArgs e) {
			if (IsReadOnly) return;

			if ((e.Key < Key.D0 || e.Key > Key.D9) && (e.Key < Key.A || e.Key > Key.F))
				return;

			// make a change
			byte value = e.Key >= Key.A ? (byte)(e.Key - Key.A + 10) : (byte)(e.Key - Key.D0);
			_lastValue = (byte)(_lastValue * 16 + value);

			if (!IsModified)
				IsModified = true;

			if (_inputIndex == 0) {
				_currentChange = new EditChange { Offset = CaretOffset + _wordIndex };
				_changes[CaretOffset + _wordIndex] = _currentChange;
			}
			_currentChange.Value = _lastValue;
			if (++_inputIndex == 2) {
				_currentChange = null;
				_inputIndex = 0;
				if (++_wordIndex == WordSize) {
					CaretOffset += WordSize;
					_wordIndex = 0;
				}
			}
			InvalidateVisual();
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

		private void Grid_MouseMove(object sender, MouseEventArgs e) {
			// change cursor 
			var pt = e.GetPosition(this);
			if (pt.X >= _hexDataXPos && pt.X < _hexDataXPos + _hexDataWidth)
				Cursor = Cursors.IBeam;
			else
				Cursor = null;
		}
	}
}
