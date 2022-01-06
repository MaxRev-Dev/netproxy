using Xunit;

namespace NetProxy.Tests
{
    public class UtilsTest
    { 
        [Fact]
        public void DeviceId_parser_works()
        {
            var ids = new[] { 30402, 23242, 354345 };

            var result = new RouteMapping().FromRawInput(string.Join(',', ids));
            foreach (var id in ids)
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