
namespace RedLarkLib.Tests.Utilities;

using RedLarkLib.Utilities;
using Xunit;

public class TaskExtensionsTests
{
    public class ThrowsTestException : Exception { }

    #region TaskExtensions.ForEachExecAndWait<TInput, TOutput>

    [Fact]
    public async Task ForEachExecAndWaitTest()
    {
        var array = new[] { 1, 2, 3 };
        var results = await array.ForEachExecAndWait(async i => { await Task.Yield(); return i; }, false).ConfigureAwait(false);
        var inputArray = results.Select(r => r.Key).OrderBy(k => k);
        Assert.Equal(array, inputArray);
        var resultArray = results.Select(r => r.Value.Result).OrderBy(r => r);
        Assert.Equal(array, resultArray);
    }

    [Fact]
    public async Task ForEachExecAndWaitThrowsFalseTest()
    {
        var array = new[] { 1, 2, 3 };
        var results = await array.ForEachExecAndWait(async i => { await Task.Yield(); if (i % 2 == 0) throw new Exception(); return i; }, false).ConfigureAwait(false);
        var resultsSorted = results.OrderBy(r => r.Key);
        var inputArray = resultsSorted.Select(r => r.Key);
        Assert.Equal(array, inputArray);
        var resultArray = resultsSorted.Select(r => r.Value).ToArray();
        Assert.Equal(array[0], resultArray[0].Result);
        Assert.True(resultArray[1].IsFaulted);
        Assert.Equal(array[2], resultArray[2].Result);
    }

    [Fact]
    public async Task ForEachExecAndWaitThrowsTrueTest()
    {
        var array = new[] { 1, 2, 3 };
        await Assert.ThrowsAsync<ThrowsTestException>(async () =>
        {
            await array.ForEachExecAndWait(async i => { await Task.Yield(); if (i % 2 == 0) throw new ThrowsTestException(); return i; }, true).ConfigureAwait(false);
        });
    }

    #endregion

    #region TaskExtensions.ForEachExecAndWait<TInput>

    [Fact]
    public async Task ForEachExecAndWaitNoOutputTest()
    {
        var array = new[] { 1, 2, 3 };
        var results = await array.ForEachExecAndWait(async i => { await Task.Yield(); return; }, false).ConfigureAwait(false);
        var inputArray = results.Select(r => r.Key).OrderBy(k => k);
        Assert.Equal(array, inputArray);
    }

