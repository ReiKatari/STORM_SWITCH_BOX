using System.Collections.Concurrent;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace StormSwitchBox.Models
{
    internal static class UiBrushCache
    {
        private static readonly ConcurrentDictionary<uint, SolidColorBrush> _brushes = new();

        public static SolidColorBrush Get(Color color)
        {
            uint key = ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
            if (_brushes.TryGetValue(key, out var cached))
            {
                return cached;
            }

            if (DispatcherQueue.GetForCurrentThread() == null)
            {
                return null!;
            }

            return _brushes.GetOrAdd(key, _ => new SolidColorBrush(color));
        }
    }
}
