// Copyright 2013-2016 Serilog Contributors
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
using System.IO.Compression;
using System.Text;
using Serilog.Events;
using Serilog.Formatting;
using System.Linq;
using Serilog.Parsing;

namespace Serilog.Sinks.ZipFile
{
    /// <summary>
    /// Write log events to a disk file.
    /// </summary>
    public sealed class ZipFileSink : IZipFileSink, IDisposable
    {
        const string FilenameTimestampFormat = "yyyy-MM-dd HH_mm_ss.fff zzz";
        TextWriter _output;
        readonly ITextFormatter _textFormatter;
        readonly object _syncRoot = new object();
        ZipArchive _archive;
        ZipArchiveEntry _logEntry;
        readonly Encoding _encoding;
        private readonly string _logArchivePath;

        /// <summary>Construct a <see cref="ZipFileSink"/>.</summary>
        /// <param name="path">Path to the file.</param>
        /// <param name="textFormatter">Formatter used to convert log events to text.</param>
        
        /// <param name="encoding">Character encoding used to write the text file. The default is UTF-8 without BOM.</param>
        /// <returns>Configuration object allowing method chaining.</returns>
        /// <remarks>This constructor preserves compatibility with early versions of the public API. New code should not depend on this type.</remarks>
        /// <exception cref="ArgumentNullException">When <paramref name="textFormatter"/> is <code>null</code></exception>
        /// <exception cref="ArgumentNullException">When <paramref name="path"/> is <code>null</code></exception>
        /// <exception cref="IOException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="PathTooLongException">When <paramref name="path"/> is too long</exception>
        /// <exception cref="UnauthorizedAccessException">The caller does not have the required permission to access the <paramref name="path"/></exception>
        /// <exception cref="ArgumentException">Invalid <paramref name="path"/></exception>
        public ZipFileSink(
            string path,
            ITextFormatter textFormatter,
            Encoding? encoding)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            _textFormatter = textFormatter ?? throw new ArgumentNullException(nameof(textFormatter));
            _logArchivePath = path;

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var logFileName = Path.GetFileNameWithoutExtension(path) + ".log";

            _archive = openLogArchive(_logArchivePath);
            _logEntry = getLogEntry(_archive, logFileName);

            Stream outputStream = _logEntry.Open();

            outputStream.Seek(0, SeekOrigin.End);

            _encoding = encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            _output = new StreamWriter(outputStream, _encoding);
        }

        bool IZipFileSink.EmitOrOverflow(LogEvent logEvent)
        {
            if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));

            lock (_syncRoot)
            {
                if (FileDestructure.TryGetLogFile(logEvent, out var fileName, out var compression, out var fileData))
                {
                    var entryName = $"{logEvent.Timestamp.ToString(FilenameTimestampFormat)} - {fileName}".Replace(':', '_');

                    var template = new MessageTemplate(new MessageTemplateToken[] { new TextToken($"File Added: \"{entryName}\"") });
                    var newEvent = new LogEvent(logEvent.Timestamp, logEvent.Level, logEvent.Exception, template, logEvent.Properties.Where(p => p.Key == "Caller").Select(p => new LogEventProperty(p.Key, p.Value)));

                    _textFormatter.Format(newEvent, _output);
                    var entry = _archive.CreateEntry(entryName, compression);
                    using var stream = entry.Open();
                    stream.Write(fileData);
                }
                else
                {
                    _textFormatter.Format(logEvent, _output);
                }

                flushZipFile();
                return true;
            }
        }

        private void flushZipFile()
        {
            //Zip files can't be flushed. They must be closed and re-opened for changes to be written.
            _output.Dispose();
            _archive.Dispose();

            _archive = openLogArchive(_logArchivePath);
            _logEntry = getLogEntry(_archive, _logEntry.Name);

            var outputStream = _logEntry.Open();
            outputStream.Seek(0, SeekOrigin.End);
            _output = new StreamWriter(outputStream, _encoding);
        }

        private static ZipArchive openLogArchive(string logArchivePath)
        {
            var underlyingStream = File.Open(logArchivePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            return new ZipArchive(underlyingStream, ZipArchiveMode.Update);
        }

        private static ZipArchiveEntry getLogEntry(ZipArchive logArchive, string name)
            => logArchive.Entries.FirstOrDefault(e => e.Name == name)
            ?? logArchive.CreateEntry(name, CompressionLevel.NoCompression);

        /// <summary>
        /// Emit the provided log event to the sink.
        /// </summary>
        /// <param name="logEvent">The log event to write.</param>
        /// <exception cref="ArgumentNullException">When <paramref name="logEvent"/> is <code>null</code></exception>
        public void Emit(LogEvent logEvent)
        {
            ((IZipFileSink) this).EmitOrOverflow(logEvent);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            lock (_syncRoot)
            {
                _output.Dispose();
                _archive.Dispose();
            }
        }

        /// <inheritdoc />
        public void FlushToDisk()
        {
            lock (_syncRoot)
            {
                flushZipFile();
            }
        }
    }
}
