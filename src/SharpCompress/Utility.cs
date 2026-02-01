using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace SharpCompress
{
    internal static partial class Utility
    {
        // 80kb is a good industry standard temporary buffer size
        internal const int TEMP_BUFFER_SIZE = 81920;
        private static readonly HashSet<char> invalidChars = new HashSet<char>(Path.GetInvalidFileNameChars());

        public static ReadOnlyCollection<T> ToReadOnly<T>(this IList<T> items) => new ReadOnlyCollection<T>(items);

        public static int URShift(int number, int bits) => (int)((uint)number >> bits);

        public static long URShift(long number, int bits) => (long)((ulong)number >> bits);

        public static void SetSize(this List<byte> list, int count)
        {
            if (count > list.Count)
            {
                list.Capacity = count;
                for (var i = list.Count; i < count; i++)
                {
                    list.Add(0x0);
                }
            }
            else
            {
                list.RemoveRange(count, list.Count - count);
            }
        }

        public static void ForEach<T>(this IEnumerable<T> items, Action<T> action)
        {
            foreach (var item in items)
            {
                action(item);
            }
        }

        public static IEnumerable<T> AsEnumerable<T>(this T item)
        {
            yield return item;
        }

        public static DateTime DosDateToDateTime(ushort iDate, ushort iTime)
        {
            var year = (iDate / 512) + 1980;
            var month = iDate % 512 / 32;
            var day = iDate % 512 % 32;
            var hour = iTime / 2048;
            var minute = iTime % 2048 / 32;
            var second = iTime % 2048 % 32 * 2;

            if (iDate == ushort.MaxValue || month == 0 || day == 0)
            {
                year = 1980;
                month = 1;
                day = 1;
            }

            if (iTime == ushort.MaxValue)
            {
                hour = minute = second = 0;
            }

            DateTime dt;
            try
            {
                dt = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Local);
            }
            catch
            {
                dt = new DateTime();
            }
            return dt;
        }

        public static uint DateTimeToDosTime(this DateTime? dateTime)
        {
            if (dateTime == null)
            {
                return 0;
            }

            var localDateTime = dateTime.Value.ToLocalTime();

            return (uint)(
                (localDateTime.Second / 2)
                | (localDateTime.Minute << 5)
                | (localDateTime.Hour << 11)
                | (localDateTime.Day << 16)
                | (localDateTime.Month << 21)
                | ((localDateTime.Year - 1980) << 25)
            );
        }

        public static DateTime DosDateToDateTime(uint iTime) =>
            DosDateToDateTime((ushort)(iTime / 65536), (ushort)(iTime % 65536));

        public static DateTime UnixTimeToDateTime(long unixtime)
        {
            var sTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return sTime.AddSeconds(unixtime);
        }


        public static long TransferTo(this Stream source, Stream destination, long maxLength)
        {

            using (var limitedStream = new SharpCompress.IO.ReadOnlySubStream(source, maxLength))
            {
                limitedStream.CopyTo(destination, TEMP_BUFFER_SIZE);
                return limitedStream.Position;
            }
        }

        public static async Task<long> TransferToAsync(this Stream source, Stream destination, long maxLength, CancellationToken cancellationToken = default(CancellationToken))
        {
            using (var limitedStream = new SharpCompress.IO.ReadOnlySubStream(source, maxLength))
            {
                await limitedStream.CopyToAsync(destination, TEMP_BUFFER_SIZE, cancellationToken).ConfigureAwait(false);
                return limitedStream.Position;
            }
        }

        public static async Task SkipAsync(this Stream source, long advanceAmount, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (source.CanSeek)
            {
                source.Position += advanceAmount;
                return;
            }


            var buffer = new byte[TEMP_BUFFER_SIZE]; 
            
            while (advanceAmount > 0)
            {
                var toRead = (int)Math.Min(buffer.Length, advanceAmount);
                var read = await source.ReadAsync(buffer, 0, toRead, cancellationToken).ConfigureAwait(false);
                if (read <= 0)
                {
                    break;
                }
                advanceAmount -= read;
            }
        }

        public static bool ReadFully(this Stream source, byte[] buffer)
        {
            var total = 0;
            int read;
            while ((read = source.Read(buffer, total, buffer.Length - total)) > 0)
            {
                total += read;
                if (total >= buffer.Length)
                {
                    return true;
                }
            }
            return (total >= buffer.Length);
        }

        public static async Task<bool> ReadFullyAsync(this Stream source, byte[] buffer, CancellationToken cancellationToken = default(CancellationToken))
        {
            var total = 0;
            int read;
            while (
                (
                    read = await source
                        .ReadAsync(buffer, total, buffer.Length - total, cancellationToken)
                        .ConfigureAwait(false)
                ) > 0
            )
            {
                total += read;
                if (total >= buffer.Length)
                {
                    return true;
                }
            }
            return (total >= buffer.Length);
        }

        public static async Task<bool> ReadFullyAsync(this Stream source, byte[] buffer, int offset, int count, CancellationToken cancellationToken = default(CancellationToken))
        {
            var total = 0;
            int read;
            while (
                (
                    read = await source
                        .ReadAsync(buffer, offset + total, count - total, cancellationToken)
                        .ConfigureAwait(false)
                ) > 0
            )
            {
                total += read;
                if (total >= count)
                {
                    return true;
                }
            }
            return (total >= count);
        }

        public static void ReadExact(this Stream stream, byte[] buffer, int offset, int length)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            if (length < 0 || length > buffer.Length - offset) throw new ArgumentOutOfRangeException(nameof(length));

            while (length > 0)
            {
                var fetched = stream.Read(buffer, offset, length);
                if (fetched <= 0)
                {
                    throw new EndOfStreamException();
                }

                offset += fetched;
                length -= fetched;
            }
        }

        public static string TrimNulls(this string source) => source.Replace('\0', ' ').Trim();

        public static uint SwapUINT32(uint number) =>
            (number >> 24)
            | ((number << 8) & 0x00FF0000)
            | ((number >> 8) & 0x0000FF00)
            | (number << 24);

        public static void SetLittleUInt32(ref byte[] buffer, uint number, long offset)
        {
            buffer[offset] = (byte)(number);
            buffer[offset + 1] = (byte)(number >> 8);
            buffer[offset + 2] = (byte)(number >> 16);
            buffer[offset + 3] = (byte)(number >> 24);
        }

        public static void SetBigUInt32(ref byte[] buffer, uint number, long offset)
        {
            buffer[offset] = (byte)(number >> 24);
            buffer[offset + 1] = (byte)(number >> 16);
            buffer[offset + 2] = (byte)(number >> 8);
            buffer[offset + 3] = (byte)number;
        }

        public static string ReplaceInvalidFileNameChars(string fileName)
        {
            var sb = new StringBuilder(fileName.Length);
            foreach (var c in fileName)
            {
                var newChar = invalidChars.Contains(c) ? '_' : c;
                sb.Append(newChar);
            }
            return sb.ToString();
        }
    }
}
