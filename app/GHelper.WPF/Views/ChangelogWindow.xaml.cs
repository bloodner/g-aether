using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GHelper.WPF.Services;
using Button = System.Windows.Controls.Button;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using ProgressBar = System.Windows.Controls.ProgressBar;
using Panel = System.Windows.Controls.Panel;

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
        /// Opens a one-version view for a pending update with Install / Later / Skip
        /// actions. Runs the download + install-over handoff when the user chooses Install.
        /// </summary>
        public static UpdateDialogOutcome ShowPendingUpdate(Window? owner, UpdateCheckResult update)
        {
            var win = new ChangelogWindow();
            if (owner != null) win.Owner = owner;
            win.ConfigureForPendingUpdate(update);
            win.ShowDialog();
            return win._outcome;
        }

        // -- Pending-update state -------------------------------------------------

        private UpdateCheckResult? _pendingUpdate;
        private UpdateDialogOutcome _outcome = UpdateDialogOutcome.Later;
        private CancellationTokenSource? _downloadCts;
        private string? _downloadedExePath;
        private ProgressBar? _progressBar;
        private TextBlock? _progressLabel;
        private TextBlock? _progressStatus;
        private Panel? _pendingFooter;

        private void ConfigureForPendingUpdate(UpdateCheckResult update)
        {
            _pendingUpdate = update;
            Title = "Update Available";
            LoadingText.Text = $"New release: {update.LatestVersion}";

            // Build the release-notes state as the initial view.
            ContentPanel.Children.Clear();
            ContentPanel.Children.Add(MakeVersionHeader(update.LatestVersion ?? "", null, false));
            ContentPanel.Children.Add(MarkdownRenderer.Render(update.ReleaseBody ?? "No release notes available."));

            // Replace the default "Close" button with Skip / Later / Install.
            if (FindName("CloseButton") is Button closeBtn) closeBtn.Visibility = Visibility.Collapsed;
            SetFooterReleaseNotes();
        }

        private void SetFooterReleaseNotes()
        {
            var footer = GetFooterGrid();
            ReplaceFooterActionColumn(footer, BuildReleaseNotesActions());
            _pendingFooter = footer;
        }

        private StackPanel BuildReleaseNotesActions()
        {
            var stack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            stack.Children.Add(MakeLinkButton("Skip this version", (_, _) =>
            {
                _outcome = UpdateDialogOutcome.Skipped;
                DialogResult = false;
                Close();
            }));

            stack.Children.Add(MakeSecondaryButton("Later", (_, _) =>
            {
                _outcome = UpdateDialogOutcome.Later;
                DialogResult = false;
                Close();
            }));

            stack.Children.Add(MakePrimaryButton(
                $"Install {_pendingUpdate?.LatestVersion}",
                async (_, _) => await StartInstall()));

            return stack;
        }

        private async Task StartInstall()
        {
            if (_pendingUpdate == null) return;
            if (string.IsNullOrEmpty(_pendingUpdate.DownloadUrl))
            {
                // No asset attached to the release — fall back to opening the release page.
                OpenReleasePageAndClose();
                return;
            }

            _outcome = UpdateDialogOutcome.Installing;
            SwapToDownloadingView();

            string tempDir = Path.Combine(Path.GetTempPath(), "G-Aether-update");
            string destPath = Path.Combine(tempDir, "G-Aether.exe");

            _downloadCts = new CancellationTokenSource();
            var progress = new Progress<double>(pct =>
            {
                if (_progressBar != null) _progressBar.Value = pct * 100.0;
                if (_progressLabel != null) _progressLabel.Text = $"{(int)(pct * 100)}%";
            });

            var result = await UpdateService.DownloadAsync(
                _pendingUpdate.DownloadUrl,
                destPath,
                _pendingUpdate.DownloadSize,
                progress,
                _downloadCts.Token);

            if (!result.Success)
            {
                SwapToDownloadFailedView(result.ErrorMessage ?? "Unknown error");
                return;
            }

            _downloadedExePath = result.FilePath;
            SwapToReadyToInstallView();
        }

        private void SwapToDownloadingView()
        {
            Title = "Downloading Update";
            ContentPanel.Children.Clear();

            var panel = new StackPanel { Margin = new Thickness(0, 24, 0, 16) };

            var heading = new TextBlock
            {
                Text = $"Downloading G-Aether {_pendingUpdate?.LatestVersion}",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xEC)),
                Margin = new Thickness(0, 0, 0, 14),
            };
            panel.Children.Add(heading);

            _progressBar = new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Height = 6,
                Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0xCD, 0xFF)),
                Background = new SolidColorBrush(Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(0),
            };
            panel.Children.Add(_progressBar);

            var labelRow = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            labelRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            labelRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _progressStatus = new TextBlock
            {
                Text = "Fetching latest build…",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0x90, 0x90, 0x9A)),
            };
            _progressLabel = new TextBlock
            {
                Text = "0%",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0x90, 0x90, 0x9A)),
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            Grid.SetColumn(_progressStatus, 0);
            Grid.SetColumn(_progressLabel, 1);
            labelRow.Children.Add(_progressStatus);
            labelRow.Children.Add(_progressLabel);
            panel.Children.Add(labelRow);

            ContentPanel.Children.Add(panel);
            LoadingText.Text = "";

            var footer = GetFooterGrid();
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            actions.Children.Add(MakeSecondaryButton("Cancel", (_, _) =>
            {
                _downloadCts?.Cancel();
                _outcome = UpdateDialogOutcome.Later;
                DialogResult = false;
                Close();
            }));
            ReplaceFooterActionColumn(footer, actions);
        }

        private void SwapToReadyToInstallView()
        {
            Title = "Ready to Install";
            ContentPanel.Children.Clear();

            var panel = new StackPanel { Margin = new Thickness(0, 32, 0, 16), HorizontalAlignment = HorizontalAlignment.Center };
            panel.Children.Add(new TextBlock
            {
                Text = "\uE930",  // check-circle glyph
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 36,
                Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xC9, 0x5E)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10),
            });
            panel.Children.Add(new TextBlock
            {
                Text = $"G-Aether {_pendingUpdate?.LatestVersion} is ready to install",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xEC)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6),
            });
            panel.Children.Add(new TextBlock
            {
                Text = "G-Aether will close and relaunch automatically.",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xAA)),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
            });

            ContentPanel.Children.Add(panel);
            LoadingText.Text = "";

            var footer = GetFooterGrid();
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            actions.Children.Add(MakeSecondaryButton("Install Later", (_, _) =>
            {
                // Keep the downloaded exe around — the user can click Install again
                // and we could skip re-download. For now, just dismiss.
                _outcome = UpdateDialogOutcome.Later;
                DialogResult = false;
                Close();
            }));
            actions.Children.Add(MakePrimaryButton("Restart Now", (_, _) =>
            {
                if (string.IsNullOrEmpty(_downloadedExePath))
                {
                    OpenReleasePageAndClose();
                    return;
                }

                string targetPath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (string.IsNullOrEmpty(targetPath) || !UpdaterService.LaunchInstaller(_downloadedExePath, targetPath))
                {
                    OpenReleasePageAndClose();
                    return;
                }

                _outcome = UpdateDialogOutcome.Installing;
                DialogResult = true;
                Close();
                // Shut the whole app down so the installer can take over.
                System.Windows.Application.Current?.Shutdown();
            }));
            ReplaceFooterActionColumn(footer, actions);
        }

        private void SwapToDownloadFailedView(string message)
        {
            Title = "Download Failed";
            ContentPanel.Children.Clear();

            var panel = new StackPanel { Margin = new Thickness(0, 20, 0, 10) };
            panel.Children.Add(new TextBlock
            {
                Text = "Couldn't download the update",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xEC)),
                Margin = new Thickness(0, 0, 0, 8),
            });
            panel.Children.Add(new TextBlock
            {
                Text = message,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xAA)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12),
            });
            panel.Children.Add(new TextBlock
            {
                Text = "You can open the release page to download manually instead.",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xAA)),
                TextWrapping = TextWrapping.Wrap,
            });

            ContentPanel.Children.Add(panel);
            LoadingText.Text = "";

            var footer = GetFooterGrid();
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            actions.Children.Add(MakeSecondaryButton("Close", (_, _) =>
            {
                _outcome = UpdateDialogOutcome.Later;
                DialogResult = false;
                Close();
            }));
            actions.Children.Add(MakePrimaryButton("Open Release Page", (_, _) => OpenReleasePageAndClose()));
            ReplaceFooterActionColumn(footer, actions);
        }

        private void OpenReleasePageAndClose()
        {
            try
            {
                if (!string.IsNullOrEmpty(_pendingUpdate?.ReleaseUrl))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = _pendingUpdate.ReleaseUrl,
                        UseShellExecute = true,
                    });
                }
            }
            catch (System.Exception ex) { Logger.WriteLine("Open release URL failed: " + ex.Message); }

            _outcome = UpdateDialogOutcome.Later;
            DialogResult = false;
            Close();
        }

        private Grid GetFooterGrid()
        {
            // Content → Border(root) → Grid(rows) → Border(Row=3) → Grid(footer cells)
            var root = (Border)Content;
            var rows = (Grid)root.Child;
            var footerBorder = (Border)rows.Children[3];
            return (Grid)footerBorder.Child;
        }

        private static void ReplaceFooterActionColumn(Grid footer, UIElement newActions)
        {
            // Column 0 is LoadingText, Column 1 is the action area. Wipe column 1 only.
            for (int i = footer.Children.Count - 1; i >= 0; i--)
            {
                var child = footer.Children[i];
                if (Grid.GetColumn(child) == 1) footer.Children.RemoveAt(i);
            }
            Grid.SetColumn(newActions, 1);
            footer.Children.Add(newActions);
        }

        private static Button MakePrimaryButton(string text, RoutedEventHandler onClick)
        {
            var b = new Button
            {
                Content = text,
                Background = new SolidColorBrush(Color.FromRgb(0x60, 0xCD, 0xFF)),
                Foreground = new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x14)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(18, 5, 18, 5),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            b.Click += onClick;
            return b;
        }

        private static Button MakeSecondaryButton(string text, RoutedEventHandler onClick)
        {
            var b = new Button
            {
                Content = text,
                Background = new SolidColorBrush(Color.FromArgb(0x10, 0xFF, 0xFF, 0xFF)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(18, 5, 18, 5),
                FontSize = 11,
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0),
            };
            b.Click += onClick;
            return b;
        }

        private static Button MakeLinkButton(string text, RoutedEventHandler onClick)
        {
            var b = new Button
            {
                Content = text,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(0x90, 0x90, 0x9A)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 5, 10, 5),
                FontSize = 10,
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0),
            };
            b.Click += onClick;
            return b;
        }

        // -- Release history view (unchanged) -----------------------------------

        private async System.Threading.Tasks.Task LoadReleases()
        {
            if (_pendingUpdate != null) return; // configured for pending update — skip history

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
            if (_pendingUpdate != null && _outcome == UpdateDialogOutcome.Later)
                DialogResult = false;
            Close();
        }
    }
}
