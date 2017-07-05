﻿using System.Diagnostics;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using POESKillTree.Utils.Converter;

namespace POESKillTree.Utils.Wpf
{
    /// <summary>
    /// Contains attached properties usable on Xaml classes.
    /// </summary>
    public static class Helper
    {
        public static readonly DependencyProperty NavigateExternallyProperty =
            DependencyProperty.RegisterAttached(
                "NavigateExternally", typeof(bool), typeof(Helper),
                new FrameworkPropertyMetadata(false, NavigateExternallyPropertyChangedCallback));

        public static readonly DependencyProperty MainWindowRelativeMaxHeightProperty =
            DependencyProperty.RegisterAttached(
                "MainWindowRelativeMaxHeight", typeof(double), typeof(Helper),
                new FrameworkPropertyMetadata(0.0, MainWindowRelativeMaxHeightPropertyChangedCallback));

        /// <summary>
        /// Gets whether the target <see cref="Hyperlink"/>'s RequestNavigation event should open the URI in the
        /// default web browser.
        /// </summary>
        [AttachedPropertyBrowsableForType(typeof(Hyperlink))]
        public static bool GetNavigateExternally(Hyperlink element)
        {
            return (bool) element.GetValue(NavigateExternallyProperty);
        }

        /// <summary>
        /// Sets whether the target <see cref="Hyperlink"/>'s RequestNavigation event should open the URI in the
        /// default web browser.
        /// </summary>
        public static void SetNavigateExternally(Hyperlink element, bool value)
        {
            element.SetValue(NavigateExternallyProperty, value);
        }

        private static void NavigateExternallyPropertyChangedCallback(DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs e)
        {
            var hyperlink = dependencyObject as Hyperlink;
            if (hyperlink == null)
                return;

            hyperlink.RequestNavigate += (sender, args) => Process.Start(args.Uri.ToString());
        }

        /// <summary>
        /// Gets the value that is subtracted from the main window's actual height which is then used
        /// as maximum height for the framework element. 0 or negative behaves as if this property
        /// was not set (no relative max height).
        /// </summary>
        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))]
        public static double GetMainWindowRelativeMaxHeight(FrameworkElement element)
        {
            return (double) element.GetValue(MainWindowRelativeMaxHeightProperty);
        }

        /// <summary>
        /// Sets the value that is subtracted from the main window's actual height which is then used
        /// as maximum height for the framework element. 0 or negative behaves as if this property
        /// was not set (no relative max height).
        /// </summary>
        public static void SetMainWindowRelativeMaxHeight(FrameworkElement element, double value)
        {
            element.SetValue(MainWindowRelativeMaxHeightProperty, value);
        }

        private static void MainWindowRelativeMaxHeightPropertyChangedCallback(DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs e)
        {
            var element = dependencyObject as FrameworkElement;
            if (element == null)
                return;

            var value = (double) e.NewValue;
            if (value <= 0)
            {
                BindingOperations.ClearBinding(element, FrameworkElement.MaxHeightProperty);
                return;
            }

            var binding = new Binding("ActualHeight")
            {
                Source = Application.Current.MainWindow,
                Converter = new SumConverter {Minimum = 0},
                ConverterParameter = -value
            };
            element.SetBinding(FrameworkElement.MaxHeightProperty, binding);
        }
    }
}