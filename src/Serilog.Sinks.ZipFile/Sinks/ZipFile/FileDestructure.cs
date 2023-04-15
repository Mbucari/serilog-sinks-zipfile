using Serilog.Core;
using Serilog.Events;
using System;
using System.IO.Compression;
using System.Linq;
using System.Reflection;

namespace Serilog.Sinks.ZipFile
{
    internal class FileDestructure : IDestructuringPolicy
    {
        private const string FILE_MAGIC_STRING = "73d675a1-8a47-4a41-9915-9a3767162c74";
        public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory, out LogEventPropertyValue result)
        {
            //Files are detected by pattern matching. If the logged type has properties named 'filename' and 'filedata' (case insensative)
            //with types string and byte[] respectively, the type is destructured into a format that ZipFileSink can parse.
            //
            //The CompressionLevel can be controlled by a thrid, optional property of type System.IO.Compression.CompressionLevel

            var properties = value.GetType().GetProperties();

            if (properties.Length >= 2
                && properties.FirstOrDefault(p => string.Compare(p.Name, "filename", true) == 0) is PropertyInfo filenameProperty && filenameProperty.PropertyType == typeof(string)
                && properties.FirstOrDefault(p => string.Compare(p.Name, "filedata", true) == 0) is PropertyInfo fileDataProperty && fileDataProperty.PropertyType == typeof(byte[]))
            {

                var filename = filenameProperty.GetValue(value) as string;
                var filedata = fileDataProperty.GetValue(value) as byte[];

                if (filename != null && filedata != null && filedata.Length > 0)
                {
                    var compressionProperty = properties.FirstOrDefault(f => f.PropertyType == typeof(CompressionLevel));
                    var compression = compressionProperty?.GetValue(value) is CompressionLevel c ? c : CompressionLevel.Fastest;

                    result = propertyValueFactory.CreatePropertyValue($"{FILE_MAGIC_STRING}{filename.Length:D8}{filename}{(int)compression:D8}{Convert.ToBase64String(filedata)}");
                    return true;
                }
            }


            result = propertyValueFactory.CreatePropertyValue(value);
            return false;
        }

        public static bool TryGetLogFile(LogEvent logEvent, out string? fileName, out CompressionLevel compression, out byte[]? fileData)
        {
            var candidate
                = logEvent.Properties.Values
                .OfType<ScalarValue>()
                .Select(v => v.Value)
                .OfType<string>()
                .FirstOrDefault(v => v.StartsWith(FILE_MAGIC_STRING) && v.Length > FILE_MAGIC_STRING.Length + 8);

            if (candidate != null && int.TryParse(candidate.AsSpan(FILE_MAGIC_STRING.Length, 8), out var length) && candidate.Length > length + 8 + FILE_MAGIC_STRING.Length)
            {
                fileName = candidate.Substring(FILE_MAGIC_STRING.Length + 8, length);

                if (Enum.TryParse(candidate.AsSpan(FILE_MAGIC_STRING.Length + 8 + fileName.Length, 8), out compression))
                {
                    var dataLength = candidate.Length - FILE_MAGIC_STRING.Length + 8 + fileName.Length + 8;

                    Span<byte> buffer = new byte[dataLength * 6 / 8 + 1];

                    if (Convert.TryFromBase64String(candidate.Substring(FILE_MAGIC_STRING.Length + 8 + fileName.Length), buffer, out var bytesWritten))
                    {
                        fileData = buffer[..bytesWritten].ToArray();
                        return true;
                    }
                }
            }

            fileName = null;
            compression = CompressionLevel.NoCompression;
            fileData = null;
            return false;
        }
    }
}
