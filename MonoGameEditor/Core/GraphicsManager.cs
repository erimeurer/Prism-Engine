using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace MonoGameEditor.Core
{
    public static class GraphicsManager
    {
        public static GraphicsDevice? GraphicsDevice { get; set; }
        public static ContentManager? ContentManager { get; set; }

        private static readonly Dictionary<GraphicsDevice, ContentManager> _deviceContentMap = new();

        public static void RegisterContentManager(GraphicsDevice device, ContentManager content)
        {
            _deviceContentMap[device] = content;
            // Also set as global fallback for now
            GraphicsDevice = device;
            ContentManager = content;
        }

        public static ContentManager? GetContentManager(GraphicsDevice device)
        {
            if (device == null) return ContentManager;
            if (_deviceContentMap.TryGetValue(device, out var content)) return content;
            return ContentManager;
        }
    }
}
