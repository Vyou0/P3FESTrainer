using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace P3FESTrainer.Controls
{
    /// <summary>
    /// Searchable combo box control with search-as-you-type popup filtering.
    /// </summary>
    public partial class SearchableComboBox : UserControl
    {
        private List<string> _allOptions = new();
        private bool _suppressTextChanged;

        public static readonly DependencyProperty ReadOnlyProperty =
            DependencyProperty.Register(nameof(ReadOnly), typeof(bool), typeof(SearchableComboBox), new PropertyMetadata(false));

        public bool ReadOnly
        {
            get => (bool)GetValue(ReadOnlyProperty);
            set => SetValue(ReadOnlyProperty, value);
        }

        public bool OpenAbove { get; set; } = false;

        public event EventHandler? SelectionCommitted;

        public SearchableComboBox()
        {
            InitializeComponent();
        }

        public void SetOptions(IEnumerable<string> options)
        {
            _allOptions = options.ToList();
        }

        public string Text
        {
            get => EditBox.Text;
            set
            {
                _suppressTextChanged = true;
                EditBox.Text = value;
                _suppressTextChanged = false;
            }
        }

        public string Get() => EditBox.Text;

        public void Set(string value) => Text = value;

        private void EditBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextChanged) return;
            string q = EditBox.Text.Trim().ToLowerInvariant();
            var matches = string.IsNullOrEmpty(q)
                ? _allOptions
                : _allOptions.Where(o => o.ToLowerInvariant().Contains(q)).ToList();
            if (matches.Count == 0)
            {
                ResultsPopup.IsOpen = false;
                return;
            }
            ResultsList.ItemsSource = matches.Take(200).ToList();
            ResultsPopup.Placement = OpenAbove ? PlacementMode.Top : PlacementMode.Bottom;
            ResultsPopup.IsOpen = true;
        }

        private void EditBox_GotFocus(object sender, RoutedEventArgs e)
        {
            ShowFullList();
        }

        private void DropdownButton_Click(object sender, RoutedEventArgs e)
        {
            if (ResultsPopup.IsOpen)
            {
                ResultsPopup.IsOpen = false;
            }
            else
            {
                EditBox.Focus();
                ShowFullList();
            }
        }

        private void ShowFullList()
        {
            ResultsList.ItemsSource = _allOptions;
            ResultsPopup.Placement = OpenAbove ? PlacementMode.Top : PlacementMode.Bottom;
            ResultsPopup.IsOpen = _allOptions.Count > 0;
        }

        private void EditBox_LostFocus(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!ResultsList.IsKeyboardFocusWithin)
                    ResultsPopup.IsOpen = false;
            }));
        }

        private void EditBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down && ResultsPopup.IsOpen && ResultsList.Items.Count > 0)
            {
                ResultsList.Focus();
                ResultsList.SelectedIndex = 0;
                (ResultsList.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem)?.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                ResultsPopup.IsOpen = false;
            }
        }

        private void ResultsList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && ResultsList.SelectedItem is string s)
            {
                Commit(s);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                ResultsPopup.IsOpen = false;
                EditBox.Focus();
            }
        }

        private void ResultsList_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject dep)
            {
                var item = FindAncestor<ListBoxItem>(dep);
                if (item != null && item.DataContext is string s)
                {
                    Commit(s);
                }
            }
        }

        private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T ancestor) return ancestor;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private void Commit(string value)
        {
            Text = value;
            ResultsPopup.IsOpen = false;
            EditBox.Focus();
            EditBox.CaretIndex = EditBox.Text.Length;
            SelectionCommitted?.Invoke(this, EventArgs.Empty);
        }
    }
}
