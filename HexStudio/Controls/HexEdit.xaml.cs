using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using Zodiacon.WPF;

namespace HexStudio.Controls {
	/// <summary>
	/// Interaction logic for HexEdit.xaml
	/// </summary>
	public partial class HexEdit : IDisposable {
		MemoryMappedFile _memFile;
		MemoryMappedViewAccessor _accessor;
		long _size, _sizeLimit;
		readonly DispatcherTimer _timer;
		double _hexDataXPos, _hexDataWidth;
		long _startOffset, _endOffset;
		double _charWidth;
		int _viewLines;

		readonly Dictionary<long, Point> _offsetsPositions = new Dictionary<long, Point>(128);
		readonly Dictionary<int, long> _verticalPositions = new Dictionary<int, long>(128);
		readonly Dictionary<long, EditChange> _changes = new Dictionary<long, EditChange>(128);
		readonly AppCommandManager _commandManager = new AppCommandManager();

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

		private void ExecutePaste(ExecutedRoutedEventArgs e) {
			throw new NotImplementedException();
		}

		private void CanExecutePaste(CanExecuteRoutedEventArgs e) {
			e.CanExecute = Clipboard.ContainsData(DataFormats.Serializable);
		}

		private void CanExecuteCopy(CanExecuteRoutedEventArgs e) {
			e.CanExecute = SelectionStart >= 0 && SelectionEnd - SelectionStart > 0;
		}

		private void ExecuteCopy(ExecutedRoutedEventArgs e) {
			try {
				var count = SelectionEnd - SelectionStart + 1;
				if (count > 1L << 31 - 1) {
					// too large, raise event
				}
				else {
					var bytes = new byte[count];
					_accessor.ReadArray(SelectionStart, bytes, 0, bytes.Length);
					Clipboard.SetData(DataFormats.Serializable, bytes);
				}
			}
			catch (OutOfMemoryException) {
								
			}
		}

		private void ExecuteSelectAll(ExecutedRoutedEventArgs e) {
			SelectionStart = 0;
			SelectionEnd = _size;
			InvalidateVisual();
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
			 DependencyProperty.Register(nameof(Filename), typeof(string), typeof(HexEdit),
				 new PropertyMetadata(null, (s, e) => ((HexEdit)s).OnFilenameChanged(e)));


		public long CaretOffset {
			get { return (long)GetValue(CaretOffsetProperty); }
			set { SetValue(CaretOffsetProperty, value); }
		}

		public static readonly DependencyProperty CaretOffsetProperty =
			 DependencyProperty.Register(nameof(CaretOffset), typeof(long), typeof(HexEdit),
				 new PropertyMetadata(0L, (s, e) => ((HexEdit)s).OnCaretOffsetChanged(e), (s, e) => ((HexEdit)s).CoerceCaretOffset(e)));

		private object CoerceCaretOffset(object value) {
			var offset = (long)value;
			if (offset < 0)
				offset = 0;
			else if (_sizeLimit > 0 && offset >= _sizeLimit)
				offset = _sizeLimit - 1;
			else if (offset >= _size && !IsReadOnly)
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
			set { SetValue(IsModifiedProperty, value); }
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

		public void CreateNew(long sizeLimit = 1 << 20) {
			Dispose();
			_memFile = MemoryMappedFile.CreateNew(null, _sizeLimit = sizeLimit);
			_size = WordSize;
			_accessor = _memFile.CreateViewAccessor();
		}

		private void OnFilenameChanged(DependencyPropertyChangedEventArgs e) {
			Dispose();

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

			var buf = new byte[end - start + 1 + 7];     // spare bytes

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

					if (j >= SelectionStart && j <= SelectionEnd) {
						// j is selected
						dc.DrawRectangle(SelectionBackground, null, new Rect(x + (j - i) / WordSize * (2 * WordSize + 1) * _charWidth, pos, _charWidth * WordSize * 2, FontSize));
					}

					for (int k = 0; k < WordSize; k++) {
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
					if (SelectionStart <= j && j <= SelectionEnd) {
						// j is selected
						dc.DrawRectangle(SelectionBackground, null, new Rect(x + _charWidth * (j - i), pos, _charWidth, FontSize));
					}

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
				_root.Focus();
			}
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
					if (CaretOffset + WordSize >= _size)
						_size = CaretOffset + 2 * WordSize;
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
					else if(_selecting) {
						UpdateSelection(CaretOffset >= offset);
						Debug.WriteLine($"Updating selection {CaretOffset}");
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
			if (Filename == null) {
				var offset = _changes.Max(change => change.Key);
				SaveChanges();

				// new file
				var bytes = new byte[offset];
				_accessor.ReadArray(0, bytes, 0, bytes.Length);
				File.WriteAllBytes(newfilename, bytes);
				Filename = newfilename;
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
			if (_accessor != null)
				_accessor.Dispose();
			if (_memFile != null)
				_memFile.Dispose();
		}
	}
}
