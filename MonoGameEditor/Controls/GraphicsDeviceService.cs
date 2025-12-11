using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Microsoft.Xna.Framework.Graphics;
using MonoGameEditor.ViewModels;

namespace MonoGameEditor.Controls
{
    /// <summary>
    /// GraphicsDevice service - creates INDEPENDENT devices per window.
    /// No sharing = no contention.
    /// </summary>
    public class GraphicsDeviceService : IGraphicsDeviceService
    {
        private static Dictionary<IntPtr, GraphicsDeviceService> _instances = new();
        private static object _lock = new object();

        public GraphicsDevice GraphicsDevice { get; private set; }

        public event EventHandler<EventArgs>? DeviceCreated;
        public event EventHandler<EventArgs>? DeviceDisposing;
        public event EventHandler<EventArgs>? DeviceReset;
        public event EventHandler<EventArgs>? DeviceResetting;

        private GraphicsDeviceService(IntPtr windowHandle, int width, int height)
        {
            var parameters = new PresentationParameters
            {
                BackBufferWidth = Math.Max(1, width),
                BackBufferHeight = Math.Max(1, height),
                BackBufferFormat = SurfaceFormat.Color,
                DepthStencilFormat = DepthFormat.Depth24,
                DeviceWindowHandle = windowHandle,
                PresentationInterval = PresentInterval.Immediate,
                IsFullScreen = false
            };

            GraphicsDevice = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.HiDef, parameters);
            DeviceCreated?.Invoke(this, EventArgs.Empty);
        }

        public static GraphicsDeviceService AddRef(IntPtr windowHandle, int width = 1, int height = 1)
        {
            lock (_lock)
            {
                if (!_instances.ContainsKey(windowHandle))
                {
                    _instances[windowHandle] = new GraphicsDeviceService(windowHandle, width, height);
                    ConsoleViewModel.Log($"[GraphicsDeviceService] Created NEW device for Handle={windowHandle}. Total devices: {_instances.Count}");
                }
                else
                {
                    ConsoleViewModel.Log($"[GraphicsDeviceService] Reusing existing device for Handle={windowHandle}");
                }
                return _instances[windowHandle];
            }
        }

        public void Release(IntPtr windowHandle)
        {
            lock (_lock)
            {
                if (_instances.ContainsKey(windowHandle))
                {
                    ConsoleViewModel.Log($"[GraphicsDeviceService] RELEASING device for Handle={windowHandle}. Total devices before: {_instances.Count}");
                    DeviceDisposing?.Invoke(this, EventArgs.Empty);
                    GraphicsDevice?.Dispose();
                    _instances.Remove(windowHandle);
                    ConsoleViewModel.Log($"[GraphicsDeviceService] Total devices after: {_instances.Count}");
                }
            }
        }
    }
}
