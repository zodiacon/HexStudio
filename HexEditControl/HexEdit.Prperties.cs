using System;
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
			 DependencyProperty.Register(nameof(OverwriteMode), typeof(bool), typeof(HexEdit), new PropertyMetadata(true, (s, e) => ((HexEdit)s).OnOverwriteModeChanged(e)));

		private void OnOverwriteModeChanged(DependencyPropertyChangedEventArgs e) {
			ClearChange();
		}

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
			else if (offset >= _hexBuffer.Size && !IsReadOnly)
				offset = _hexBuffer.Size - 1;
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
					//Clipboard.SetData(DataFormats.Serializable, bytes);
				}
			}
			catch (OutOfMemoryException) {

			}
		}

		private void ExecuteSelectAll(ExecutedRoutedEventArgs e) {
			SelectionStart = 0;
			SelectionEnd = _hexBuffer.Size;
			InvalidateVisual();
		}

	}
}
