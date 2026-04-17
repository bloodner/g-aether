using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GHelper.WPF.Services;
using Button = System.Windows.Controls.Button;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using Orientation = System.Windows.Controls.Orientation;

namespace GHelper.WPF.Views
{
    public partial class ChangelogWindow : Window
    {
        public ChangelogWindow()
        {
            InitializeComponent();
            Loaded += async (_, _) => await LoadReleases();
        }

        /// <summary>
        /// Opens the full version history from the repo.
        /// </summary>
        public static void ShowHistory(Window? owner)
        {
            var win = new ChangelogWindow();
            if (owner != null) win.Owner = owner;
            win.ShowDialog();
        }

        /// <summary>
        /// Opens a one-version view for a pending update, with Update/Later actions.
        /// Returns true if user chose to update.
        /// </summary>
        public static bool ShowPendingUpdate(Window? owner, string version, string body, string releaseUrl)
        {
            var win = new ChangelogWindow();
            if (owner != null) win.Owner = owner;
            win.ConfigureForPendingUpdate(version, body, releaseUrl);
            return win.ShowDialog() == true;
        }

        private string? _pendingReleaseUrl;
        private bool _updateRequested;

        private void ConfigureForPendingUpdate(string version, string body, string releaseUrl)
        {
            _pendingReleaseUrl = releaseUrl;
            Title = "Update Available";
            LoadingText.Text = $"New release: {version}";

            // Swap the simple "Close" footer for Update / Later
            if (FindName("CloseButton") is Button closeBtn) closeBtn.Visibility = Visibility.Collapsed;
            var footerGrid = (Grid)((Border)((Grid)((Border)Content).Child).Children[3]).Child;
            footerGrid.Children.RemoveAt(1);

            var actionStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var laterBtn = new Button
            {
                Content = "Later",
                Background = new SolidColorBrush(Color.FromArgb(0x10, 0xFF, 0xFF, 0xFF)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(18, 5, 18, 5),
                FontSize = 11,
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0),
            };
            laterBtn.Click += (_, _) => { DialogResult = false; Close(); };

            var updateBtn = new Button
            {
                Content = $"Update to {version}",
                Background = new SolidColorBrush(Color.FromRgb(0x60, 0xCD, 0xFF)),
                Foreground = new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x14)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(18, 5, 18, 5),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            updateBtn.Click += (_, _) =>
            {
                _updateRequested = true;
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = _pendingReleaseUrl ?? "",
                        UseShellExecute = true
                    });
                }
                catch (System.Exception ex)
                {
                    Logger.WriteLine("Open release URL failed: " + ex.Message);
                }
                DialogResult = true;
                Close();
            };

            actionStack.Children.Add(laterBtn);
            actionStack.Children.Add(updateBtn);
            Grid.SetColumn(actionStack, 1);
            footerGrid.Children.Add(actionStack);

            // Render only this one release
            ContentPanel.Children.Clear();
            ContentPanel.Children.Add(MakeVersionHeader(version, null, false));
            ContentPanel.Children.Add(MarkdownRenderer.Render(body));
        }

        private async System.Threading.Tasks.Task LoadReleases()
        {
            if (_pendingReleaseUrl != null) return; // already configured for pending update

            var releases = await UpdateService.GetAllReleasesAsync();

            ContentPanel.Children.Clear();
            if (releases.Count == 0)
            {
                LoadingText.Text = "Couldn't load release history.";
                ContentPanel.Children.Add(new TextBlock
                {
                    Text = "Check your internet connection and try again.",
                    Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xAA)),
                    FontSize = 11,
                });
                return;
            }

            LoadingText.Text = $"{releases.Count} release{(releases.Count == 1 ? "" : "s")} total";

            for (int i = 0; i < releases.Count; i++)
            {
                var r = releases[i];
                ContentPanel.Children.Add(MakeVersionHeader(r.Version, r.PublishedAt, r.IsPrerelease));
                ContentPanel.Children.Add(MarkdownRenderer.Render(r.Body));
                if (i < releases.Count - 1)
                {
                    ContentPanel.Children.Add(new Border
                    {
                        Height = 1,
                        Background = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)),
                        Margin = new Thickness(0, 20, 0, 16),
                    });
                }
            }
        }

        private static UIElement MakeVersionHeader(string version, System.DateTime? published, bool isPrerelease)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 10) };

            var versionBox = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x20, 0x60, 0xCD, 0xFF)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x60, 0xCD, 0xFF)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 3, 8, 3),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = version,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0xCD, 0xFF)),
                },
            };
            panel.Children.Add(versionBox);

            if (isPrerelease)
            {
                var preTag = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0x6B, 0x35)),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(6, 2, 6, 2),
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = "PRE-RELEASE",
                        FontSize = 9,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x6C)),
                    },
                };
                panel.Children.Add(preTag);
            }

            if (published.HasValue)
            {
                var date = new TextBlock
                {
                    Text = published.Value.ToLocalTime().ToString("MMM d, yyyy"),
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x8A)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 0, 0),
                };
                panel.Children.Add(date);
            }

            return panel;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1) DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_updateRequested && _pendingReleaseUrl != null)
                DialogResult = false;
            Close();
        }
    }
}
