using System;
using System.IO;

namespace MonoGameEditor.Runtime
{
    public class RuntimeLogger : MonoGameEditor.Core.ILogger
    {
        private static StreamWriter _writer;

        static RuntimeLogger()
        {
            try
            {
                _writer = new StreamWriter("GameLog.txt", true) { AutoFlush = true };
                _writer.WriteLine($"=== Game started at {DateTime.Now} ===");
            }
            catch { }
        }

        public void Log(string message) => WriteMessage("INFO", message);
        public void LogWarning(string message) => WriteMessage("WARN", message);
        public void LogError(string message) => WriteMessage("ERROR", message);

        private void WriteMessage(string type, string msg)
        {
            string line = $"[{type}] {msg}";
            Console.WriteLine(line);
            try { _writer?.WriteLine($"{DateTime.Now:HH:mm:ss} {line}"); } catch { }
        }
    }
}
