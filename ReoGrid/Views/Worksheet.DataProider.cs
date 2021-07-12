using System;
using System.Collections.Generic;
using System.Text;

namespace unvell.ReoGrid
{
    using System.Linq;
    using unvell.ReoGrid.Data;
    using unvell.ReoGrid.Views;

    partial class Worksheet
    {
        public IList<DataProvider> DataProviders
        {
            get
            {
                SheetViewport stport = (this.viewportController as NormalViewportController).View.Children.FirstOrDefault(x => x is SheetViewport) as SheetViewport;
                if (stport == null)
                    return null;
                CellsViewport cvport = stport.Children.FirstOrDefault(x => x is CellsViewport) as CellsViewport;
                if (cvport == null)
                    return null;
                return cvport.DataProviders;
            }
        }
        public void RegisterDataProvider(DataProvider dataprovider)
        {
            SheetViewport stport = (this.viewportController as NormalViewportController).View.Children.FirstOrDefault(x => x is SheetViewport) as SheetViewport;
            if (stport == null)
                return;
            CellsViewport cvport = stport.Children.FirstOrDefault(x => x is CellsViewport) as CellsViewport;
            if (cvport == null)
                return;
            cvport.RegisterDataProvider(dataprovider);
        }
        public void UnregisterDataProvider(DataProvider dataprovider)
        {
            SheetViewport stport = (this.viewportController as NormalViewportController).View.Children.FirstOrDefault(x => x is SheetViewport) as SheetViewport;
            if (stport == null)
                return;
            CellsViewport cvport = stport.Children.FirstOrDefault(x => x is CellsViewport) as CellsViewport;
            if (cvport == null)
                return;
            cvport.UnregisterDataProvider(dataprovider);
        }
    }
}

