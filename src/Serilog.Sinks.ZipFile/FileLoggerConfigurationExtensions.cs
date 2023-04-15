// Copyright 2013-2017 Serilog Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.IO;
using System.Text;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;
using Serilog.Formatting.Json;
using Serilog.Sinks.ZipFile;

// ReSharper disable RedundantArgumentDefaultValue, MethodOverloadWithOptionalParameter

namespace Serilog
{
    /// <summary>Extends <see cref="LoggerConfiguration"/> with methods to add file sinks.</summary>
    public static class FileLoggerConfigurationExtensions
    {
        const int DefaultRetainedFileCountLimit = 31; // A long month of logs
        const string DefaultOutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

        /// <summary>
        /// Write log events to the specified file.
        /// </summary>
        /// <param name="sinkConfiguration">Logger sink configuration.</param>
        /// <param name="path">Path to the file.</param>
        /// <param name="restrictedToMinimumLevel">The minimum level for
        /// events passed through the sink. Ignored when <paramref name="levelSwitch"/> is specified.</param>
        /// <param name="levelSwitch">A switch allowing the pass-through minimum level
        /// to be changed at runtime.</param>
        /// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
        /// <param name="outputTemplate">A message template describing the format used to write to the sink.
        /// the default is "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}".</param>
        /// <param name="flushToDiskInterval">If provided, a full disk flush will be performed periodically at the specified interval.</param>
        /// <param name="rollingInterval">The interval at which logging will roll over to a new file.</param>
        /// <param name="retainedFileCountLimit">The maximum number of log files that will be retained,
        /// including the current log file. For unlimited retention, pass null. The default is 31.</param>
        /// <param name="encoding">Character encoding used to write the text file. The default is UTF-8 without BOM.</param>
        /// <param name="retainedFileTimeLimit">The maximum time after the end of an interval that a rolling log file will be retained.
        /// Must be greater than or equal to <see cref="TimeSpan.Zero"/>.
        /// Ignored if <paramref see="rollingInterval"/> is <see cref="RollingInterval.Infinite"/>.
        /// The default is to retain files indefinitely.</param>
        /// <returns>Configuration object allowing method chaining.</returns>
        /// <exception cref="ArgumentNullException">When <paramref name="sinkConfiguration"/> is <code>null</code></exception>
        /// <exception cref="ArgumentNullException">When <paramref name="path"/> is <code>null</code></exception>
        /// <exception cref="ArgumentNullException">When <paramref name="outputTemplate"/> is <code>null</code></exception>
        /// <exception cref="IOException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="PathTooLongException">When <paramref name="path"/> is too long</exception>
        /// <exception cref="UnauthorizedAccessException">The caller does not have the required permission to access the <paramref name="path"/></exception>
        /// <exception cref="ArgumentException">Invalid <paramref name="path"/></exception>
        public static LoggerConfiguration ZipFile(
            this LoggerSinkConfiguration sinkConfiguration,
            string path,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            string outputTemplate = DefaultOutputTemplate,
            IFormatProvider? formatProvider = null,
            LoggingLevelSwitch? levelSwitch = null,
            TimeSpan? flushToDiskInterval = null,
            RollingInterval rollingInterval = RollingInterval.Infinite,
            int? retainedFileCountLimit = DefaultRetainedFileCountLimit,
            Encoding? encoding = null,
            TimeSpan? retainedFileTimeLimit = null)
        {
            if (sinkConfiguration == null) throw new ArgumentNullException(nameof(sinkConfiguration));
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (outputTemplate == null) throw new ArgumentNullException(nameof(outputTemplate));

            var formatter = new MessageTemplateTextFormatter(outputTemplate, formatProvider);
            return ZipFile(sinkConfiguration, formatter, path, restrictedToMinimumLevel,
                levelSwitch, flushToDiskInterval, rollingInterval, retainedFileCountLimit, encoding, retainedFileTimeLimit);
        }

