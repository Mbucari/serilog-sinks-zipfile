# Serilog.Sinks.ZipFile

Writes [Serilog](https://serilog.net) events to a text file, as well as supplemntal log files, into a zip archive.

### Getting started

Install the [Serilog.Sinks.ZipFile](https://www.nuget.org/packages/Serilog.Sinks.ZipFile/) package from NuGet:

```powershell
Install-Package Serilog.Sinks.ZipFile
```

To configure the sink in C# code, call `WriteTo.ZipFile()` during logger configuration:

```csharp
var log = new LoggerConfiguration()
    .WriteTo.ZipFile("log.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();
```

This will append the time period to the filename, creating a file set like:

```
log20180631.txt
log20180701.txt
log20180702.txt
```

> **Important**: Only one process may write to a log file at a given time.

### Logging Files into the Zip

Supplemental log files are detected by pattern matching. If the logged type contains properties named 'filename' and 'filedata' (case insensative) with types `string` and `byte[]` respectively, the type is destructured into a format that ZipFileSink can parse, and the file is added to the zip. File names are prepended with the timestamp. By defauly, files are compressed with `CompressionLevel.Fastest`. If the logged object contains a property of type `System.IO.Compression.CompressionLevel`, that compression level will be used instead.

Example:

```C#
Serilog.Log.Logger.Information(
    "{@file}",
    new
    {
        filename = "somefile.txt",
        filedata = System.Text.Encoding.UTF8.GetBytes("Some text in a file")
    });
```

### Rolling policies

To create a log file per day or other time period, specify a `rollingInterval` as shown in the examples above.

Specifying both `rollingInterval` will cause both policies to be applied, while specifying neither will result in all events being written to a single file.

Old files will be cleaned up as per `retainedFileCountLimit` - the default is 31.

### XML `<appSettings>` configuration

To use the file sink with the [Serilog.Settings.AppSettings](https://github.com/serilog/serilog-settings-appsettings) package, first install that package if you haven't already done so:

```powershell
Install-Package Serilog.Settings.AppSettings
```

Instead of configuring the logger in code, call `ReadFrom.AppSettings()`:

```csharp
var log = new LoggerConfiguration()
    .ReadFrom.AppSettings()
    .CreateLogger();
```

In your application's `App.config` or `Web.config` file, specify the file sink assembly and required path format under the `<appSettings>` node:

```xml
<configuration>
  <appSettings>
    <add key="serilog:using:ZipFile" value="Serilog.Sinks.ZipFile" />
    <add key="serilog:write-to:ZipFile.path" value="log.txt" />
```

The parameters that can be set through the `serilog:write-to:ZipFile` keys are the method parameters accepted by the `WriteTo.ZipFile()` configuration method. 

In XML and JSON configuration formats, environment variables can be used in setting values. This means, for instance, that the log file path can be based on `TMP` or `APPDATA`:

```xml
    <add key="serilog:write-to:ZipFile.path" value="%APPDATA%\MyApp\log.txt" />
```

### JSON `appsettings.json` configuration

To use the file sink with _Microsoft.Extensions.Configuration_, for example with ASP.NET Core or .NET Core, use the [Serilog.Settings.Configuration](https://github.com/serilog/serilog-settings-configuration) package. First install that package if you have not already done so:

```powershell
Install-Package Serilog.Settings.Configuration
```

Instead of configuring the file directly in code, call `ReadFrom.Configuration()`:

```csharp
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .CreateLogger();
```

In your `appsettings.json` file, under the `Serilog` node, :

```json
{
  "Serilog": {
    "WriteTo": [
      { "Name": "ZipFile", "Args": { "path": "log.txt", "rollingInterval": "Day" } }
    ]
  }
}
```

See the XML `<appSettings>` example above for a discussion of available `Args` options.

### Controlling event formatting

The file sink creates events in a fixed text format by default:

```
2018-07-06 09:02:17.148 +10:00 [INF] HTTP GET / responded 200 in 1994 ms
```

The format is controlled using an _output template_, which the file configuration method accepts as an `outputTemplate` parameter.

The default format above corresponds to an output template like:

```csharp
  .WriteTo.ZipFile("log.txt",
    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
```

##### JSON event formatting

To write events to the file in an alternative format such as [JSON](https://github.com/serilog/serilog-formatting-compact), pass an `ITextFormatter` as the first argument:

```csharp
    // Install-Package Serilog.Formatting.Compact
    .WriteTo.ZipFile(new CompactJsonFormatter(), "log.txt")
```

### Shared log files

To enable multi-process shared log files, set `shared` to `true`:

```csharp
    .WriteTo.ZipFile("log.txt", shared: true)
```

### Auditing

The file sink can operate as an audit file through `AuditTo`:

```csharp
    .AuditTo.ZipFile("audit.txt")
```

Only a limited subset of configuration options are currently available in this mode.

### Performance

By default, the file sink will flush each event written through it to disk.

The [Serilog.Sinks.Async](https://github.com/serilog/serilog-sinks-async) package can be used to wrap the file sink and perform all disk access on a background worker thread.
