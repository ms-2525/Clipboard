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
        internal const double RowSize = 13;   // content type is Segoe UI 13/18, like the plain body

        internal static readonly FontFamily ContentFamily = new("Segoe UI");
        internal static readonly FontFamily MonoFamily = new("Consolas");

        internal sealed class RichRun
        {
            public string Text; // null = a line break
            public Brush Fg;
            public bool Bold, Italic, Underline, Strike, Mono;
            public double Size;

            public Inline ToInline()
            {
                if (Text == null) return new LineBreak();
                var run = new Run(Text)
                {
                    FontFamily = Mono ? MonoFamily : ContentFamily,
                    FontSize = Size > 0 ? Size : RowSize
                };
                if (Fg != null) run.Foreground = Fg;
                if (Bold) run.FontWeight = FontWeights.SemiBold;
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

        // ---- theme-aware colour ---------------------------------------------

        /// <summary>
        /// FOREGROUND ONLY (S0 frozen decision): a run colour within 60 luma of the window background
        /// is illegible on this theme, so it becomes Fg75. Neutral black/white keep the row's own level
        /// instead — that is what makes a plain-looking Word body look like a plain clip.
        /// </summary>
        internal static Brush Legible(Brush brush, double appLuma, Brush fg75)
        {
            if (brush is not SolidColorBrush solid || solid.Color.A == 0) return null;
            var color = solid.Color;
            if (color.R == color.G && color.G == color.B && (color.R == 0 || color.R == 255)) return null;
            return Math.Abs(Luma(color) - appLuma) < 60 ? fg75 : solid;
        }

        internal static double Luma(Color color) => 0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B;

        internal static Color ThemeColor(string key) =>
            ThemeBrush(key) is SolidColorBrush brush ? brush.Color : Colors.Black;

        internal static Brush ThemeBrush(string key) => Application.Current?.TryFindResource(key) as Brush;
    }
}