        /// <summary>
        /// Write log events to the specified file.
        /// </summary>
        /// <param name="sinkConfiguration">Logger sink configuration.</param>
        /// <param name="formatter">A formatter, such as <see cref="JsonFormatter"/>, to convert the log events into
        /// text for the file. If control of regular text formatting is required, use the other
        /// overload of <see cref="ZipFile(LoggerSinkConfiguration, string, LogEventLevel, string, IFormatProvider, LoggingLevelSwitch, TimeSpan?, RollingInterval, int?, Encoding, TimeSpan?)"/>
        /// and specify the outputTemplate parameter instead.
        /// </param>
        /// <param name="path">Path to the file.</param>
        /// <param name="restrictedToMinimumLevel">The minimum level for
        /// events passed through the sink. Ignored when <paramref name="levelSwitch"/> is specified.</param>
        /// <param name="levelSwitch">A switch allowing the pass-through minimum level
        /// to be changed at runtime.</param>
        /// <param name="flushToDiskInterval">If provided, a full disk flush will be performed periodically at the specified interval.</param>
        /// <param name="rollingInterval">The interval at which logging will roll over to a new file.</param>
        /// <param name="retainedFileCountLimit">The maximum number of log files that will be retained,
        /// including the current log file. For unlimited retention, pass null. The default is 31.</param>
        /// <param name="encoding">Character encoding used to write the text file. The default is UTF-8 without BOM.</param>
        /// <param name="retainedFileTimeLimit">The maximum time after the end of an interval that a rolling log file will be retained.
        /// Must be greater than or equal to <see cref="TimeSpan.Zero"/>.
        /// Ignored if <paramref see="rollingInterval"/> is <see cref="RollingInterval.Infinite"/>.
        /// The default is to retain files indefinitely.</param>
        /// <returns>Configuration object allowing method chaining.</returns>
        /// <exception cref="ArgumentNullException">When <paramref name="sinkConfiguration"/> is <code>null</code></exception>
        /// <exception cref="ArgumentNullException">When <paramref name="formatter"/> is <code>null</code></exception>
        /// <exception cref="ArgumentNullException">When <paramref name="path"/> is <code>null</code></exception>
        /// <exception cref="IOException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="PathTooLongException">When <paramref name="path"/> is too long</exception>
        /// <exception cref="UnauthorizedAccessException">The caller does not have the required permission to access the <paramref name="path"/></exception>
        /// <exception cref="ArgumentException">Invalid <paramref name="path"/></exception>
        public static LoggerConfiguration ZipFile(
            this LoggerSinkConfiguration sinkConfiguration,
            ITextFormatter formatter,
            string path,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            LoggingLevelSwitch? levelSwitch = null,
            TimeSpan? flushToDiskInterval = null,
            RollingInterval rollingInterval = RollingInterval.Infinite,
            int? retainedFileCountLimit = DefaultRetainedFileCountLimit,
            Encoding? encoding = null,
            TimeSpan? retainedFileTimeLimit = null)
        {
            if (sinkConfiguration == null) throw new ArgumentNullException(nameof(sinkConfiguration));
            if (formatter == null) throw new ArgumentNullException(nameof(formatter));
            if (path == null) throw new ArgumentNullException(nameof(path));

            return ConfigureFile(sinkConfiguration.Sink, formatter, path, restrictedToMinimumLevel, levelSwitch,
                false, flushToDiskInterval, encoding, rollingInterval,
                retainedFileCountLimit, retainedFileTimeLimit);
        }

        /// <summary>
        /// Write audit log events to the specified file.
        /// </summary>
        /// <param name="sinkConfiguration">Logger sink configuration.</param>
        /// <param name="path">Path to the file.</param>
        /// <param name="restrictedToMinimumLevel">The minimum level for
        /// events passed through the sink. Ignored when <paramref name="levelSwitch"/> is specified.</param>
        /// <param name="levelSwitch">A switch allowing the pass-through minimum level
        /// to be changed at runtime.</param>
        /// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
        /// <param name="outputTemplate">A message template describing the format used to write to the sink.
        /// the default is "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}".</param>
        /// <param name="encoding">Character encoding used to write the text file. The default is UTF-8 without BOM.</param>
        /// <returns>Configuration object allowing method chaining.</returns>
        /// <exception cref="ArgumentNullException">When <paramref name="sinkConfiguration"/> is <code>null</code></exception>
        /// <exception cref="ArgumentNullException">When <paramref name="path"/> is <code>null</code></exception>
        /// <exception cref="ArgumentNullException">When <paramref name="outputTemplate"/> is <code>null</code></exception>
        /// <exception cref="IOException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="PathTooLongException">When <paramref name="path"/> is too long</exception>
        /// <exception cref="UnauthorizedAccessException">The caller does not have the required permission to access the <paramref name="path"/></exception>
        /// <exception cref="ArgumentException">Invalid <paramref name="path"/></exception>
        public static LoggerConfiguration ZipFile(
            this LoggerAuditSinkConfiguration sinkConfiguration,
            string path,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            string outputTemplate = DefaultOutputTemplate,
            IFormatProvider? formatProvider = null,
            LoggingLevelSwitch? levelSwitch = null,
            Encoding? encoding = null)
        {
            if (sinkConfiguration == null) throw new ArgumentNullException(nameof(sinkConfiguration));
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (outputTemplate == null) throw new ArgumentNullException(nameof(outputTemplate));

            var formatter = new MessageTemplateTextFormatter(outputTemplate, formatProvider);
            return ZipFile(sinkConfiguration, formatter, path, restrictedToMinimumLevel, levelSwitch, encoding);
        }

