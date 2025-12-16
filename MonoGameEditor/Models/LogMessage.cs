using System;

namespace MonoGameEditor.Models
{
    /// <summary>
    /// Represents a console log message with type, timestamp, and formatting
    /// </summary>
    public class LogMessage
    {
        public DateTime Timestamp { get; set; }
        public string Message { get; set; }
        public LogType Type { get; set; }

        public string FormattedMessage => $"[{Timestamp:HH:mm:ss}] {Message}";

        public string Color => Type switch
        {
            LogType.Warning => "#FFA500",  // Orange
            LogType.Error => "#FF4444",    // Red
            _ => "#CCCCCC"                 // Light gray (Info)
        };

        public string Icon => Type switch
        {
            LogType.Warning => "⚠",
            LogType.Error => "✗",
            _ => "ℹ"
        };

        public LogMessage(string message, LogType type = LogType.Info)
        {
            Timestamp = DateTime.Now;
            Message = message;
            Type = type;
        }
    }
}
