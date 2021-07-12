using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using unvell.ReoGrid.Graphics;

namespace unvell.ReoGrid.Data
{
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct StructPoint
    {
        public int X;
        public int Y;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct MOUSELLHookStruct
    {
        public StructPoint Point;
        public int MouseData;
        public int Flags;
        public int Time;
        public int ExtraInfo;
    }
    public enum HookType : int
    {
        WH_JOURNALRECORD = 0,
        WH_JOURNALPLAYBACK = 1,
        WH_KEYBOARD = 2,
        WH_GETMESSAGE = 3,
        WH_CALLWNDPROC = 4,
        WH_CBT = 5,
        WH_SYSMSGFILTER = 6,
        WH_MOUSE = 7,
        WH_HARDWARE = 8,
        WH_DEBUG = 9,
        WH_SHELL = 10,
        WH_FOREGROUNDIDLE = 11,
        WH_CALLWNDPROCRET = 12,
        WH_KEYBOARD_LL = 13,
        WH_MOUSE_LL = 14
    }

    //public interface IDataProviderSelector
    //{

    //    Action<object> SelectedItemChangedCallback { get; set; }
    //}
    public class SelectorOpeningEventArgs : EventArgs
    {
        public SelectorOpeningEventArgs(Cell cell)
        {
            Cell = cell;
        }
        public Cell Cell { get;private set; }
        public bool IsCancelled { get; set; } = false;
    }
    public class SelectorClosedEventArgs : EventArgs
    {
        public SelectorClosedEventArgs(Cell cell,object selecteditem)
        {
            Cell = cell;
            SelectedItem = selecteditem;
        }
        public Cell Cell { get; private set; }
        public object SelectedItem { get; private set; }
    }


    public static class DataProviderHelper
    {
        /// <summary>
        /// Adds or inserts a child back into its parent
        /// </summary>
        /// <param name="child"></param>
        /// <param name="index"></param>
        public static void AddToParent(this UIElement child, DependencyObject parent, int? index = null)
        {
            if (parent == null)
                return;

            if (parent is ItemsControl itemsControl)
                if (index == null)
                    itemsControl.Items.Add(child);
                else
                    itemsControl.Items.Insert(index.Value, child);
            else if (parent is Panel panel)
                if (index == null)
                    panel.Children.Add(child);
                else
                    panel.Children.Insert(index.Value, child);
            else if (parent is Decorator decorator)
                decorator.Child = child;
            else if (parent is ContentPresenter contentPresenter)
                contentPresenter.Content = child;
            else if (parent is ContentControl contentControl)
                contentControl.Content = child;
        }

        /// <summary>
        /// Removes the child from its parent collection or its content.
        /// </summary>
        /// <param name="child"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        public static bool RemoveFromParent(this UIElement child, out DependencyObject parent, out int? index)
        {
            parent = GetParent(child, true);
            if (parent == null)
                parent = GetParent(child, false);

            index = null;

            if (parent == null)
                return false;

            if (parent is ItemsControl itemsControl)
            {
                if (itemsControl.Items.Contains(child))
                {
                    index = itemsControl.Items.IndexOf(child);
                    itemsControl.Items.Remove(child);
                    return true;
                }
            }
            else if (parent is Panel panel)
            {
                if (panel.Children.Contains(child))
                {
                    index = panel.Children.IndexOf(child);
                    panel.Children.Remove(child);
                    return true;
                }
            }
            else if (parent is Decorator decorator)
            {
                if (decorator.Child == child)
                {
                    decorator.Child = null;
                    return true;
                }
            }
            else if (parent is ContentPresenter contentPresenter)
            {
                if (contentPresenter.Content == child)
                {
                    contentPresenter.Content = null;
                    return true;
                }
            }
            else if (parent is ContentControl contentControl)
            {
                if (contentControl.Content == child)
                {
                    contentControl.Content = null;
                    return true;
                }
            }

            return false;
        }

        public static DependencyObject GetParent(this DependencyObject depObj, bool isVisualTree)
        {
            if (isVisualTree)
            {
                if (depObj is System.Windows.Media.Visual || depObj is System.Windows.Media.Media3D.Visual3D)
                    return System.Windows.Media.VisualTreeHelper.GetParent(depObj);
                return null;
            }
            else
                return LogicalTreeHelper.GetParent(depObj);
        }
    }

    public class DataProvider : IDisposable
    {
        public WeakReference<ToggleButton> Trigger { get; private set; }
        public WeakReference<Popup> Selector { get; private set; }
        public WeakReference<Cell> ActiveCell { get; private set; }

        private readonly HookProc _HookProc;
        private IntPtr _hMouseHook = IntPtr.Zero;
        private bool Listen { get; set; } = false;
        private bool hooked = false;
        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [System.Runtime.InteropServices.DllImport("kernel32", SetLastError = true)]
        static extern IntPtr LoadLibrary(string lpFileName);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetWindowsHookEx(HookType hookType, HookProc lpfn, IntPtr hMod, uint dwThreadId);
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr UnhookWindowsHookEx(IntPtr hook);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        public const int WM_LBUTTONDOWN = 0x201;
        public const int WM_RBUTTONDOWN = 0x204;
        public const int WM_MBUTTONDOWN = 0x207;

        public event EventHandler<SelectorOpeningEventArgs> SelectorOpening;
        public event EventHandler<SelectorClosedEventArgs> SelectorClosed;
        public DataProvider()
        {
            var toggle = new ToggleButton();
            WeakEventManager<ToggleButton, RoutedEventArgs>.AddHandler(toggle, "Checked", new EventHandler<RoutedEventArgs>(Trigger_Checked));
            WeakEventManager<ToggleButton, RoutedEventArgs>.AddHandler(toggle, "Unchecked", new EventHandler<RoutedEventArgs>(Trigger_Unchecked));
            var selector = new Popup();
            WeakEventManager<Popup, EventArgs>.AddHandler(selector, "Opened", new EventHandler<EventArgs>(Selector_Opened));
            WeakEventManager<Popup, EventArgs>.AddHandler(selector, "Closed", new EventHandler<EventArgs>(Selector_Closed));
            WeakEventManager<Popup, RoutedEventArgs>.AddHandler(selector, "Loaded", new EventHandler<RoutedEventArgs>(Selector_Loaded));
            WeakEventManager<Popup, RoutedEventArgs>.AddHandler(selector, "Unloaded", new EventHandler<RoutedEventArgs>(Selector_Unloaded));
            selector.Child = new System.Windows.Controls.ListBox();
            SelectionChangedEventHandler = new EventHandler<SelectionChangedEventArgs>(DataProviderSelector_SelectionChanged);
            string xamlpath = "pack://application:,,,/unvell.ReoGrid;component/Data/DataProvider.Toggle.Style.xaml";
            System.Windows.Resources.StreamResourceInfo xamlinfo = System.Windows.Application.GetResourceStream(new Uri(xamlpath));
            ResourceDictionary styleresouces = System.Windows.Markup.XamlReader.Load(xamlinfo.Stream) as ResourceDictionary;
            toggle.Resources = styleresouces;
            Trigger = new WeakReference<ToggleButton>(toggle);
            Selector = new WeakReference<Popup>(selector);
            _HookProc = OnMouseHook;
        }

        private void Selector_Loaded(object sender, RoutedEventArgs e)
        {
        }

        public void OnHide()
        {
            Unhook();
            if (Selector.TryGetTarget(out var selector) && selector != null && selector.IsOpen == true)
            {
                selector.IsOpen = false;
            }
            Listen = false;
        }
        public void OnShow()
        {
        }

        public void OnDestory()
        {
            OnHide();
            if (Trigger.TryGetTarget(out ToggleButton trigger) && trigger != null)
            {
                trigger.Visibility = Visibility.Collapsed;
                trigger.RemoveFromParent(out _, out _);
            }
            if (Selector.TryGetTarget(out Popup selector) && selector != null)
            {
                selector.Visibility = Visibility.Collapsed;
                selector.RemoveFromParent(out _, out _);
            }
            Unhook();
            ActiveCell.SetTarget(null);
            Trigger.SetTarget(null);
            Selector.SetTarget(null);
            GC.Collect();
        }

        private void Hook()
        {
            if (hooked == false)
            {
                var hModule = GetModuleHandle(null);
                // 你可能会在网上搜索到下面注释掉的这种代码，但实际上已经过时了。
                //   下面代码在 .NET Core 3.x 以上可正常工作，在 .NET Framework 4.0 以下可正常工作。
                //   如果不满足此条件，你也可能可以正常工作，详情请阅读本文后续内容。
                // var hModule = Marshal.GetHINSTANCE(Assembly.GetExecutingAssembly().GetModules()[0]);
                _hMouseHook = SetWindowsHookEx(HookType.WH_MOUSE_LL, _HookProc, hModule, 0);
                if (_hMouseHook == IntPtr.Zero)
                {
                    int errorcode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                    throw new System.ComponentModel.Win32Exception(errorcode);
                }
                hooked = true;
            }
        }

        private void Unhook()
        {
            if (hooked && _hMouseHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hMouseHook);
                _hMouseHook = IntPtr.Zero;
            }
            _hMouseHook = IntPtr.Zero;
            hooked = false;
        }

        private void Selector_Unloaded(object sender, RoutedEventArgs e)
        {
            Unhook();
        }

        private IntPtr OnMouseHook(int nCode, IntPtr wParam, IntPtr lParam)
        {
            // 在这里，你可以处理全局鼠标消息。
            if (Listen)
            {
                switch (wParam.ToInt32())
                {
                    case WM_LBUTTONDOWN:
                    case WM_RBUTTONDOWN:
                    case WM_MBUTTONDOWN:
                        if (Selector.TryGetTarget(out var selector) && selector != null && selector.IsOpen == true)
                        {
                            if (!selector.IsMouseOver)
                            {
                                selector.IsOpen = false;
                                return CallNextHookEx(new IntPtr(0), nCode, wParam, lParam);
                            }
                        }
                        break;
                    default:
                        break;
                }
            }
            return CallNextHookEx(new IntPtr(0), nCode, wParam, lParam);
        }

        public System.Collections.IEnumerable ItemsSource 
        {
            get
            {
                if (Selector.TryGetTarget(out var selector) && selector != null)
                    return (selector.Child as System.Windows.Controls.ListBox).ItemsSource;
                else
                    return null;
            }
            set
            {
                if (Selector.TryGetTarget(out var selector) && selector != null)
                    (selector.Child as System.Windows.Controls.ListBox).ItemsSource = value;
            }
        }
        public object SelectedItem
        {
            get
            {
                if (Selector.TryGetTarget(out var selector) && selector != null)
                    return (selector.Child as System.Windows.Controls.ListBox).SelectedItem;
                else
                    return null;
            }
            set
            {
                if (Selector.TryGetTarget(out var selector) && selector != null)
                    (selector.Child as System.Windows.Controls.ListBox).SelectedItem = value;
            }
        }
        private EventHandler<SelectionChangedEventArgs> SelectionChangedEventHandler;
        private void Selector_Opened(object sender, EventArgs e)
        {
            if (Selector.TryGetTarget(out var selector) && selector != null)
            {
                var listbox = (selector.Child as ListBox);
                WeakEventManager<ListBox, SelectionChangedEventArgs>.AddHandler(listbox, "SelectionChanged", SelectionChangedEventHandler);
                Listen = true;
                Hook();
            }
        }

        private void DataProviderSelector_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (Selector.TryGetTarget(out var selector) && selector != null)
            {
                var listbox = (selector.Child as ListBox);
                WeakEventManager<ListBox, SelectionChangedEventArgs>.RemoveHandler(listbox, "SelectionChanged", SelectionChangedEventHandler);
                selector.IsOpen = false;
            }
        }

