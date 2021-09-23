using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace SkyKick.Extensions.Logging.Console
{

    using static AnsiColorCodeHelper;

    public sealed class LogfmtConsoleFormatter : ConsoleFormatter, IDisposable
    {
        private IDisposable? _optionsReloadToken;

        public LogfmtConsoleFormatter(IOptionsMonitor<LogfmtConsoleFormatterOptions> options)
            : base("logfmt")
        {
            FormatterOptions = options.CurrentValue;
            _optionsReloadToken = options.OnChange(ReloadLoggerOptions);
        }

        private void ReloadLoggerOptions(LogfmtConsoleFormatterOptions options)
        {
            FormatterOptions = options;
        }

        public void Dispose()
        {
            _optionsReloadToken?.Dispose();
        }

        internal LogfmtConsoleFormatterOptions FormatterOptions { get; set; }

        public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider scopeProvider, TextWriter textWriter)
        {
            string? message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception);
            if (logEntry.Exception == null && message == null)
            {
                return;
            }

            if (FormatterOptions.FirstLineSignifier is not null)
                textWriter.Write(FormatterOptions.FirstLineSignifier);

            LogLevel logLevel = logEntry.LogLevel;
            ConsoleColorOptions logLevelColors = GetLogLevelConsoleColors(logLevel, FormatterOptions.LogLevelColors);
            var logLevelString = GetLogLevelString(logLevel);

            string? timestamp = null;
            string? timestampFormat = FormatterOptions.TimestampFormat;
            if (timestampFormat != null)
            {
                DateTimeOffset dateTimeOffset = GetCurrentDateTime();
                timestamp = dateTimeOffset.ToString(timestampFormat);
            }
            if (timestamp != null)
            {
                textWriter.Write("ts=");
                textWriter.Write(timestamp);
                textWriter.Write(' ');
            }
            if (logLevelString != null)
            {
                textWriter.Write("level=");
                WriteColoredMessage(textWriter, logLevelString, logLevelColors);
            }
            CreateDefaultLogMessage(textWriter, logEntry, message, scopeProvider, logLevelColors);
        }

        private static void WriteColoredMessage(TextWriter textWriter, string message, ConsoleColorOptions logLevelColors)
        {
            // Order: backgroundcolor, foregroundcolor, Message, reset foregroundcolor, reset backgroundcolor
            if (logLevelColors.Background.HasValue)
            {
                textWriter.Write(GetBackgroundColorEscapeCode(logLevelColors.Background.Value));
            }
            if (logLevelColors.Foreground.HasValue)
            {
                textWriter.Write(GetForegroundColorEscapeCode(logLevelColors.Foreground.Value));
            }
            textWriter.Write(message);
            if (logLevelColors.Foreground.HasValue)
            {
                textWriter.Write(DefaultForegroundColor); // reset to default foreground color
            }
            if (logLevelColors.Background.HasValue)
            {
                textWriter.Write(DefaultBackgroundColor); // reset to the background color
            }
        }

        private void CreateDefaultLogMessage<TState>(TextWriter textWriter, in LogEntry<TState> logEntry, string? message, IExternalScopeProvider scopeProvider, ConsoleColorOptions logLevelColors)
        {
            int eventId = logEntry.EventId.Id;
            Exception? exception = logEntry.Exception;

            WriteMessage(textWriter, "msg", message, logLevelColors);

            if (exception is not null)
            {
                WriteMessage(textWriter, "exception", exception.GetType().FullName);
                WriteMessage(textWriter, "err", exception.Message, logLevelColors);
            }

            // category and event id

            if (FormatterOptions.IncludeComponent)
            {
                textWriter.Write(" component=");
                textWriter.Write(logEntry.Category);
            }
            if (FormatterOptions.IncludeEventId)
            {
                textWriter.Write(" event_id=");

#if NETCOREAPP
                Span<char> span = stackalloc char[10];
                if (eventId.TryFormat(span, out int charsWritten))
                    textWriter.Write(span.Slice(0, charsWritten));
                else
#endif
                    textWriter.Write(eventId.ToString());
            }

            if (logEntry.State is IReadOnlyCollection<KeyValuePair<string, object>> stateProperties)
            {
                var originalFormat = stateProperties.Where(kv => kv.Key == "{OriginalFormat}");

                if (FormatterOptions.IncludeLogTemplate && originalFormat.Any())
                    WriteMessage(textWriter, "msg_fmt", EscapeMessage(originalFormat.First().Value?.ToString()) ?? EscapeMessage(message));

                if (FormatterOptions.IncludeStructuredParameters)
                {
                    var props = stateProperties.Except(originalFormat);

                    foreach (var prop in props)
                    {
                        if (prop.Value is object value)
                            WriteMessage(textWriter, GetNormalizedKeyCase(prop.Key), value.ToString());
                    }
                }
            }

            if (FormatterOptions.IncludeScopes)
                // scope information
                WriteScopeInformation(textWriter, scopeProvider);

            if (FormatterOptions.StackTraceFormat != StackTraceFormat.None && exception?.StackTrace is not null)
            {
                //textWriter.Write("\n");

                if (FormatterOptions.StackTraceFormat == StackTraceFormat.Full)
                    textWriter.Write('\n');
                textWriter.Write(exception.StackTrace);//.Replace("\n", "").Replace("\r", ""));

                if (FormatterOptions.StackTraceFormat == StackTraceFormat.SingleLine)
                    textWriter.Write(exception.StackTrace.Replace("\n", "\\u000A").Replace("\r", "\\u000D"));
            }

            textWriter.Write("\n");
        }

        private static string? EscapeMessage(string? message)
        {
            return message?.Replace(Environment.NewLine, "\\u000A").Replace("\"", "\\u0022");
        }

        private static void WriteMessage(TextWriter textWriter, string key, string? message, ConsoleColorOptions? logLevelColors = null)
        {
            string? newMessage = EscapeMessage(message);

            if (!string.IsNullOrEmpty(newMessage))
            {
                textWriter.Write($" {key}=");

                if (newMessage.Contains(' '))
                {
                    textWriter.Write('"');
                    WriteWithColorDetection(textWriter, newMessage, logLevelColors);
                    textWriter.Write('"');
                }
                else
                {
                    WriteWithColorDetection(textWriter, newMessage, logLevelColors);
                }
            }

            static void WriteWithColorDetection(TextWriter textWriter, string message, ConsoleColorOptions? logLevelColors)
            {
                if (logLevelColors is not null)
                    WriteColoredMessage(textWriter, message, logLevelColors);
                else
                    textWriter.Write(message);
            }
        }

        private DateTimeOffset GetCurrentDateTime()
        {
            return FormatterOptions.UseUtcTimestamp ? DateTimeOffset.UtcNow : DateTimeOffset.Now;
        }

        private static string GetLogLevelString(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Trace => "trace",
                LogLevel.Debug => "debug",
                LogLevel.Information => "info",
                LogLevel.Warning => "warn",
                LogLevel.Error => "error",
                LogLevel.Critical => "crit",
                _ => throw new ArgumentOutOfRangeException(nameof(logLevel))
            };
        }

        private ConsoleColorOptions GetLogLevelConsoleColors(LogLevel logLevel, Dictionary<LogLevel, ConsoleColorOptions>? overrides)
        {
            bool disableColors = (FormatterOptions.ColorBehavior == LoggerColorBehavior.Disabled) ||
                (FormatterOptions.ColorBehavior == LoggerColorBehavior.Default && System.Console.IsOutputRedirected);
            if (disableColors)
            {
                return new ConsoleColorOptions { Foreground = null, Background = null };
            }

            if (overrides is not null && overrides.ContainsKey(logLevel))
                return overrides[logLevel];

            // We must explicitly set the background color if we are setting the foreground color,
            // since just setting one can look bad on the users console.
            return logLevel switch
            {
                LogLevel.Trace => new ConsoleColorOptions { Foreground = ConsoleColor.Gray, Background = ConsoleColor.Black },
                LogLevel.Debug => new ConsoleColorOptions { Foreground = ConsoleColor.Gray, Background = ConsoleColor.Black },
                LogLevel.Information => new ConsoleColorOptions { Foreground = ConsoleColor.DarkGreen, Background = ConsoleColor.Black },
                LogLevel.Warning => new ConsoleColorOptions { Foreground = ConsoleColor.Yellow, Background = ConsoleColor.Black },
                LogLevel.Error => new ConsoleColorOptions { Foreground = ConsoleColor.Black, Background = ConsoleColor.DarkRed },
                LogLevel.Critical => new ConsoleColorOptions { Foreground = ConsoleColor.White, Background = ConsoleColor.DarkRed },
                _ => new ConsoleColorOptions { Foreground = null, Background = null }
            };
        }

        private static string GetNormalizedKeyCase(string key)
        {
            var sb = new StringBuilder(key.Length);
            var first = true;
            foreach (var c in key)
            {
                if (char.IsUpper(c))
                {
                    if (!first)
                    {
                        sb.Append('_');
                    }
                    sb.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    sb.Append(c);
                }
                first = false;
            }

            return sb.ToString();
        }

        private void WriteScopeInformation(TextWriter textWriter, IExternalScopeProvider scopeProvider)
        {
            if (FormatterOptions.IncludeScopes && scopeProvider != null)
            {
                scopeProvider.ForEachScope((scope, state) =>
                {
                    if (scope is IReadOnlyList<KeyValuePair<string, object>> props)
                    {
                        foreach (var prop in props)
                        {
                            var value = prop.Value?.ToString();
                            if (value is not null && value.Contains(' '))
                                value = $"\"{value}\"";

                            state.Write(' ');
                            state.Write(GetNormalizedKeyCase(prop.Key));
                            state.Write("=");
                            if (value is not null && value.Contains(' '))
                            {
                                state.Write('"');
                                state.Write(value);
                                state.Write('"');
                            }
                            else
                            {
                                state.Write(value);
                            }
                        }
                    }
                }, textWriter);
            }
        }
    }
}