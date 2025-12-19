using System;
using System.IO;

namespace MonoGameEditor.ViewModels
{
    public class RuntimeConsole
    {
        private static string _logFilePath = "GameLog.txt";
        private static StreamWriter _writer;

        static RuntimeConsole()
        {
            try
            {
                _writer = new StreamWriter(_logFilePath, true) { AutoFlush = true };
                _writer.WriteLine($"=== Game started at {DateTime.Now} ===");
            }
            catch { }
        }

        public static void Log(string message) => WriteMessage("INFO", message);
        public static void LogInfo(string message) => WriteMessage("INFO", message);
        public static void LogWarning(string message) => WriteMessage("WARN", message);
        public static void LogError(string message) => WriteMessage("ERROR", message);

        private static void WriteMessage(string type, string msg)
        {
            string line = $"[{type}] {msg}";
            Console.WriteLine(line);
            try { _writer?.WriteLine($"{DateTime.Now:HH:mm:ss} {line}"); } catch { }
        }
    }
}
