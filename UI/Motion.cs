using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media;

namespace Clip.UI
{
    /// <summary>
    /// The app's only motion vocabulary (docs\RESEARCH-motion-ui.md §2/§6). Every call site reads
    /// <see cref="On"/> at the moment of use, so the Windows "Animation effects" toggle is live.
    /// </summary>
    internal static class Motion
    {
        public static bool On => SystemParameters.ClientAreaAnimation;

        /// <summary>Spline key frame animating from the property's current base value.</summary>
        public static DoubleAnimationUsingKeyFrames Spline(double to, double ms, double x1, double y1, double x2, double y2)
            => new DoubleAnimationUsingKeyFrames
            {
                KeyFrames = { new SplineDoubleKeyFrame(to, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(ms)), new KeySpline(x1, y1, x2, y2)) }
            };

        public static DoubleAnimation Linear(double to, double ms)
            => new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms));

        /// <summary>Ortu's 380 ms confirmation: 150 in, 80 hold, 150 out.</summary>
        public static DoubleAnimationUsingKeyFrames Pulse()
            => new DoubleAnimationUsingKeyFrames
            {
                KeyFrames =
                {
                    new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150))),
                    new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(230))),
                    new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(380)))
                }
            };
    }
}
