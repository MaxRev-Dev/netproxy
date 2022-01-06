using System.Collections.Generic;
using System.Net;
using Xunit;

namespace NetProxy.Tests
{
    public class UtilsTest
    {
        [Theory]
        [MemberData(nameof(Hosts))]
        public void TestDnsResolver(string source, IPEndPoint expected)
        {
            var actual = Utils.ResolveIpFromDns(source);

            Assert.Equal(expected, actual);
        }

        public static IEnumerable<object[]> Hosts =>
            new[]
            {
                new object[]{"127.0.0.1:3000", new IPEndPoint(IPAddress.Parse("127.0.0.1"), 3000) },
                new object[]{"localhost:80", new IPEndPoint(IPAddress.Parse("[::1]"), 80) },
            };


        [Fact]
        public void DeviceId_parser_works()
        {
            var ids = new[] { 30402, 23242, 354345 };

            var result = new RouteMapping().FromRawInput(string.Join(',', ids));
            foreach(var id in ids)
            {
                Assert.True(result.Contains((uint)id));
            }
            result = new RouteMapping().FromRawInput("1000-10000;23425");
            Assert.True(result.Contains(5000));
            Assert.False(result.Contains(53200));
            Assert.True(result.Contains(23425));
        }
    }
}