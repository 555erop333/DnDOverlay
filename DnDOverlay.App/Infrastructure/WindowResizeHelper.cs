using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;

namespace DnDOverlay.Infrastructure
{
    public class WindowResizeHelper
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [StructLayout(LayoutKind.Sequential)]
        struct POINT
        {
            public int X;
            public int Y;
        }

        private void ClampWindowPos(IntPtr lParam)
        {
            if (_isResizing)
            {
                return;
            }

            var pos = Marshal.PtrToStructure<WINDOWPOS>(lParam);
            if (pos.Equals(default(WINDOWPOS)))
            {
                return;
            }

            if ((pos.flags & SWP_NOSIZE) != 0)
            {
                return;
            }

            var width = GetLockedDimensionInt(_lockedWidth, _window.Width, _window.ActualWidth, _window.MinWidth);
            var height = GetLockedDimensionInt(_lockedHeight, _window.Height, _window.ActualHeight, _window.MinHeight);

            pos.cx = width;
            pos.cy = height;
            pos.flags |= SWP_NOSIZE;

            Marshal.StructureToPtr(pos, lParam, fDeleteOld: false);
        }

        private void RestoreLockedSize()
        {
            if (!_disableSystemResize || _isResizing || _isRestoringSize)
            {
                return;
            }

            var targetWidth = _lockedWidth;
            var targetHeight = _lockedHeight;

            if (targetWidth <= 0 || targetHeight <= 0)
            {
                return;
            }

            void Apply()
            {
                if (_isResizing)
                {
                    return;
                }

                var widthDelta = Math.Abs(_window.ActualWidth - targetWidth);
                var heightDelta = Math.Abs(_window.ActualHeight - targetHeight);

                if (widthDelta < 0.5 && heightDelta < 0.5)
                {
                    return;
                }

                try
                {
                    _isRestoringSize = true;
                    _window.Width = targetWidth;
                    _window.Height = targetHeight;
                }
                finally
                {
                    _isRestoringSize = false;
                }
            }

            if (_window.CheckAccess())
            {
                Apply();
            }
            else
            {
                _window.Dispatcher.BeginInvoke((Action)Apply, DispatcherPriority.Send);
            }
        }

        private void UpdateLockedSize(bool force = false)
        {
            if (!_disableSystemResize)
            {
                return;
            }

            var width = GetStableDimension(_window.Width, _window.ActualWidth, _window.MinWidth);
            var height = GetStableDimension(_window.Height, _window.ActualHeight, _window.MinHeight);

            if (!force && Math.Abs(width - _lockedWidth) < 0.5 && Math.Abs(height - _lockedHeight) < 0.5)
            {
                return;
            }

            _lockedWidth = width;
            _lockedHeight = height;
        }

        private static double GetStableDimension(double target, double fallbackActual, double fallbackMin)
        {
            if (double.IsNaN(target) || target <= 0)
            {
                if (!double.IsNaN(fallbackActual) && fallbackActual > 0)
                {
                    target = fallbackActual;
                }
                else if (fallbackMin > 0)
                {
                    target = fallbackMin;
                }
                else
                {
                    target = 1;
                }
            }

            return target;
        }

        private static int GetCurrentDimension(double target, double fallbackActual, double fallbackMin)
        {
            var dimension = GetStableDimension(target, fallbackActual, fallbackMin);
            return (int)Math.Max(1, Math.Round(dimension));
        }

        private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (_isResizing)
            {
                UpdateLockedSize(force: true);
                return;
            }

            if (_isRestoringSize)
            {
                return;
            }

