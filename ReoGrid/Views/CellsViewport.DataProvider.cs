using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using unvell.ReoGrid.Data;
using unvell.ReoGrid.Graphics;
using unvell.ReoGrid.Rendering;

namespace unvell.ReoGrid.Views
{
    partial class CellsViewport
    {
        public void RegisterDataProvider(DataProvider provider)
        {
            if (DataProviders.Contains(provider))
                return;
            if (provider.Trigger.TryGetTarget(out var trigger) && trigger != null)
            {
                sheet.workbook.ControlInstance.Children.Add(trigger);
                trigger.Visibility = System.Windows.Visibility.Collapsed;
            }
            if (provider.Selector.TryGetTarget(out var selector) && selector != null)
            {
                sheet.workbook.ControlInstance.Children.Add(selector);
            }

            DataProviders.Add(provider);
        }
        public void UnregisterDataProvider(DataProvider provider)
        {
            if (!DataProviders.Contains(provider))
                return;
            for (int i = 0; i < sheet.RowCount; i++)
            {
                for (int j = 0; j < sheet.ColumnCount; j++)
                {
                    Cell cell = sheet.GetCell(i, j);
                    if (cell == null) continue;
                    if (cell.DataProvider == provider)
                        cell.DataProvider = null;
                }
            }
            if (provider.Trigger.TryGetTarget(out var trigger) && trigger != null)
            {
                trigger.Visibility = System.Windows.Visibility.Collapsed;
                if (sheet.workbook.ControlInstance.Children.Contains(trigger))
                    sheet.workbook.ControlInstance.Children.Remove(trigger);
            }
            if (provider.Selector.TryGetTarget(out var selector) && selector != null)
            {
                if (sheet.workbook.ControlInstance.Children.Contains(selector))
                    sheet.workbook.ControlInstance.Children.Remove(selector);
            }

            DataProviders.Remove(provider);
        }

        public System.Collections.Generic.List<DataProvider> DataProviders { get; set; }
			= new System.Collections.Generic.List<DataProvider>();
		private void DrawDataProvider(CellDrawingContext dc)
		{
            if (dc.DrawMode != DrawMode.View) return;
            if (DataProviders.Count == 0)
                return;
  

            if (sheet.SelectionRange.IsEmpty || dc.DrawMode != DrawMode.View)
            {
                DataProviders.ForEach(x =>
                {
                    if (x.Trigger.TryGetTarget(out var trigger) && trigger != null)
                    {
                        trigger.Visibility = System.Windows.Visibility.Collapsed;
                    }
                });
                DataProviders.ForEach(x =>
                {
                    if (x.Selector.TryGetTarget(out var selector) && selector != null)
                    {
                        selector.IsOpen = false;
                    }
                });
            }
            else
            {
                Cell cell = sheet.GetCell(sheet.SelectionRange.StartPos);
                for (int i = 0; i < DataProviders.Count; i++)
                {
                    DataProvider dp = DataProviders[i];
                    if (dp.ActiveCell == null) continue;
                    if (dp.ActiveCell.TryGetTarget(out var tc) && tc == cell) continue;
                    if (dp.Trigger.TryGetTarget(out var trigger) && trigger != null)
                    {
                        if(trigger.Visibility != System.Windows.Visibility.Collapsed)
                            trigger.Visibility = System.Windows.Visibility.Collapsed;
                    }
                    if (dp.Selector.TryGetTarget(out var selector) && selector != null)
                    {
                        if (selector.IsOpen) 
                            selector.IsOpen = false;
                    }
                }

                WPFGraphics g = dc.Graphics as WPFGraphics;

                
                if (cell == null || cell.DataProvider == null) return;
                
                if (g.TransformStack.Count != 0)
                {
                    MatrixTransform mt = g.TransformStack.Peek();
                    if (mt.TryTransform(new System.Windows.Point(cell.Right, cell.Top), out var righttop))
                    {
                        if (righttop.X < this.Left || righttop.Y < this.Top)
                        {
                            if (cell.DataProvider.Trigger.TryGetTarget(out var trigger))
                            {
                                trigger.Visibility = System.Windows.Visibility.Collapsed;
                            }
                        }
                        else
                        {
                            mt.TryTransform(new System.Windows.Point(cell.Left, cell.Bottom), out var leftbottom);
                            if (cell.DataProvider.Trigger.TryGetTarget(out var trigger) && trigger != null)
                            {
                                bool flag = false;
                                if (!IsEqual(trigger.Height, cell.Height))
                                {
                                    trigger.Height = cell.Height;
                                    flag = true;
                                }

                                if (trigger.Margin.Right != 0 ||
                                    trigger.Margin.Bottom != 0 ||
                                    !IsEqual(trigger.Margin.Left, righttop.X) ||
                                    !IsEqual(trigger.Margin.Left, righttop.X))
                                {
                                    trigger.Margin = new System.Windows.Thickness(righttop.X, righttop.Y, 0, 0);
                                    flag = true;
                                }

                                if (trigger.Visibility != System.Windows.Visibility.Visible)
                                {
                                    trigger.Visibility = System.Windows.Visibility.Visible;
                                    flag = true;
                                }

                                if (flag)
                                {
                                    Point position = (trigger.Parent as Canvas).PointToScreen(leftbottom);
                                    Rectangle rectangle = new Rectangle(position.X, position.Y, righttop.X - leftbottom.X, righttop.Y - leftbottom.Y);
                                    cell.DataProvider.Update(rectangle, cell);
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (cell.DataProvider.Trigger.TryGetTarget(out var trigger))
                    {
                        Rectangle scaledSelectionRect = GetScaledAndClippedRangeRect(this,
                            sheet.SelectionRange.StartPos, sheet.SelectionRange.StartPos, 0);
                        trigger.Margin = new System.Windows.Thickness(this.Left + scaledSelectionRect.Right, this.Top + scaledSelectionRect.Top, 0, 0);
                        trigger.Visibility = System.Windows.Visibility.Visible;
                    }
                }
            }
        }

        public bool IsEqual(double v1,double v2)
        {
            return System.Math.Abs(v1 - v2) < 0.1;
        }
    }
}
