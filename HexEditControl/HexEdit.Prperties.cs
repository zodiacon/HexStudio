using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Zodiacon.HexEditControl {
	partial class HexEdit {
		public bool OverwriteMode {
			get { return (bool)GetValue(OverwriteModeProperty); }
			set { SetValue(OverwriteModeProperty, value); }
		}

		public static readonly DependencyProperty OverwriteModeProperty =
			 DependencyProperty.Register(nameof(OverwriteMode), typeof(bool), typeof(HexEdit),
				 new PropertyMetadata(true, (s, e) => ((HexEdit)s).OnOverwriteModeChanged(e)));

		private void OnOverwriteModeChanged(DependencyPropertyChangedEventArgs e) {
			ClearChange();
			UpdateCaretWidth();
		}

        public static readonly RoutedEvent CaretPositionChangedEvent =
            EventManager.RegisterRoutedEvent(nameof(CaretPositionChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(HexEdit));

        public event RoutedEventHandler CaretPositionChanged {
            add { AddHandler(CaretPositionChangedEvent, value); }
            remove { RemoveHandler(CaretPositionChangedEvent, value); }
        }

		void UpdateCaretWidth() {
			_caret.Width = OverwriteMode ? (_charWidth < 1 ? 8 : _charWidth) : SystemParameters.CaretWidth;
		}

		public long SelectionLength => SelectionStart < 0 ? 0 : SelectionEnd - SelectionStart + WordSize;

		public Brush EditForeground {
			get { return (Brush)GetValue(EditForegroundProperty); }
			set { SetValue(EditForegroundProperty, value); }
		}

		public static readonly DependencyProperty EditForegroundProperty =
			 DependencyProperty.Register(nameof(EditForeground), typeof(Brush), typeof(HexEdit),
				 new FrameworkPropertyMetadata(Brushes.Red, FrameworkPropertyMetadataOptions.AffectsRender));

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
			else if (_hexBuffer != null && offset > _hexBuffer.Size)
				offset = _hexBuffer.Size;
			return offset;
		}

		private void OnCaretOffsetChanged(DependencyPropertyChangedEventArgs e) {
			SetCaretPosition(CaretOffset);
			MakeVisible(CaretOffset);
            RaiseEvent(new RoutedEventArgs(CaretPositionChangedEvent));
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
			 DependencyProperty.Register(nameof(BytesPerLine), typeof(int), typeof(HexEdit), 
                 new PropertyMetadata(32, (s, e) => ((HexEdit)s).Refresh()), ValidateBytesPerLine);

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
			 DependencyProperty.Register(nameof(VerticalSpace), typeof(double), typeof(HexEdit), new PropertyMetadata(2.0, (s, e) => ((HexEdit)s).Refresh()));

		public int WordSize {
			get { return (int)GetValue(WordSizeProperty); }
			set { SetValue(WordSizeProperty, value); }
		}

		public static readonly DependencyProperty WordSizeProperty =
			 DependencyProperty.Register(nameof(WordSize), typeof(int), typeof(HexEdit), new PropertyMetadata(1,
				 (s, e) => ((HexEdit)s).Refresh()), ValidateWordSize);

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



		public bool ShowOffset {
			get { return (bool)GetValue(ShowOffsetProperty); }
			set { SetValue(ShowOffsetProperty, value); }
		}

		public static readonly DependencyProperty ShowOffsetProperty =
			DependencyProperty.Register(nameof(ShowOffset), typeof(bool), typeof(HexEdit), 
				new PropertyMetadata(true, (s, e) => ((HexEdit)s).Refresh()));


		private static bool ValidateWordSize(object value) {
			var wordSize = (int)value;
			return wordSize == 1 || wordSize == 2 || wordSize == 4 || wordSize == 8;
		}

		private string FormatBytes(byte[] bytes, int wordSize) {
			var sb = new StringBuilder((wordSize + 1) * bytes.Length);
			for (int i = 0; i < bytes.Length; i += wordSize)
				sb.Append(_bitConverters[_bitConverterIndex[wordSize]](bytes, i)).Append(" ");
			return sb.ToString();
		}

		private void ExecuteSelectAll(ExecutedRoutedEventArgs e) {
			SelectionStart = 0;
			SelectionEnd = _hexBuffer.Size;
			InvalidateVisual();
		}

	}
}
