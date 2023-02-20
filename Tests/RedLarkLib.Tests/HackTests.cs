namespace RedLarkLib.Tests
{
    public class HackTests
    {
        //[Fact]
        public async Task LockTest()
        {
            var factory = (IRedLarkFactory)new DefaultRedLarkFactory();
            await using (var redlark = factory.New(
                                            new[]
                                            {
                                                "localhost:63791",
                                                "localhost:63792",
                                                "localhost:63793"
                                            }))
            {
                await redlark.Connect();

                await using (var lockObj = await redlark.Lock("MyResource", 30000))
                {
                    await Task.Delay(10000);
                }
            }
        }
    }
}