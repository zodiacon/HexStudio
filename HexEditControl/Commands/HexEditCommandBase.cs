using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zodiacon.WPF;

namespace Zodiacon.HexEditControl.Commands {
	abstract class HexEditCommandBase : IAppCommand {
		public string Description { get; set; }

		public string Name { get; set; }
		public HexEdit HexEdit { get; }

		protected HexEditCommandBase(HexEdit hexEdit) {
			HexEdit = hexEdit;
		}

		protected virtual void Invalidate(bool modified = true) {
			if(modified)
				HexEdit.IsModified = true;
			HexEdit.InvalidateVisual();
		}

		public abstract void Execute();

		public abstract void Undo();
	}
}
