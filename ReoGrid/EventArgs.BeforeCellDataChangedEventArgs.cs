using System;
using System.Collections.Generic;
using System.Text;

namespace unvell.ReoGrid.Events
{
    public class BeforeCellDataChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Get instance of current editing cell. This property may be null.
        /// </summary>
        public Cell Cell { get; protected set; }
        public object NewData { get; protected set; }
        public object OldData { get; protected set; }
        public bool IsCancelled { get; set; }
        /// <summary>
        /// Create instance for CellEventArgs with specified cell.
        /// </summary>
        /// <param name="cell">Instance of current editing cell.</param>
        public BeforeCellDataChangedEventArgs(Cell cell, object newobj, object oldobj)
        {
            this.Cell = cell;
            this.NewData = newobj;
            this.OldData = oldobj;
            IsCancelled = false;
        }
    }
}
