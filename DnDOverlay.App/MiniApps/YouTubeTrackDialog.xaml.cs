using System;
using System.Windows;

namespace DnDOverlay
{
    public partial class YouTubeTrackDialog : Window
    {
        private readonly bool _allowLinkEdit;
        private string? _linkValue;

        public string? SelectedLink => _linkValue;
        public string? TitleInput
        {
            get
            {
                var text = TitleTextBox.Text;
                return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
            }
        }
        public string? DescriptionInput
        {
            get
            {
                var text = DescriptionTextBox.Text;
                if (string.IsNullOrWhiteSpace(text))
                {
                    return null;
                }

                return text.Trim();
            }
        }

        public YouTubeTrackDialog(bool allowLinkEdit)
        {
            InitializeComponent();
            _allowLinkEdit = allowLinkEdit;

            if (!allowLinkEdit)
            {
                LinkRow.Height = new GridLength(0);
                LinkLabel.Visibility = Visibility.Collapsed;
                LinkTextBox.Visibility = Visibility.Collapsed;
            }
        }

        public void SetInitialValues(string? link, string? title, string? description)
        {
            if (_allowLinkEdit)
            {
                LinkTextBox.Text = link ?? string.Empty;
                _linkValue = null;
            }
            else
            {
                _linkValue = link;
            }

            TitleTextBox.Text = title ?? string.Empty;
            DescriptionTextBox.Text = description ?? string.Empty;
        }

        private void OkButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_allowLinkEdit)
            {
                var link = (LinkTextBox.Text ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(link))
                {
                    MessageBox.Show(this,
                        "Введите ссылку на YouTube",
                        "YouTube трек",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    LinkTextBox.Focus();
                    LinkTextBox.SelectAll();
                    return;
                }

                _linkValue = link;
            }

            DialogResult = true;
        }

        private void CancelButton_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
