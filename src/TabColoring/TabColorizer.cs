using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace SSMSTabManager
{
    internal static class TabColorizer
    {
        private const string DocumentTabTypeName = "DocumentTabItem";
        private const double StripeHeight = 5;

        public static bool Apply(IVsUIShell shell, string moniker, Color? color)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var view = FindFrameView(shell, moniker);
            if (view == null)
            {
                return false;
            }

            var border = Application.Current.Windows.Cast<Window>()
                .Select(window => FindTab(window, view))
                .Where(tab => tab != null)
                .Select(FindDescendant<Border>)
                .FirstOrDefault(found => found != null);
            if (border == null)
            {
                return false;
            }

            if (color.HasValue)
            {
                var brush = new SolidColorBrush(color.Value);
                border.Background = brush;
                border.BorderBrush = brush;
                border.BorderThickness = new Thickness(0, 0, 0, StripeHeight);
            }
            else
            {
                border.ClearValue(Border.BackgroundProperty);
                border.ClearValue(Border.BorderBrushProperty);
                border.ClearValue(Border.BorderThicknessProperty);
            }
            return true;
        }

        private static object FindFrameView(IVsUIShell shell, string moniker)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            ErrorHandler.ThrowOnFailure(shell.GetDocumentWindowEnum(out var frames));
            var frame = new IVsWindowFrame[1];
            while (frames.Next(1, frame, out var fetched) == VSConstants.S_OK && fetched == 1)
            {
                frame[0].GetProperty((int)__VSFPROPID.VSFPROPID_pszMkDocument, out var frameMoniker);
                if (string.Equals(frameMoniker as string, moniker, StringComparison.OrdinalIgnoreCase))
                {
                    return frame[0].GetType().GetProperty("FrameView")?.GetValue(frame[0]);
                }
            }
            return null;
        }

        private static FrameworkElement FindTab(DependencyObject root, object view)
        {
            var element = root as FrameworkElement;
            if (element != null && element.GetType().Name == DocumentTabTypeName)
            {
                return ReferenceEquals(element.DataContext, view) ? element : null;
            }

            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var tab = FindTab(VisualTreeHelper.GetChild(root, i), view);
                if (tab != null)
                {
                    return tab;
                }
            }
            return null;
        }

        private static T FindDescendant<T>(DependencyObject root) where T : DependencyObject
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                var found = child as T ?? FindDescendant<T>(child);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }
    }
}
