using System;

namespace MonoGameEditor.Core
{
    /// <summary>
    /// Abstraction layer for logging that works in both editor and runtime
    /// </summary>
    public static class Logger
    {
        private static ILogger _implementation = null!;

        public static void Initialize(ILogger implementation)
        {
            _implementation = implementation;
        }

        public static void Log(string message)
        {
            _implementation?.Log(message);
        }

        public static void LogWarning(string message)
        {
            _implementation?.LogWarning(message);
        }

        public static void LogError(string message)
        {
            _implementation?.LogError(message);
        }
    }

    public interface ILogger
    {
        void Log(string message);
        void LogWarning(string message);
        void LogError(string message);
    }
}