        /// <summary>
        /// Write audit log events to the specified file.
        /// </summary>
        /// <param name="sinkConfiguration">Logger sink configuration.</param>
        /// <param name="formatter">A formatter, such as <see cref="JsonFormatter"/>, to convert the log events into
        /// text for the file. If control of regular text formatting is required, use the other
        /// overload of <see cref="ZipFile(LoggerAuditSinkConfiguration, string, LogEventLevel, string, IFormatProvider, LoggingLevelSwitch, Encoding)"/>
        /// and specify the outputTemplate parameter instead.
        /// </param>
        /// <param name="path">Path to the file.</param>
        /// <param name="restrictedToMinimumLevel">The minimum level for
        /// events passed through the sink. Ignored when <paramref name="levelSwitch"/> is specified.</param>
        /// <param name="levelSwitch">A switch allowing the pass-through minimum level
        /// to be changed at runtime.</param>
        /// <param name="encoding">Character encoding used to write the text file. The default is UTF-8 without BOM.</param>
        /// <returns>Configuration object allowing method chaining.</returns>
        /// <exception cref="ArgumentNullException">When <paramref name="sinkConfiguration"/> is <code>null</code></exception>
        /// <exception cref="ArgumentNullException">When <paramref name="formatter"/> is <code>null</code></exception>
        /// <exception cref="ArgumentNullException">When <paramref name="path"/> is <code>null</code></exception>
        /// <exception cref="IOException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="PathTooLongException">When <paramref name="path"/> is too long</exception>
        /// <exception cref="UnauthorizedAccessException">The caller does not have the required permission to access the <paramref name="path"/></exception>
        /// <exception cref="ArgumentException">Invalid <paramref name="path"/></exception>
        public static LoggerConfiguration ZipFile(
            this LoggerAuditSinkConfiguration sinkConfiguration,
            ITextFormatter formatter,
            string path,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            LoggingLevelSwitch? levelSwitch = null,
            Encoding? encoding = null)
        {
            if (sinkConfiguration == null) throw new ArgumentNullException(nameof(sinkConfiguration));
            if (formatter == null) throw new ArgumentNullException(nameof(formatter));
            if (path == null) throw new ArgumentNullException(nameof(path));

            return ConfigureFile(sinkConfiguration.Sink, formatter, path, restrictedToMinimumLevel, levelSwitch, true,
                null, encoding, RollingInterval.Infinite, null, null);
        }

        static LoggerConfiguration ConfigureFile(
            this Func<ILogEventSink, LogEventLevel, LoggingLevelSwitch?, LoggerConfiguration> addSink,
            ITextFormatter formatter,
            string path,
            LogEventLevel restrictedToMinimumLevel,
            LoggingLevelSwitch? levelSwitch,
            bool propagateExceptions,
            TimeSpan? flushToDiskInterval,
            Encoding? encoding,
            RollingInterval rollingInterval,
            int? retainedFileCountLimit,
            TimeSpan? retainedFileTimeLimit)
        {
            if (addSink == null) throw new ArgumentNullException(nameof(addSink));
            if (formatter == null) throw new ArgumentNullException(nameof(formatter));
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (retainedFileCountLimit.HasValue && retainedFileCountLimit < 1) throw new ArgumentException("At least one file must be retained.", nameof(retainedFileCountLimit));
            if (retainedFileTimeLimit.HasValue && retainedFileTimeLimit < TimeSpan.Zero) throw new ArgumentException("Negative value provided; retained file time limit must be non-negative.", nameof(retainedFileTimeLimit));

            ILogEventSink sink;

            try
            {
                if (rollingInterval != RollingInterval.Infinite)
                {
                    sink = new RollingZipFileSink(path, formatter, retainedFileCountLimit, encoding, rollingInterval, retainedFileTimeLimit);
                }
                else
                {
                    sink = new ZipFileSink(path, formatter, encoding);
                }
            }
            catch (Exception ex)
            {
                SelfLog.WriteLine("Unable to open file sink for {0}: {1}", path, ex);

                if (propagateExceptions)
                    throw;

                return addSink(new NullSink(), LevelAlias.Maximum, null);
            }

            if (flushToDiskInterval.HasValue)
            {
#pragma warning disable 618
                sink = new PeriodicFlushToDiskSink(sink, flushToDiskInterval.Value);
#pragma warning restore 618
            }

            return addSink(sink, restrictedToMinimumLevel, levelSwitch).Destructure.With<FileDestructure>();
        }
    }
}