            if (_disableSystemResize)
            {
                RestoreLockedSize();
            }
        }
        
        private readonly Window _window;
        private readonly bool _disableSystemResize;
        private double _lockedWidth;
        private double _lockedHeight;
        private bool _isRestoringSize;
        private ResizeDirection _resizeDirection;
        private Point _startMousePosition;
        private double _startWidth;
        private double _startHeight;
        private double _startLeft;
        private double _startTop;
        private bool _isResizing;

        private const int WM_GETMINMAXINFO = 0x0024;
        private const int WM_WINDOWPOSCHANGING = 0x0046;
        private const int WM_SYSCOMMAND = 0x0112;
        private const int WM_ENTERSIZEMOVE = 0x0231;
        private const int WM_EXITSIZEMOVE = 0x0232;
        private const int SWP_NOSIZE = 0x0001;
        private const int SWP_NOMOVE = 0x0002;
        private const int SWP_NOZORDER = 0x0004;
        private const int SWP_NOACTIVATE = 0x0010;
        private const int SWP_FRAMECHANGED = 0x0020;
        private const int SC_SIZE = 0xF000;
        private const int SC_MAXIMIZE = 0xF030;
        private const int GWL_STYLE = -16;
        private const int WS_MAXIMIZEBOX = 0x00010000;
        private const int WS_THICKFRAME = 0x00040000;

        private enum ResizeDirection
        {
            None,
            TopLeft, Top, TopRight,
            Left, Right,
            BottomLeft, Bottom, BottomRight
        }

        public WindowResizeHelper(Window window, bool disableSystemResize = false)
        {
            _window = window;
            _disableSystemResize = disableSystemResize;
            _window.ResizeMode = _disableSystemResize ? ResizeMode.NoResize : ResizeMode.CanResize;

            if (_disableSystemResize)
            {
                _window.SourceInitialized += OnSourceInitialized;
                _window.SizeChanged += OnWindowSizeChanged;
            }
            
            AddResizeHandlersToWindow();
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            if (!_disableSystemResize)
            {
                return;
            }

            if (PresentationSource.FromVisual(_window) is HwndSource hwndSource)
            {
                hwndSource.AddHook(WndProc);
                RemoveSystemResizeStyles(hwndSource.Handle);
            }
        }

        private void RemoveSystemResizeStyles(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            var style = GetWindowLong(hwnd, GWL_STYLE);
            if (style == 0)
            {
                return;
            }

            var newStyle = style & ~WS_MAXIMIZEBOX & ~WS_THICKFRAME;
            if (newStyle == style)
            {
                return;
            }

            SetWindowLong(hwnd, GWL_STYLE, newStyle);
            const uint flags = SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED;
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, flags);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (!_disableSystemResize)
            {
                return IntPtr.Zero;
            }

            switch (msg)
            {
                case WM_GETMINMAXINFO:
                    UpdateMinMaxInfo(lParam);
                    handled = true;
                    break;
                case WM_WINDOWPOSCHANGING:
                    ClampWindowPos(lParam);
                    break;
                case WM_SYSCOMMAND:
                    if (IsRestrictedSysCommand(wParam))
                    {
                        handled = true;
                        return IntPtr.Zero;
                    }
                    break;
                case WM_EXITSIZEMOVE:
                    if (!_isResizing)
                    {
                        RestoreLockedSize();
                    }
                    break;
            }

            return IntPtr.Zero;
        }

        private static bool IsRestrictedSysCommand(IntPtr wParam)
        {
            var command = (int)(wParam.ToInt64() & 0xFFF0);
            return command == SC_SIZE || command == SC_MAXIMIZE;
        }

        private void UpdateMinMaxInfo(IntPtr lParam)
        {
            var info = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            if (info.Equals(default(MINMAXINFO)))
            {
                return;
            }

            var width = GetCurrentDimension(_window.Width, _window.ActualWidth, _window.MinWidth);
            var height = GetCurrentDimension(_window.Height, _window.ActualHeight, _window.MinHeight);

            info.ptMinTrackSize.X = info.ptMaxTrackSize.X = width;
            info.ptMinTrackSize.Y = info.ptMaxTrackSize.Y = height;
            info.ptMaxSize.X = width;
            info.ptMaxSize.Y = height;

            Marshal.StructureToPtr(info, lParam, fDeleteOld: false);
        }

        private static int GetLockedDimensionInt(double locked, double target, double fallbackActual, double fallbackMin)
        {
            var dimension = locked > 0 ? locked : GetStableDimension(target, fallbackActual, fallbackMin);
            return (int)Math.Max(1, Math.Round(dimension));
        }

        private void AddResizeHandlersToWindow()
        {
            // Ждем загрузки окна чтобы получить доступ к контенту
            _window.Loaded += OnWindowLoaded;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            // Оборачиваем существующий контент в Grid с зонами для изменения размера
            WrapContentWithResizeGrid();
            UpdateLockedSize(force: true);
        }

        private void WrapContentWithResizeGrid()
        {
            var originalContent = _window.Content as UIElement;
            if (originalContent == null) return;

            // Создаем контейнер Grid
            var containerGrid = new Grid();

            // Перемещаем оригинальный контент
            _window.Content = null;
            
            // Добавляем контент БЕЗ отступа - зоны будут внутри окна
            containerGrid.Children.Add(originalContent);
            
            // Потом добавляем зоны для изменения размера ПОВЕРХ контента
            CreateResizeHandles(containerGrid);
            
            _window.Content = containerGrid;
        }

        private void CreateResizeHandles(Grid container)
        {
            // Толщина зон захвата
            const int edgeSize = 5;
            const int cornerSize = 7;
            
            // Зоны будут полностью прозрачными, но курсор будет меняться при наведении
            var transparentBrush = Brushes.Transparent;

            // Углы - они имеют приоритет и должны быть добавлены последними
            
            // Края (должны быть добавлены первыми)
            // Верхний край
            var topEdge = new Rectangle
            {
                Height = edgeSize,
                Fill = transparentBrush,
                Cursor = Cursors.SizeNS,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(cornerSize, 2, cornerSize, 0), // 2px отступ сверху
                Tag = ResizeDirection.Top,
                IsHitTestVisible = true
            };
            Panel.SetZIndex(topEdge, 1000);
            container.Children.Add(topEdge);
            AttachHandlers(topEdge);

            // Нижний край
            var bottomEdge = new Rectangle
            {
                Height = edgeSize,
                Fill = transparentBrush,
                Cursor = Cursors.SizeNS,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(cornerSize, 0, cornerSize, 2), // 2px отступ снизу
                Tag = ResizeDirection.Bottom,
                IsHitTestVisible = true
            };
            Panel.SetZIndex(bottomEdge, 1000);
            container.Children.Add(bottomEdge);
            AttachHandlers(bottomEdge);

            // Левый край
            var leftEdge = new Rectangle
            {
                Width = edgeSize,
                Fill = transparentBrush,
                Cursor = Cursors.SizeWE,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(2, cornerSize, 0, cornerSize), // 2px отступ слева
                Tag = ResizeDirection.Left,
                IsHitTestVisible = true
            };
            Panel.SetZIndex(leftEdge, 1000);
            container.Children.Add(leftEdge);
            AttachHandlers(leftEdge);

            // Правый край
            var rightEdge = new Rectangle
            {
                Width = edgeSize,
                Fill = transparentBrush,
                Cursor = Cursors.SizeWE,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, cornerSize, 2, cornerSize), // 2px отступ справа
                Tag = ResizeDirection.Right,
                IsHitTestVisible = true
            };
            Panel.SetZIndex(rightEdge, 1000);
            container.Children.Add(rightEdge);
            AttachHandlers(rightEdge);

            // Углы (добавляем последними чтобы они были поверх краев)
            // Верхний левый
            var topLeft = new Rectangle
            {
                Width = cornerSize,
                Height = cornerSize,
                Fill = transparentBrush,
                Cursor = Cursors.SizeNWSE,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(2, 2, 0, 0), // 2px отступ от края
                Tag = ResizeDirection.TopLeft,
                IsHitTestVisible = true
            };
            Panel.SetZIndex(topLeft, 1001);
            container.Children.Add(topLeft);
            AttachHandlers(topLeft);

            // Верхний правый
            var topRight = new Rectangle
            {
                Width = cornerSize,
                Height = cornerSize,
                Fill = transparentBrush,
                Cursor = Cursors.SizeNESW,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 2, 0), // 2px отступ от края
                Tag = ResizeDirection.TopRight,
                IsHitTestVisible = true
            };
            Panel.SetZIndex(topRight, 1001);
            container.Children.Add(topRight);
            AttachHandlers(topRight);

            // Нижний левый
            var bottomLeft = new Rectangle
            {
                Width = cornerSize,
                Height = cornerSize,
                Fill = transparentBrush,
                Cursor = Cursors.SizeNESW,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(2, 0, 0, 2), // 2px отступ от края
                Tag = ResizeDirection.BottomLeft,
                IsHitTestVisible = true
            };
            Panel.SetZIndex(bottomLeft, 1001);
            container.Children.Add(bottomLeft);
            AttachHandlers(bottomLeft);

            // Нижний правый
            var bottomRight = new Rectangle
            {
                Width = cornerSize,
                Height = cornerSize,
                Fill = transparentBrush,
                Cursor = Cursors.SizeNWSE,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 2, 2), // 2px отступ от края
                Tag = ResizeDirection.BottomRight,
                IsHitTestVisible = true
            };
            Panel.SetZIndex(bottomRight, 1001);
            container.Children.Add(bottomRight);
            AttachHandlers(bottomRight);
        }

        private void AttachHandlers(Rectangle rect)
        {
            rect.MouseLeftButtonDown += OnResizeStart;
            rect.MouseMove += OnResizeMove;
            rect.MouseLeftButtonUp += OnResizeEnd;
            rect.LostMouseCapture += OnLostMouseCapture;
        }

        private void OnResizeStart(object sender, MouseButtonEventArgs e)
        {
            var rect = sender as Rectangle;
            if (rect == null) return;

            _resizeDirection = (ResizeDirection)rect.Tag;
            
            // Получаем абсолютные экранные координаты мыши через Win32 API
            _startMousePosition = GetMousePosition();
            _startWidth = _window.ActualWidth;
            _startHeight = _window.ActualHeight;
            _startLeft = _window.Left;
            _startTop = _window.Top;
            _isResizing = true;

            rect.CaptureMouse();
            e.Handled = true;
        }
        
        private Point GetMousePosition()
        {
            POINT pt;
            GetCursorPos(out pt);
            return new Point(pt.X, pt.Y);
        }

        private void OnResizeMove(object sender, MouseEventArgs e)
        {
            if (!_isResizing || _resizeDirection == ResizeDirection.None) return;

            // Получаем текущие абсолютные экранные координаты мыши
            var currentMousePos = GetMousePosition();
            var deltaX = currentMousePos.X - _startMousePosition.X;
            var deltaY = currentMousePos.Y - _startMousePosition.Y;

            switch (_resizeDirection)
            {
                case ResizeDirection.TopLeft:
                    ResizeWindow(deltaX, deltaY, true, true, true, true);
                    break;
                case ResizeDirection.Top:
                    ResizeWindow(0, deltaY, false, true, false, true);
                    break;
                case ResizeDirection.TopRight:
                    ResizeWindow(deltaX, deltaY, false, true, true, true); // Исправлено: changeHeight = true
                    break;
                case ResizeDirection.Left:
                    ResizeWindow(deltaX, 0, true, false, true, false);
                    break;
                case ResizeDirection.Right:
                    ResizeWindow(deltaX, 0, false, false, true, false);
                    break;
                case ResizeDirection.BottomLeft:
                    ResizeWindow(deltaX, deltaY, true, false, true, true); // Исправлено: changeWidth = true
                    break;
                case ResizeDirection.Bottom:
                    ResizeWindow(0, deltaY, false, false, false, true);
                    break;
                case ResizeDirection.BottomRight:
                    ResizeWindow(deltaX, deltaY, false, false, true, true);
                    break;
            }
        }

        private void ResizeWindow(double deltaX, double deltaY, bool changeLeft, bool changeTop, 
            bool changeWidth, bool changeHeight)
        {
            var newWidth = _startWidth;
            var newHeight = _startHeight;
            var newLeft = _startLeft;
            var newTop = _startTop;

            if (changeWidth)
            {
                if (changeLeft)
                {
                    // При движении левой грани влево deltaX < 0, ширина должна увеличиться
                    newWidth -= deltaX;
                    newLeft += deltaX;
                }
                else
                {
                    newWidth += deltaX;
                }
            }

            if (changeHeight)
            {
                if (changeTop)
                {
                    // При движении верхней грани вверх deltaY < 0, высота должна увеличиться
                    newHeight -= deltaY;
                    newTop += deltaY;
                }
                else
                {
                    newHeight += deltaY;
                }
            }

            // Применяем минимальные размеры
            var minWidth = _window.MinWidth > 0 ? _window.MinWidth : 150;
            var minHeight = _window.MinHeight > 0 ? _window.MinHeight : 100;

            if (newWidth < minWidth)
            {
                if (changeLeft)
                    newLeft = _startLeft + _startWidth - minWidth;
                newWidth = minWidth;
            }

            if (newHeight < minHeight)
            {
                if (changeTop)
                    newTop = _startTop + _startHeight - minHeight;
                newHeight = minHeight;
            }

            // Применяем максимальные размеры, если они установлены
            if (_window.MaxWidth > 0 && newWidth > _window.MaxWidth)
            {
                if (changeLeft)
                    newLeft = _startLeft + _startWidth - _window.MaxWidth;
                newWidth = _window.MaxWidth;
            }

            if (_window.MaxHeight > 0 && newHeight > _window.MaxHeight)
            {
                if (changeTop)
                    newTop = _startTop + _startHeight - _window.MaxHeight;
                newHeight = _window.MaxHeight;
            }

            _window.Width = newWidth;
            _window.Height = newHeight;
            if (changeLeft)
                _window.Left = newLeft;
            if (changeTop)
                _window.Top = newTop;
        }

        private void OnResizeEnd(object sender, MouseButtonEventArgs e)
        {
            if (_isResizing)
            {
                _isResizing = false;
                _resizeDirection = ResizeDirection.None;
                
                var rect = sender as Rectangle;
                rect?.ReleaseMouseCapture();
                UpdateLockedSize(force: true);
            }
        }

        private void OnLostMouseCapture(object sender, MouseEventArgs e)
        {
            _isResizing = false;
            _resizeDirection = ResizeDirection.None;
        }

        public static void EnableResize(Window window, bool disableSystemResize = false)
        {
            new WindowResizeHelper(window, disableSystemResize);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPOS
        {
            public IntPtr hwnd;
            public IntPtr hwndInsertAfter;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public int flags;
        }
    }
}
