using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace DnDOverlay.Infrastructure
{
    public static class TopmostToggleBinder
    {
        public static void Bind(Window window, ToggleButton toggleButton)
        {
            if (window == null) throw new ArgumentNullException(nameof(window));
            if (toggleButton == null) throw new ArgumentNullException(nameof(toggleButton));

            void UpdateToggle()
            {
                toggleButton.IsChecked = window.Topmost;
            }

            UpdateToggle();

            var descriptor = DependencyPropertyDescriptor.FromProperty(Window.TopmostProperty, typeof(Window));
            if (descriptor == null)
            {
                return;
            }

            EventHandler? handler = null;
            handler = (_, _) => UpdateToggle();

            descriptor.AddValueChanged(window, handler);

            window.Closed += (_, _) => descriptor.RemoveValueChanged(window, handler);
        }
    }
}
