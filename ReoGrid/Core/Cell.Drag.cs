using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using unvell.ReoGrid.CellTypes;
using unvell.ReoGrid.Data;
using unvell.ReoGrid.DataFormat;
using unvell.ReoGrid.Events;
using unvell.ReoGrid.Formula;
#if EX_SCRIPT
using unvell.ReoGrid.Script;
using unvell.ReoScript;
#endif

namespace unvell.ReoGrid
{
    public class DragCellData
    {
        public List<CellPosition> FromCells { get; internal set; }
        public List<CellPosition> ToCells { get; internal set; }
        public string OperationId { get; private set; } = Guid.NewGuid().ToString("D");
        public object Data { get; set; }
    }
    partial class Cell
    {
        public DataProvider DataProvider { get; set; }
        internal bool SetDragData(DragCellData value)
        {
            if (this.worksheet != null)
            {
                // update cell data
                return this.worksheet.SetDragSingleCellData(this, value);
            }
            else
            {
                this.InnerData = value.Data;
                return true;
            }
        }
    }

    partial class Worksheet
    {
        internal bool DragUpdateCellData(Cell cell, DragCellData data, Stack<List<Cell>> dirtyCellStack = null)
        {
            if (cell.body != null)
            {
                data.Data = cell.body.OnSetData(data.Data);
            }

            cell.InnerData = data.Data;

            if (this.HasSettings(WorksheetSettings.Edit_AutoFormatCell))
            {
                DataFormatterManager.Instance.FormatCell(cell);
            }
            else
            {
                cell.InnerDisplay = Convert.ToString(data.Data);
            }

#if WPF
            cell.formattedText = null;

            //if (cell.FormattedText == null || cell.FormattedText.Text != cell.InnerDisplay)
            //{
            //	float fontSize = cell.InnerStyle.FontSize * this.scaleFactor * (96f / 72f);

            //	cell.FormattedText = new System.Windows.Media.FormattedText(cell.InnerDisplay, 
            //		System.Globalization.CultureInfo.CurrentCulture,
            //		System.Windows.FlowDirection.LeftToRight,
            //		ResourcePoolManager.Instance.GetTypeface(cell.InnerStyle.FontName),
            //		fontSize,
            //		ResourcePoolManager.Instance.GetBrush(cell.InnerStyle.TextColor));
            //}
#endif

            return AfterDragCellDataUpdate(cell, data, dirtyCellStack);
        }
        private bool AfterDragCellDataUpdate(Cell cell, DragCellData data, Stack<List<Cell>> dirtyCellStack = null)
        {
#if FORMULA
            if ((this.settings & WorksheetSettings.Formula_AutoUpdateReferenceCell)
                == WorksheetSettings.Formula_AutoUpdateReferenceCell)
            {
                UpdateReferencedFormulaCells(cell, dirtyCellStack);
            }
#endif // FORMULA

#if DRAWING
            if (cell.Data is Drawing.RichText)
            {
                var rt = (Drawing.RichText)cell.Data;

                rt.SuspendUpdateText();
                rt.Size = cell.Bounds.Size;
                rt.TextWrap = cell.InnerStyle.TextWrapMode;
                rt.ResumeUpdateText();
                rt.UpdateText();
            }
            else
#endif // DRAWING
            {
                cell.FontDirty = true;
            }

            if (this.controlAdapter != null
                && !this.viewDirty
                && !this.suspendDataChangedEvent)
            {
                this.RequestInvalidate();
            }

            if (!this.suspendDataChangedEvent)
            {
                var header = this.cols[cell.Column];

                if (header.Body != null)
                {
                    header.Body.OnDataChange(cell.Row, cell.Row);
                }

                // raise text changed event
                if (RaiseDragCellDataChangedEvent(cell, data) == false)
                    return false;
            }
            return true;
        }

        internal bool RaiseDragCellDataChangedEvent(Cell cell, DragCellData data)
        {
            if (DragCellDataChanged != null)
            {
                DragCellEventArgs eventargs = new DragCellEventArgs(cell)
                {
                    FromCells = data.FromCells,
                    ToCells = data.ToCells
                };
                DragCellDataChanged(this, eventargs);
                if (eventargs.IsCancelled)
                {
                    return false;
                }
            }
#if EX_SCRIPT
            RaiseScriptEvent("ondatachange", new RSCellObject(this, cell.InternalPos, cell));
#endif
            return true;
        }

        internal bool SetDragSingleCellData(Cell cell, DragCellData data)
        {
            var args = new BeforeCellDataChangedEventArgs(cell, data.Data, cell.Data);
            BeforeCellDataChanged?.Invoke(this, args);
            if (args.IsCancelled)
                return false;
            // set cell body
            if (data.Data is ICellBody)
            {
                SetCellBody(cell, (ICellBody)data.Data);

                data.Data = cell.InnerData;
            }

            if (data.Data is string || data.Data is StringBuilder
#if EX_SCRIPT
 || data is StringObject
#endif // EX_SCRIPT
                )
            {
                string str = data.Data is string ? (string)(data.Data) : Convert.ToString(data.Data);

                // cell data processed as plain-text
                if (str.Length > 1)
                {
                    if (str[0] == '\'')
                    {

#if FORMULA
                        // clear old references
                        ClearCellReferenceList(cell);

                        // clear dependents arrows
                        RemoveCellTraceDependents(cell);

                        // clear precedents arrow
                        RemoveCellTracePrecedents(cell);

                        // clear formula status
                        cell.formulaStatus = FormulaStatus.Normal;

#endif // FORMULA

                        cell.InnerData = data.Data;
                        cell.InnerDisplay = str.Substring(1);

                        AfterCellDataUpdate(cell);
                        return true;
                    }

#if FORMULA
                    if (str[0] == '=')
                    {
                        SetCellFormula(cell, str.Substring(1));

                        try
                        {
                            RecalcCell(cell);
                        }
                        catch (Exception ex)
                        {
                            this.NotifyExceptionHappen(ex);
                        }

                        return true;
                    }
#endif // FORMULA

                }
            }

            // experimental: directly set an image as cell data
            //
            //else if (data is System.Drawing.Image)
            //{
            //	if (cell.body == null)
            //	{
            //		cell.Body = new ImageCell((System.Drawing.Image)data);
            //	}
            //	else if (cell.body is ImageCell)
            //	{
            //		((ImageCell)cell.body).Image = (System.Drawing.Image)data;
            //	}
            //}

#if FORMULA
            if (formulaRanges.Count > 0)
            {
                // clear old references
                ClearCellReferenceList(cell);
            }

            // clear cell formula
            cell.InnerFormula = null;

            // clear formula status
            cell.formulaStatus = FormulaStatus.Normal;

#endif // FORMULA

            return DragUpdateCellData(cell, data);
        }
    }
}
