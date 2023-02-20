namespace RedLarkLib.Tests.Implementations;

using Moq;
using RedLarkLib.Implementation;
using RedLarkLib.Internal;
using RedLarkLib.Testing;
using Xunit;

public class LockTests
{
    [Fact]
    public async Task Test()
    {
        var redlarkMock = new Mock<IRedLarkInternal>();
        var aborted = false;
        var lockObj = (ILockInternal)new Lock(redlarkMock.Object, 1, 200, "unit_test", "UnitTestUniqueValue0", 200, 0, l => { aborted = false; } );
        var lockTest = (ILockTesting)lockObj;

        lockObj.Initialize();

        Assert.Null(lockTest.RenewTimer);
        Assert.True(lockTest.IsLocked);
        Assert.False(aborted);

        await lockObj.Unlock();

        Assert.Null(lockTest.RenewTimer);
        Assert.False(aborted);
        Assert.False(lockTest.IsLocked);

        redlarkMock.Verify(x => x.Unlock(lockObj), Times.Once);
        redlarkMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RenewOkTest()
    {
        var redlarkMock = new Mock<IRedLarkInternal>();
        var aborted = false;
        var lockObj = (ILockInternal)new Lock(redlarkMock.Object, 1, 200, "unit_test", "UnitTestUniqueValue0", 200, 1, l => { aborted = true; });
        var lockTest = (ILockTesting)lockObj;

        redlarkMock.Setup(x => x.Renew(lockObj, lockObj.Ttl).Result).Returns(160);

        lockObj.Initialize();

        Assert.NotNull(lockTest.RenewTimer);
        Assert.True(lockTest.IsLocked);
        Assert.False(aborted);

        await Task.Delay(200);

        Assert.NotNull(lockTest.RenewTimer);
        Assert.True(lockTest.IsLocked);
        Assert.False(aborted);

        await lockObj.Unlock();

        Assert.Null(lockTest.RenewTimer);
        Assert.False(lockTest.IsLocked);
        Assert.False(aborted);

        redlarkMock.Verify(x => x.Unlock(lockObj), Times.Once);
        redlarkMock.Verify(x => x.Renew(lockObj, lockObj.Ttl), Times.Once);
        redlarkMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RenewFailTest()
    {
        var redlarkMock = new Mock<IRedLarkInternal>();
        var aborted = false;
        var lockObj = (ILockInternal)new Lock(redlarkMock.Object, 1, 200, "unit_test", "UnitTestUniqueValue0", 200, 1, l => { aborted = true; });
        var lockTest = (ILockTesting)lockObj;

        redlarkMock.Setup(x => x.Renew(lockObj, lockObj.Ttl).Result).Returns(0);

        lockObj.Initialize();

        Assert.NotNull(lockTest.RenewTimer);
        Assert.True(lockTest.IsLocked);
        Assert.False(aborted);

        await Task.Delay(200);

        Assert.Null(lockTest.RenewTimer);
        Assert.False(lockTest.IsLocked);
        Assert.True(aborted);

        redlarkMock.Verify(x => x.Unlock(lockObj), Times.Once);
        redlarkMock.Verify(x => x.Renew(lockObj, lockObj.Ttl), Times.Once);
        redlarkMock.VerifyNoOtherCalls();
    }
}
