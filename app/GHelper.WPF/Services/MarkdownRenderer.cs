using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace GHelper.WPF.Services
{
    /// <summary>
    /// Minimal markdown renderer for GitHub release notes.
    /// Supports: ## H2, ### H3, bullet lists (- / *), paragraphs, **bold**, and blank-line breaks.
    /// Renders into a StackPanel of styled TextBlocks.
    /// </summary>
    public static class MarkdownRenderer
    {
        private static readonly Color TextColor = Color.FromRgb(0xE0, 0xE0, 0xE5);
        private static readonly Color DimColor = Color.FromRgb(0xA0, 0xA0, 0xAA);
        private static readonly Color AccentColor = Color.FromRgb(0x60, 0xCD, 0xFF);

        public static UIElement Render(string markdown)
        {
            var stack = new StackPanel();
            if (string.IsNullOrWhiteSpace(markdown))
            {
                stack.Children.Add(MakeParagraph("(no release notes)", DimColor));
                return stack;
            }

            var lines = markdown.Replace("\r\n", "\n").Split('\n');
            bool lastWasHeader = true; // suppresses leading blank padding

            foreach (var rawLine in lines)
            {
                string line = rawLine.TrimEnd();

                if (string.IsNullOrWhiteSpace(line))
                {
                    if (!lastWasHeader)
                        stack.Children.Add(new Border { Height = 8 });
                    lastWasHeader = true;
                    continue;
                }

                if (line.StartsWith("### "))
                {
                    stack.Children.Add(MakeHeader(line.Substring(4), 12));
                    lastWasHeader = true;
                }
                else if (line.StartsWith("## "))
                {
                    stack.Children.Add(MakeHeader(line.Substring(3), 14));
                    lastWasHeader = true;
                }
                else if (line.StartsWith("# "))
                {
                    stack.Children.Add(MakeHeader(line.Substring(2), 16));
                    lastWasHeader = true;
                }
                else if (line.StartsWith("- ") || line.StartsWith("* "))
                {
                    stack.Children.Add(MakeBullet(line.Substring(2)));
                    lastWasHeader = false;
                }
                else
                {
                    stack.Children.Add(MakeParagraph(line, TextColor));
                    lastWasHeader = false;
                }
            }

            return stack;
        }

        private static TextBlock MakeHeader(string text, double fontSize)
        {
            var tb = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI Variable"),
                FontSize = fontSize,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF7)),
                Margin = new Thickness(0, 10, 0, 6),
                TextWrapping = TextWrapping.Wrap,
            };
            ApplyInlines(tb, text);
            return tb;
        }

        private static TextBlock MakeParagraph(string text, Color color)
        {
            var tb = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI Variable"),
                FontSize = 11,
                Foreground = new SolidColorBrush(color),
                Margin = new Thickness(0, 1, 0, 1),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 17,
            };
            ApplyInlines(tb, text);
            return tb;
        }

        private static Grid MakeBullet(string text)
        {
            var grid = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var bullet = new TextBlock
            {
                Text = "•",
                FontSize = 11,
                Foreground = new SolidColorBrush(AccentColor),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 1, 0, 0),
            };

            var content = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI Variable"),
                FontSize = 11,
                Foreground = new SolidColorBrush(TextColor),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 17,
            };
            ApplyInlines(content, text);

            Grid.SetColumn(bullet, 0);
            Grid.SetColumn(content, 1);
            grid.Children.Add(bullet);
            grid.Children.Add(content);
            return grid;
        }

        /// <summary>
        /// Parses a line for **bold** and inline code (`x`), adding Inlines to the given TextBlock.
        /// </summary>
        private static void ApplyInlines(TextBlock target, string text)
        {
            // Simple tokenizer: ** ... ** for bold, ` ... ` for code
            var pattern = new Regex(@"(\*\*[^*]+\*\*|`[^`]+`)");
            int last = 0;
            foreach (Match m in pattern.Matches(text))
            {
                if (m.Index > last)
                    target.Inlines.Add(new Run(text.Substring(last, m.Index - last)));

                string token = m.Value;
                if (token.StartsWith("**"))
                {
                    target.Inlines.Add(new Run(token.Substring(2, token.Length - 4)) { FontWeight = FontWeights.SemiBold });
                }
                else if (token.StartsWith("`"))
                {
                    target.Inlines.Add(new Run(token.Substring(1, token.Length - 2))
                    {
                        FontFamily = new FontFamily("Consolas, Cascadia Mono"),
                        FontSize = 10.5,
                        Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                    });
                }
                last = m.Index + m.Length;
            }
            if (last < text.Length)
                target.Inlines.Add(new Run(text.Substring(last)));
        }
    }
}
