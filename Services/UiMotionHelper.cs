using System;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;

namespace StormSwitchBox.Services
{
    /// <summary>
    /// GPU-accelerated UI transition helper powered by Windows Composition API (60-240 Hz).
    /// </summary>
    public static class UiMotionHelper
    {
        public static void AnimateEntrance(UIElement? element, float offsetY = 12f, int durationMs = 190)
        {
            if (element == null) return;
            try
            {
                ElementCompositionPreview.SetIsTranslationEnabled(element, true);
                var visual = ElementCompositionPreview.GetElementVisual(element);
                var compositor = visual.Compositor;
                var duration = TimeSpan.FromMilliseconds(Math.Clamp(durationMs, 60, 500));
                var easeOut = compositor.CreateCubicBezierEasingFunction(
                    new Vector2(0.16f, 1.0f),
                    new Vector2(0.30f, 1.0f));

                var opacityAnim = compositor.CreateScalarKeyFrameAnimation();
                opacityAnim.Duration = duration;
                opacityAnim.InsertKeyFrame(0f, 0f);
                opacityAnim.InsertKeyFrame(1f, 1f, easeOut);
                visual.StartAnimation("Opacity", opacityAnim);

                if (MathF.Abs(offsetY) > 0.1f)
                {
                    var moveAnim = compositor.CreateVector3KeyFrameAnimation();
                    moveAnim.Duration = duration;
                    moveAnim.InsertKeyFrame(0f, new Vector3(0f, offsetY, 0f));
                    moveAnim.InsertKeyFrame(1f, Vector3.Zero, easeOut);
                    visual.StartAnimation("Translation", moveAnim);
                }
            }
            catch { }
        }

        public static void AnimateSlideFade(UIElement? element, float offsetX = 16f, int durationMs = 170)
        {
            if (element == null) return;
            try
            {
                ElementCompositionPreview.SetIsTranslationEnabled(element, true);
                var visual = ElementCompositionPreview.GetElementVisual(element);
                var compositor = visual.Compositor;
                var duration = TimeSpan.FromMilliseconds(Math.Clamp(durationMs, 60, 500));
                var easeOut = compositor.CreateCubicBezierEasingFunction(
                    new Vector2(0.16f, 1.0f),
                    new Vector2(0.30f, 1.0f));

                var opacityAnim = compositor.CreateScalarKeyFrameAnimation();
                opacityAnim.Duration = duration;
                opacityAnim.InsertKeyFrame(0f, 0f);
                opacityAnim.InsertKeyFrame(1f, 1f, easeOut);
                visual.StartAnimation("Opacity", opacityAnim);

                if (MathF.Abs(offsetX) > 0.1f)
                {
                    var moveAnim = compositor.CreateVector3KeyFrameAnimation();
                    moveAnim.Duration = duration;
                    moveAnim.InsertKeyFrame(0f, new Vector3(offsetX, 0f, 0f));
                    moveAnim.InsertKeyFrame(1f, Vector3.Zero, easeOut);
                    visual.StartAnimation("Translation", moveAnim);
                }
            }
            catch { }
        }
    }
}
