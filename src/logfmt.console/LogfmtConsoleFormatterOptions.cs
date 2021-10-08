using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace SkyKick.Extensions.Logging.Console.Logfmt
{
    public class LogfmtConsoleFormatterOptions : ConsoleFormatterOptions
    {
        public LoggerColorBehavior ColorBehavior { get; set; }

        public StackTraceFormat StackTraceFormat { get; set; } = StackTraceFormat.SingleLine;

        public bool IncludeLogTemplate { get; set; } = true;

        public bool IncludeEventId { get; set; } = true;

        public bool IncludeComponent { get; set; } = true;

        public bool IncludeStructuredParameters { get; set; } = true;

        public string? FirstLineSignifier { get; set; } = "\u200B"; // such as zwsp

        public Dictionary<LogLevel, ConsoleColorOptions>? LogLevelColors { get; set; }
    }
}