    [Fact]
    public async Task ForEachExecAndWaitNoOutputThrowsFalseTest()
    {
        var array = new[] { 1, 2, 3 };
        var results = await array.ForEachExecAndWait(async i => { await Task.Yield(); if (i % 2 == 0) throw new Exception(); return; }, false).ConfigureAwait(false);
        var resultsSorted = results.OrderBy(r => r.Key);
        var inputArray = resultsSorted.Select(r => r.Key);
        Assert.Equal(array, inputArray);
        var resultArray = resultsSorted.Select(r => r.Value).ToArray();
        Assert.True(resultArray[0].IsCompletedSuccessfully);
        Assert.True(resultArray[1].IsFaulted);
        Assert.True(resultArray[2].IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ForEachExecAndWaitNoOutputThrowsTrueTest()
    {
        var array = new[] { 1, 2, 3 };
        await Assert.ThrowsAsync<ThrowsTestException>(async () =>
        {
            await array.ForEachExecAndWait(async i => { await Task.Yield(); if (i % 2 == 0) throw new ThrowsTestException(); return; }, true).ConfigureAwait(false);
        });
    }

    #endregion

    #region TaskExtensions.ForEachAsyncDispose<TInput>

    public class ForEachAsyncDisposable : IAsyncDisposable
    {
        private readonly bool m_throw;
        public bool IsDisposed { get; private set; } = false;
        public int Id { get; }

        public ForEachAsyncDisposable(int a_id, bool a_throw = false)
        {
            m_throw = a_throw;
            Id = a_id;    
        }

        async ValueTask IAsyncDisposable.DisposeAsync()
        {
            await Task.Yield();
            if (m_throw) throw new ThrowsTestException();
            IsDisposed = true;
        }
    }

    [Fact]
    public async Task ForEachAsyncDisposeTest()
    {
        var items = new[]
        {
            new ForEachAsyncDisposable(1),
            new ForEachAsyncDisposable(2),
            new ForEachAsyncDisposable(3)
        };

        var results = await items.ForEachAsyncDispose();
        var sortedResults = results.OrderBy(r => r.Key.Id).ToArray();
        Assert.Equal(1, sortedResults[0].Key.Id);
        Assert.Equal(2, sortedResults[1].Key.Id);
        Assert.Equal(3, sortedResults[2].Key.Id);
        Assert.True(sortedResults[0].Value.IsCompletedSuccessfully);
        Assert.True(sortedResults[1].Value.IsCompletedSuccessfully);
        Assert.True(sortedResults[2].Value.IsCompletedSuccessfully);
        Assert.True(sortedResults[0].Key.IsDisposed);
        Assert.True(sortedResults[1].Key.IsDisposed);
        Assert.True(sortedResults[2].Key.IsDisposed);
    }


    [Fact]
    public async Task ForEachAsyncDisposeThrowsFalseTest()
    {
        var items = new[]
        {
            new ForEachAsyncDisposable(1),
            new ForEachAsyncDisposable(2, true),
            new ForEachAsyncDisposable(3)
        };

        var results = await items.ForEachAsyncDispose(false).ConfigureAwait(false);
        var sortedResults = results.OrderBy(r => r.Key.Id).ToArray();
        Assert.Equal(1, sortedResults[0].Key.Id);
        Assert.Equal(2, sortedResults[1].Key.Id);
        Assert.Equal(3, sortedResults[2].Key.Id);
        Assert.True(sortedResults[0].Value.IsCompletedSuccessfully);
        Assert.True(sortedResults[1].Value.IsFaulted);
        Assert.True(sortedResults[2].Value.IsCompletedSuccessfully);
        Assert.True(sortedResults[0].Key.IsDisposed);
        Assert.False(sortedResults[1].Key.IsDisposed);
        Assert.True(sortedResults[2].Key.IsDisposed);

    }

    [Fact]
    public async Task ForEachAsyncDisposeThrowsTrueTest()
    {
        var items = new[]
        {
            new ForEachAsyncDisposable(1),
            new ForEachAsyncDisposable(2, true),
            new ForEachAsyncDisposable(3)
        };

        await Assert.ThrowsAsync<ThrowsTestException>(async () =>
        {
            await items.ForEachAsyncDispose(true).ConfigureAwait(false);
        });

    }

    #endregion

    #region TaskExtensions.ForEachEnumerateAsCompleted<TInput, TOutput>

    [Fact]
    public async Task ForEachEnumerateAsCompletedTest()
    {
        var array = new[] { 1, 2, 3 };
        int index = 0;
        await foreach (var result in array.ForEachEnumerateAsCompleted(async i => { await Task.Delay(i * 100).ConfigureAwait(false); return i; }, false))
        {
            Assert.Equal(array[index], result.Key);
            Assert.True(result.Value.IsCompletedSuccessfully);
            Assert.Equal(array[index], result.Value.Result);
            index++;
        }
    }

    [Fact]
    public async Task ForEachEnumerateAsCompletedThrowsFalseTest()
    {
        var array = new[] { 1, 2, 3 };
        int index = 0;
        await foreach (var result in array.ForEachEnumerateAsCompleted(async i => { await Task.Delay(i * 100).ConfigureAwait(false); if (i % 2 == 0) throw new ThrowsTestException(); return i; }, false))
        {
            Assert.Equal(array[index], result.Key);
            if (array[index] % 2 == 0)
            {
                Assert.True(result.Value.IsFaulted);
            }
            else
            {
                Assert.True(result.Value.IsCompletedSuccessfully);
                Assert.Equal(array[index], result.Value.Result);
            }
            index++;
        }
        Assert.Equal(3, index);

    }

    [Fact]
    public async Task ForEachEnumerateAsCompletedThrowsTrueTest()
    {
        var array = new[] { 1, 2, 3 };
        int index = 0;

        await Assert.ThrowsAsync<AggregateException>(async () => {
            await foreach (var result in array.ForEachEnumerateAsCompleted(async i => { await Task.Delay(i * 100).ConfigureAwait(false); if (i % 2 == 0) throw new ThrowsTestException(); return i; }, true))
            {
                Assert.Equal(array[index], result.Key);
                if (array[index] % 2 == 0)
                {
                    Assert.True(result.Value.IsFaulted);
                }
                else
                {
                    Assert.True(result.Value.IsCompletedSuccessfully);
                    Assert.Equal(array[index], result.Value.Result);
                }
                index++;
            }
        });
    }


    #endregion
}
