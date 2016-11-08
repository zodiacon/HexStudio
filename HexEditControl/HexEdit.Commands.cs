using System;
using System.Windows;
using System.Windows.Input;
using Zodiacon.HexEditControl.Commands;
using Zodiacon.WPF;

namespace Zodiacon.HexEditControl {
	partial class HexEdit {
		readonly AppCommandManager _commandManager = new AppCommandManager();

		static void RegisterCommands() {
			CommandManager.RegisterClassCommandBinding(typeof(HexEdit), new CommandBinding(ApplicationCommands.SelectAll,
				(s, e) => ((HexEdit)s).ExecuteSelectAll(e)));
			CommandManager.RegisterClassCommandBinding(typeof(HexEdit), new CommandBinding(ApplicationCommands.Copy,
				(s, e) => ((HexEdit)s).ExecuteCopy(e), (s, e) => ((HexEdit)s).CanExecuteCopy(e)));
			CommandManager.RegisterClassCommandBinding(typeof(HexEdit), new CommandBinding(ApplicationCommands.Paste,
				(s, e) => ((HexEdit)s).ExecutePaste(e), (s, e) => ((HexEdit)s).CanExecuteCut(e)));
			CommandManager.RegisterClassCommandBinding(typeof(HexEdit), new CommandBinding(ApplicationCommands.Cut,
				(s, e) => ((HexEdit)s).ExecuteCut(e), (s, e) => ((HexEdit)s).CanExecuteCut(e)));
			CommandManager.RegisterClassCommandBinding(typeof(HexEdit), new CommandBinding(ApplicationCommands.Undo,
				(s, e) => ((HexEdit)s).ExecuteUndo(e), (s, e) => ((HexEdit)s).CanExecuteUndo(e)));
			CommandManager.RegisterClassCommandBinding(typeof(HexEdit), new CommandBinding(ApplicationCommands.Redo,
				(s, e) => ((HexEdit)s).ExecuteRedo(e), (s, e) => ((HexEdit)s).CanExecuteRedo(e)));
            CommandManager.RegisterClassCommandBinding(typeof(HexEdit), new CommandBinding(ApplicationCommands.Delete,
                (s, e) => ((HexEdit)s).ExecuteDelete(e), (s, e) => ((HexEdit)s).CanExecuteDelete(e)));
        }

        private void CanExecuteDelete(CanExecuteRoutedEventArgs e) {
            e.CanExecute = !IsReadOnly && (SelectionLength > 0 || CaretOffset < _hexBuffer.Size - WordSize);
        }

        private void ExecuteDelete(ExecutedRoutedEventArgs e) {
            if (SelectionLength > 0) {
                AddCommand(new DeleteBulkTextCommand(this, Range.FromStartToEnd(SelectionStart, SelectionEnd)));
                ClearSelection();
            }
            else {
                AddCommand(new DeleteBulkTextCommand(this, Range.FromStartAndCount(CaretOffset, WordSize)));
            }
        }

        void AddCommand(IAppCommand cmd, bool execute = true) {
            _commandManager.AddCommand(cmd);
            IsModified = _commandManager.CanUndo;
            InvalidateVisual();
        }

		private void CanExecuteUndo(CanExecuteRoutedEventArgs e) {
			e.CanExecute = _commandManager.CanUndo;
		}

		private void ExecuteUndo(ExecutedRoutedEventArgs e) {
			_commandManager.Undo();
            IsModified = _commandManager.CanUndo;
		}

		private void ExecuteRedo(ExecutedRoutedEventArgs e) {
			_commandManager.Redo();
            IsModified = _commandManager.CanUndo;
        }

        private void CanExecuteRedo(CanExecuteRoutedEventArgs e) {
			e.CanExecute = _commandManager.CanRedo;
		}

		private void ExecutePaste(ExecutedRoutedEventArgs e) {
			var bytes = (byte[])Clipboard.GetData(DataFormats.Serializable);
			var br = new ByteRange(CaretOffset, bytes);

			var cmd = new AddBulkTextCommand(this, br, OverwriteMode);
			_commandManager.AddCommand(cmd);
		}

		private void CanExecuteCut(CanExecuteRoutedEventArgs e) {
            e.CanExecute = !IsReadOnly && SelectionLength > 0 && SelectionLength < 1 << 30;
		}

		private void CanExecuteCopy(CanExecuteRoutedEventArgs e) {
			e.CanExecute = SelectionLength > 0 && SelectionLength < 1 << 30;
		}

		private void ExecuteCopy(ExecutedRoutedEventArgs e) {
			try {
				var count = SelectionLength;
				var bytes = new byte[count];
				_hexBuffer.GetBytes(SelectionStart, (int)count, bytes);
				var data = new DataObject(DataFormats.Serializable, bytes);
				data.SetText(FormatBytes(bytes, WordSize));
				Clipboard.SetDataObject(data, true);
			}
			catch (OutOfMemoryException) {

			}
		}
		private void ExecuteCut(ExecutedRoutedEventArgs e) {
            ExecuteCopy(e);
            ExecuteDelete(e);
		}
	}
}

