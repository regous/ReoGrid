using System;
using System.Collections.Generic;
using System.Text;

namespace unvell.ReoGrid.Events
{
    public class BeforeDragCellDataChangedEventArgs : EventArgs
    {
        public List<CellPosition> FromCells { get; internal set; }
        public List<CellPosition> ToCells { get; internal set; }
        public bool IsCancelled { get; set; } = false;
        /// <summary>
        /// Create instance for CellEventArgs with specified cell.
        /// </summary>
        /// <param name="cell">Instance of current editing cell.</param>
        public BeforeDragCellDataChangedEventArgs()
        {
            IsCancelled = false;
        }
    }
}
