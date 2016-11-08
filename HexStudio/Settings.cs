using HexStudio.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HexStudio {
	public class Settings {
		public List<string> RecentFiles { get; set; }
		public RgbColor TextForeground { get; set; }
		public double FontSize { get; set; }
	}
}
