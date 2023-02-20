namespace RedLarkLib.Tests.Implementations;

using Moq;
using RedLarkLib.Exceptions;
using RedLarkLib.Implementation;
using RedLarkLib.Internal;
using RedLarkLib.Testing;
using Xunit;
using Xunit.Abstractions;

public class RedLarkTests
{
    public class ServerFactoryConfig
    {
        public IEnumerable<bool> Connect {  get; }    
        public IEnumerable<bool> Lock { get; }

        public ServerFactoryConfig(IEnumerable<bool> connects, IEnumerable<bool> locks)
        {
            Connect = connects;
            Lock = locks;
        }
    }

    public class ServerFactoryForRedLarkTests : IServerFactoryInternal
    {
        private readonly ServerFactoryConfig[] m_configs;
        private int m_configIndex = 0;
        public MockRepository MockRepository { get; } = new MockRepository(MockBehavior.Loose);
    
        public ServerFactoryForRedLarkTests(params ServerFactoryConfig[] configs)
        {
            m_configs = configs;
        }

        public List<Mock<IServerInternal>> Servers { get; } = new List<Mock<IServerInternal>>();

        IServerInternal IServerFactoryInternal.New(string a_connectionInfo)
        {
            var serverMock = MockRepository.Create<IServerInternal>();
            var config = m_configs[m_configIndex];
            m_configIndex = (m_configIndex + 1) % m_configs.Length;

            var connectHandle = serverMock.SetupSequence(x => x.Connect().Result);
            foreach (var response in config.Connect)
                connectHandle = connectHandle.Returns(response);

            var lockHandle = serverMock.SetupSequence(x => x.Lock(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()).Result);
            foreach (var response in config.Lock)
                lockHandle = lockHandle.Returns(response);

            Servers.Add(serverMock);
            return serverMock.Object;
        }
    }

    private readonly ITestOutputHelper m_output;

    public RedLarkTests(ITestOutputHelper a_output)
    {
        this.m_output = a_output;
    }

    #region Connect Tests

    [Fact]
    public async Task ConnectTest()
    {
        var serverFactory = new ServerFactoryForRedLarkTests(
            new ServerFactoryConfig(
                connects: new[] { true },
                locks: new[] { true }
                ),
            new ServerFactoryConfig(
                connects: new[] { true },
                locks: new[] { true }
                ),
            new ServerFactoryConfig(
                connects: new[] { true },
                locks: new[] { true }
                )
            );
        await using (var redlark = (IRedLark)new RedLark(serverFactory, new DefaultLockFactoryInternal(), new[] { "a", "b", "c" }, 3, 100, 100))
        {
            await redlark.Connect();

            var redlarkTest = (IRedLarkTesting)redlark;
            Assert.Equal(3, redlarkTest.ConnectServerCount);
        }

        Assert.Equal(3, serverFactory.Servers.Count);
        foreach (var mock in serverFactory.Servers)
        {
            mock.Verify(x => x.Connect().Result, Times.Once);
            mock.Verify(x => x.DisposeAsync(), Times.Once);
        }
    }

    [Fact]
    public async Task ConnectOneFailTest()
    {
        var serverFactory = new ServerFactoryForRedLarkTests(
            new ServerFactoryConfig(
                connects: new[] { false },
                locks: new[] { true }
                ),
            new ServerFactoryConfig(
                connects: new[] { true },
                locks: new[] { true }
                ),
            new ServerFactoryConfig(
                connects: new[] { true },
                locks: new[] { true }
                )
            );
        await using (var redlark = (IRedLark)new RedLark(serverFactory, new DefaultLockFactoryInternal(), new[] { "a", "b", "c" }, 3, 100, 100))
        {
            await redlark.Connect();

            var redlarkTest = (IRedLarkTesting)redlark;
            Assert.Equal(2, redlarkTest.ConnectServerCount);
        }

        Assert.Equal(3, serverFactory.Servers.Count);
        foreach (var mock in serverFactory.Servers)
        {
            mock.Verify(x => x.Connect().Result, Times.Once);
            mock.Verify(x => x.DisposeAsync(), Times.Once);
        }
    }

    [Fact]
    public async Task ConnectQuorumFailsTest()
    {
        var serverFactory = new ServerFactoryForRedLarkTests(
            new ServerFactoryConfig(
                connects: new[] { false },
                locks: new[] { true }
                ),
            new ServerFactoryConfig(
                connects: new[] { false },
                locks: new[] { true }
                ),
            new ServerFactoryConfig(
                connects: new[] { true },
                locks: new[] { true }
                )
            );

        await Assert.ThrowsAsync<CannotObtainLockException>(async () =>
        {
            await using (var redlark = (IRedLark)new RedLark(serverFactory, new DefaultLockFactoryInternal(), new[] { "a", "b", "c" }, 3, 100, 100))
            {
                await redlark.Connect();
            }
        });

        foreach (var mock in serverFactory.Servers)
        {
            mock.Verify(x => x.Connect().Result, Times.Once);
            mock.Verify(x => x.DisposeAsync(), Times.AtLeastOnce);
        }
    }

    #endregion

    #region Lock Tests