        private void Selector_Closed(object sender, EventArgs e)
        {
            if (Trigger.TryGetTarget(out var trigger))
            {
                trigger.IsChecked = false;
                ActiveCell.TryGetTarget(out var cell);
                if (Selector.TryGetTarget(out var selector))
                    SelectorClosed?.Invoke(this, new SelectorClosedEventArgs(cell, (selector.Child as System.Windows.Controls.ListBox).SelectedItem));
            }
            Listen = false;
            Unhook();
        }

        
        internal void Update(Rectangle rectangle, Cell cell)
        {
            if(Selector.TryGetTarget(out var selector) && selector != null)
            {
                selector.MinWidth = rectangle.Width;
                selector.Placement = PlacementMode.AbsolutePoint;
                selector.HorizontalOffset = rectangle.X;
                selector.VerticalOffset = rectangle.Y;
            }
            
            ActiveCell = new WeakReference<Cell>(cell);
        }
        private void Trigger_Unchecked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (Selector.TryGetTarget(out var selector) && selector != null)
                selector.IsOpen = false;
        }

        private void Trigger_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            ActiveCell.TryGetTarget(out var cell);
            var eventargs = new SelectorOpeningEventArgs(cell);
            SelectorOpening?.Invoke(this, eventargs);
            if (eventargs.IsCancelled) return;
            if (Selector.TryGetTarget(out var selector))
                selector.IsOpen = true;
        }

        public void Dispose()
        {
            Unhook();
        }
    }
}
