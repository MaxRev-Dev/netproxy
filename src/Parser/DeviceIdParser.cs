using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;

#nullable enable

namespace NetProxy.Parser
{
    internal struct DeviceIdRequestPartial
    {
        public DeviceIdRequestPartial(int received, uint? deviceId, byte[]? buffer)
        {
            DeviceId = deviceId;
            Buffer = buffer;
            Received = received;
        }

        public uint? DeviceId { get; }
        public byte[]? Buffer { get; }
        public int Received { get; }

        public bool IsValid() => Buffer != null && DeviceId.HasValue;
    }

    internal class DeviceIdParser
    {
        public const int HeaderStart = 3;
        public const int DeviceIdSize = 4;
        public const int HeaderSize = HeaderStart + DeviceIdSize;

        public static bool RetrivePacket(Socket socket, out DeviceIdRequestPartial result, int? bufferSize = default)
        {
            int length = bufferSize ?? HeaderSize;
            Span<byte> buffer = length <= 1024 ? stackalloc byte[length] : new byte[length];
            int received = 0;
            try
            {
                received = socket.Receive(buffer);
                var deviceId = MemoryMarshal.Read<uint>(buffer.Slice(HeaderStart, DeviceIdSize));
                result = new DeviceIdRequestPartial(received, deviceId, buffer.ToArray());
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                // format error in memory marshal
            }
            catch (ArgumentException)
            {
                // format error in memory marshal
            }
            catch (ObjectDisposedException)
            {
                // should be a closed connection
            }
            catch (SocketException)
            {
                // some problems with socket connection
            }
            result = new(received, null, null);
            return false;
        }
    }
}
