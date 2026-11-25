using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Clip.UI
{
    /// <summary>
    /// Attached row content: fills a realised row's <see cref="TextBlock.Inlines"/> from its ClipRow.
    /// Two attached properties on purpose — the row supplies the item, the row's Query re-renders the
    /// match runs, and neither needs an event subscription that could outlive the container.
    /// </summary>
    public static class RowContent
    {
        public static readonly DependencyProperty RowProperty = DependencyProperty.RegisterAttached(
            "Row", typeof(ClipRow), typeof(RowContent), new PropertyMetadata(null, OnChanged));

        public static readonly DependencyProperty QueryProperty = DependencyProperty.RegisterAttached(
            "Query", typeof(string), typeof(RowContent), new PropertyMetadata(null, OnChanged));

        public static ClipRow GetRow(DependencyObject d) => (ClipRow)d.GetValue(RowProperty);
        public static void SetRow(DependencyObject d, ClipRow value) => d.SetValue(RowProperty, value);
        public static string GetQuery(DependencyObject d) => (string)d.GetValue(QueryProperty);
        public static void SetQuery(DependencyObject d, string value) => d.SetValue(QueryProperty, value);

        static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBlock block) return;
            var inlines = RichRow.Inlines(GetRow(block));
            block.Inlines.Clear();
            foreach (var inline in inlines) block.Inlines.Add(inline);
        }

        // ---- the run record every builder path emits -------------------------

        internal const int MaxRuns = 300;
        internal const int MaxChars = 2000;   // stops after the sixth rendered line is well past

        // UI/Themes/Typography.xaml owns the numbers: one place sizes every row with the chrome.
        internal static readonly double RowSize = TypeSize("FontBodySize", 13);
        internal static readonly double RowLine = TypeSize("FontBodyLine", 18);

        static double TypeSize(string key, double fallback) =>
            Application.Current?.TryFindResource(key) is double d ? d : fallback;

        internal static readonly FontFamily ContentFamily = new(new Uri("pack://application:,,,/"), "./Assets/Fonts/#Inter");
        internal static readonly FontFamily MonoFamily = new(new Uri("pack://application:,,,/"), "./Assets/Fonts/#IBM Plex Mono");

        internal sealed class RichRun
        {
            public string Text; // null = a line break
            public Brush Fg;
            public Brush Bg;
            public FontFamily Family;
            public bool Bold, Italic, Underline, Strike, Mono;
            public double Size;

            public Inline ToInline()
            {
                if (Text == null) return new LineBreak();
                var run = new Run(Text)
                {
                    FontFamily = Family ?? (Mono ? MonoFamily : ContentFamily),
                    FontSize = Size > 0 ? Size : RowSize
                };
                if (Fg != null) run.Foreground = Fg;
                if (Bg != null) run.Background = Bg;
                if (Bold) run.FontWeight = FontWeights.Bold;
                if (Italic) run.FontStyle = FontStyles.Italic;
                if (Underline && Strike) run.TextDecorations = UnderlineStrike;
                else if (Underline) run.TextDecorations = TextDecorations.Underline;
                else if (Strike) run.TextDecorations = TextDecorations.Strikethrough;
                return run;
            }
        }

        static readonly TextDecorationCollection UnderlineStrike = Combine();

        static TextDecorationCollection Combine()
        {
            var collection = new TextDecorationCollection
            {
                new TextDecoration { Location = TextDecorationLocation.Underline },
                new TextDecoration { Location = TextDecorationLocation.Strikethrough }
            };
            collection.Freeze();
            return collection;
        }

        // ---- theme accents (UI-layer annotations: match highlight) --
        // ---- brushes ------------------------------------------------------

        internal static Brush ThemeBrush(string key) => Application.Current?.TryFindResource(key) as Brush;

        /// <summary>LAYER B: original-content fallbacks. Never used for app chrome.</summary>
        internal static Brush ContentBrush(string key) => Application.Current?.TryFindResource(key) as Brush;
    }
}
