using NetProxy.Parser;
using System;
using System.Runtime.InteropServices;

namespace NetProxy.Tests
{
    public partial class ClientHandlingTests
    {
        public static class TestHelpers
        {
            public static void CopyIdToBuffer(uint deviceId, byte[] buffer)
            {
                var sliceOfId = buffer.AsSpan()
                    .Slice(DeviceIdParser.HeaderStart, DeviceIdParser.DeviceIdSize);
                MemoryMarshal.Write(sliceOfId, ref deviceId);
            }
        }
    }
}