# logfmt-dotnet

# Usage Examples
You can start using the formatter with either of the 2 methods below. Both will require adding the log formatter to the logging builder
``` c#
var builder = WebApplication.CreateBuilder(args);
...
// Make the logfmt console formatter available
builder.Logging.AddConsoleFormatter<LogfmtConsoleFormatter, LogfmtConsoleFormatterOptions>();
```

## Host Builder Example
``` c#
internal static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .ConfigureLogging(loggingBuilder =>
        {
            loggingBuilder
                .ClearProviders()
                .AddConsoleFormatter<LogfmtConsoleFormatter, LogfmtConsoleFormatterOptions>(x =>
                {
                    x.IncludeScopes = true;
                    x.UseUtcTimestamp = true;
                })
                // you must set the formatter name to match the formatter you want to use, in this case "logfmt"
                .AddConsole(x => x.FormatterName = "logfmt")
                .SetMinimumLevel(LogLevel.Debug);
        })
```

## appsettings.json Example

``` json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "System": "Information",
      "Microsoft": "Information"
    },
    "Console": {
      "FormatterName": "logfmt",
      "FormatterOptions": {
        "StackTraceFormat": "Full",
        "IncludeScopes": true,
        "IncludeEventId": true,
        "IncludeComponent": true,
        "IncludeLogTemplate": true,
        "IncludeStructuredParameters": true,
        "FirstLineSignifier": "\u200B",
        "LogLevelColors": {
          "Error": {
            "Background": "Black",
            "Foreground": "Red"
          }
        }
      }
    }
  }
}
```