    [Fact]
    public async Task LockTest()
    {
        var serverFactory = new ServerFactoryForRedLarkTests(
            new ServerFactoryConfig(
                new[] { true },
                new[] { true }
                ),
            new ServerFactoryConfig(
                new[] { true },
                new[] { true }
                ),
            new ServerFactoryConfig(
                new[] { true },
                new[] { true }
                )
            );
        await using (var redlark = (IRedLark)new RedLark(serverFactory, new DefaultLockFactoryInternal(), new[] { "a", "b", "c" }, 3, 100, 100, a_shortcircuitOnQuorum: false))
        {
            await redlark.Connect();
            await using (var lockObj = await redlark.Lock("resource", 200))
            {
                Assert.NotNull(lockObj);

                var lockTest = lockObj as ILockTesting;
                if (lockTest != null)
                {
                    Assert.Equal(3, lockTest.ServerLockCount);
                }
            }
        }

        Assert.Equal(3, serverFactory.Servers.Count);
        foreach (var mock in serverFactory.Servers)
        {
            mock.Verify(x => x.Connect().Result, Times.Once);
            mock.Verify(x => x.DisposeAsync(), Times.Once);
            mock.Verify(x => x.Lock(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()).Result, Times.Once);
            mock.Verify(x => x.Unlock(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }
    }


    [Fact]
    public async Task LockOneFailTest()
    {
        var serverFactory = new ServerFactoryForRedLarkTests(
            new ServerFactoryConfig(
                new[] { true },
                new[] { false }
                ),
            new ServerFactoryConfig(
                new[] { true },
                new[] { true }
                ),
            new ServerFactoryConfig(
                new[] { true },
                new[] { true }
                )
            );
        await using (var redlark = (IRedLark)new RedLark(serverFactory, new DefaultLockFactoryInternal(), new[] { "a", "b", "c" }, 3, 100, 100, a_shortcircuitOnQuorum: false))
        {
            await redlark.Connect();
            await using (var lockObj = await redlark.Lock("resource", 200))
            {
                Assert.NotNull(lockObj);

                var lockTest = lockObj as ILockTesting;
                if (lockTest != null)
                {
                    Assert.Equal(2, lockTest.ServerLockCount);
                }
            }
        }

        Assert.Equal(3, serverFactory.Servers.Count);
        foreach (var mock in serverFactory.Servers)
        {
            mock.Verify(x => x.Connect().Result, Times.Once);
            mock.Verify(x => x.DisposeAsync(), Times.Once);
            mock.Verify(x => x.Lock(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()).Result, Times.Once);
            mock.Verify(x => x.Unlock(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }
    }

    [Fact]
    public async Task LockQuorumFailsTest()
    {
        var serverFactory = new ServerFactoryForRedLarkTests(
            new ServerFactoryConfig(
                new[] { true },
                new[] { false, false, false }
                ),
            new ServerFactoryConfig(
                new[] { true },
                new[] { false, false, false }
                ),
            new ServerFactoryConfig(
                new[] { true },
                new[] { true, true, true }
                )
            );
        await using (var redlark = (IRedLark)new RedLark(serverFactory, new DefaultLockFactoryInternal(), new[] { "a", "b", "c" }, 3, 100, 100, a_shortcircuitOnQuorum: false))
        {
            await redlark.Connect();
            await using (var lockObj = await redlark.Lock("resource", 200))
            {
                Assert.Null(lockObj);
            }
        }

        Assert.Equal(3, serverFactory.Servers.Count);
        foreach (var mock in serverFactory.Servers)
        {
            mock.Verify(x => x.Connect().Result, Times.Once);
            mock.Verify(x => x.DisposeAsync(), Times.Once);
            mock.Verify(x => x.Lock(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()).Result, Times.Exactly(3));
            mock.Verify(x => x.Unlock(It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(3));
        }
    }

    [Fact]
    public async Task LockQuorumFailsButRecoversTest()
    {
        var serverFactory = new ServerFactoryForRedLarkTests(
            new ServerFactoryConfig(
                new[] { true },
                new[] { false, false, false }
                ),
            new ServerFactoryConfig(
                new[] { true },
                new[] { false, false, true }
                ),
            new ServerFactoryConfig(
                new[] { true },
                new[] { true, true, true }
                )
            );
        await using (var redlark = (IRedLark)new RedLark(serverFactory, new DefaultLockFactoryInternal(), new[] { "a", "b", "c" }, 3, 100, 100, a_shortcircuitOnQuorum: false))
        {
            await redlark.Connect();
            await using (var lockObj = await redlark.Lock("resource", 200))
            {
                Assert.NotNull(lockObj);

                var lockTest = lockObj as ILockTesting;
                if (lockTest != null)
                {
                    Assert.Equal(2, lockTest.ServerLockCount);
                }
            }
        }

        Assert.Equal(3, serverFactory.Servers.Count);
        int lockCount = 0;
        int unlockCount = 0;
        foreach (var mock in serverFactory.Servers)
        {
            lockCount += mock.Invocations.Where(i => i.Method.Name == "Lock").Count();
            unlockCount += mock.Invocations.Where(i => i.Method.Name == "Unlock").Count();
            mock.Verify(x => x.Connect().Result, Times.Once);
            mock.Verify(x => x.DisposeAsync(), Times.Once);
            mock.Verify(x => x.Lock(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()).Result, Times.Exactly(3));
            mock.Verify(x => x.Unlock(It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(3));
        }

        m_output.WriteLine($"lockCount: {lockCount}");
        m_output.WriteLine($"unlockCount: {unlockCount}");
    }

    #endregion

}