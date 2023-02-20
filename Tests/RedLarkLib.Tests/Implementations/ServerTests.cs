namespace RedLarkLib.Tests.Implementations;

using RedLarkLib.Implementation;
using Xunit;

public class ServerTests
{
    [Fact]
    public async Task ServerTest()
    {
        await using (Server server = new Server(string.Empty))
        {
            Assert.NotNull(server);
        }
    }
}
