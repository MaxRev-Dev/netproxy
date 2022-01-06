using NetProxy.Configuration.Routes;
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
            var actual = Utils.ResloveDns(source);

            Assert.Equal( expected, actual);
        }

        public static IEnumerable<object[]> Hosts =>
            new[]
            {
                new object[]{"127.0.0.1:3000", new IPEndPoint(IPAddress.Parse("127.0.0.1"), 3000) },
                new object[]{"localhost:80", new IPEndPoint(IPAddress.Parse("[::1]"), 80) },
            };
    }
}