using Microsoft.Xna.Framework.Graphics;
using System;

namespace MonoGameEditor.Core.Shaders
{
    /// <summary>
    /// Simple IGraphicsDeviceService implementation for temporary ContentManager creation
    /// </summary>
    internal class SimpleGraphicsDeviceService : IGraphicsDeviceService
    {
        public GraphicsDevice GraphicsDevice { get; }

        public event EventHandler<EventArgs>? DeviceCreated;
        public event EventHandler<EventArgs>? DeviceDisposing;
        public event EventHandler<EventArgs>? DeviceReset;
        public event EventHandler<EventArgs>? DeviceResetting;

        public SimpleGraphicsDeviceService(GraphicsDevice device)
        {
            GraphicsDevice = device;
        }
    }
}
