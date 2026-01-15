namespace RedLarkLib.Utilities;

/// <summary>
/// Provides extension methods for working with collections of tasks in parallel.
/// These extensions enable efficient parallel execution with various completion patterns.
/// </summary>
/// <remarks>
/// <para>
/// These extension methods are central to RedLark's performance optimizations:
/// </para>
/// <list type="bullet">
///   <item><description>
///     <b>Parallel Execution:</b> All tasks start immediately without waiting for previous completions.
///   </description></item>
///   <item><description>
///     <b>Early Return:</b> <see cref="ForEachEnumerateAsCompleted{TInput, TOutput}"/> enables
///     processing results as they arrive, supporting quorum-based short-circuiting.
///   </description></item>
///   <item><description>
///     <b>Error Isolation:</b> Failed tasks don't prevent other tasks from completing.
///   </description></item>
/// </list>
/// </remarks>
public static class TaskExtensions
{
    /// <summary>
    /// Executes an async function for each item in the collection and waits for all to complete.
    /// </summary>
    /// <typeparam name="TInput">The type of input items.</typeparam>
    /// <typeparam name="TOutput">The return type of the async function.</typeparam>
    /// <param name="a_list">The collection of input items.</param>
    /// <param name="a_func">The async function to execute for each item.</param>
    /// <param name="a_throwOnFault">If true, throws on first faulted task; otherwise captures all results.</param>
    /// <returns>A collection of key-value pairs mapping each input to its completed task.</returns>
    /// <remarks>
    /// <para>
    /// All tasks are started immediately (in parallel) and the method waits for all to complete.
    /// This is more efficient than sequential await when operations are independent.
    /// </para>
    /// <para>
    /// Example usage:
    /// </para>
    /// <code>
    /// var results = await servers.ForEachExecAndWait(s => s.Connect());
    /// var connected = results.Where(r => r.Value.IsCompletedSuccessfully &amp;&amp; r.Value.Result);
    /// </code>
    /// </remarks>
    public static async Task<IEnumerable<KeyValuePair<TInput,Task<TOutput>>>> ForEachExecAndWait<TInput, TOutput>(
        this IEnumerable<TInput> a_list,
        Func<TInput, Task<TOutput>> a_func,
        bool a_throwOnFault = false)
    where TInput: notnull
    {
        var tasks = new Dictionary<TInput,Task<TOutput>>();
        foreach (var item in a_list)
        {
            tasks.Add(item, a_func(item));
        }

        try
        {
            await Task.WhenAll(tasks.Values).ConfigureAwait(false);
        }
        catch
        {
            if (a_throwOnFault) throw;
        }

        return tasks;
    }

    /// <summary>
    /// Executes an async action for each item in the collection and waits for all to complete.
    /// </summary>
    /// <typeparam name="TInput">The type of input items.</typeparam>
    /// <param name="a_list">The collection of input items.</param>
    /// <param name="a_func">The async action to execute for each item.</param>
    /// <param name="a_throwOnFault">If true, throws on first faulted task; otherwise captures all results.</param>
    /// <returns>A collection of key-value pairs mapping each input to its completed task.</returns>
    /// <remarks>
    /// Similar to the generic version but for actions that don't return a value.
    /// Useful for operations like unlock that don't need results.
    /// </remarks>
    public static async Task<IEnumerable<KeyValuePair<TInput,Task>>> ForEachExecAndWait<TInput>(
        this IEnumerable<TInput> a_list,
        Func<TInput, Task> a_func,
        bool a_throwOnFault = false)
    where TInput : notnull
    {
        var tasks = new Dictionary<TInput, Task>();
        foreach (var item in a_list)
        {
            tasks.Add(item, a_func(item));
        }

        try
        {
            await Task.WhenAll(tasks.Values).ConfigureAwait(false);
        }
        catch
        {
            if (a_throwOnFault) throw;
        }

        return tasks;
    }

    /// <summary>
    /// Disposes all items in the collection asynchronously in parallel.
    /// </summary>
    /// <typeparam name="TInput">The type of disposable items.</typeparam>
    /// <param name="a_list">The collection of items to dispose.</param>
    /// <param name="a_throwOnFault">If true, throws on first faulted disposal; otherwise captures all results.</param>
    /// <returns>A collection of key-value pairs mapping each item to its disposal task.</returns>
    /// <remarks>
    /// Convenience method for disposing multiple <see cref="IAsyncDisposable"/> items in parallel.
    /// Used by RedLark to efficiently close all server connections.
    /// </remarks>
    public static async Task<IEnumerable<KeyValuePair<TInput,Task>>> ForEachAsyncDispose<TInput>(
        this IEnumerable<TInput> a_list,
        bool a_throwOnFault = false)
        where TInput : notnull, IAsyncDisposable
    {
        return await a_list.ForEachExecAndWait(c => c.DisposeAsync().AsTask(), a_throwOnFault).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes an async function for each item and yields results as tasks complete.
    /// </summary>
    /// <typeparam name="TInput">The type of input items.</typeparam>
    /// <typeparam name="TOutput">The return type of the async function.</typeparam>
    /// <param name="a_list">The collection of input items.</param>
    /// <param name="a_func">The async function to execute for each item.</param>
    /// <param name="a_throwOnFault">If true, throws when encountering a faulted task.</param>
    /// <returns>An async enumerable yielding results in completion order.</returns>
    /// <remarks>
    /// <para>
    /// <b>This is the key method enabling RedLark's quorum-based short-circuiting.</b>
    /// </para>
    /// <para>
    /// Unlike <see cref="ForEachExecAndWait{TInput, TOutput}"/>, this method yields results
    /// as soon as each task completes, allowing the caller to:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Process results immediately as they arrive</description></item>
    ///   <item><description>Stop iteration early once a quorum is achieved</description></item>
    ///   <item><description>Avoid waiting for slow servers when not needed</description></item>
    /// </list>
    /// <para>
    /// Example usage in lock acquisition:
    /// </para>
    /// <code>
    /// await foreach (var result in servers.ForEachEnumerateAsCompleted(s => s.Lock(resource, value, ttl)))
    /// {
    ///     if (result.Value.IsCompletedSuccessfully &amp;&amp; result.Value.Result)
    ///         locksAcquired++;
    ///     if (locksAcquired >= quorum)
    ///         break; // Short-circuit - don't wait for remaining servers
    /// }
    /// </code>
    /// </remarks>
    public static async IAsyncEnumerable<KeyValuePair<TInput,Task<TOutput>>> ForEachEnumerateAsCompleted<TInput, TOutput>(
        this IEnumerable<TInput> a_list,
        Func<TInput, Task<TOutput>> a_func,
        bool a_throwOnFault = false)
        where TInput: notnull
    {
        var tasks = new Dictionary<TInput, Task<TOutput>>();
        foreach (var item in a_list)
        {
            tasks.Add(item, a_func(item));
        }

        var activeTasks = new List<Task<TOutput>>(tasks.Values);
        while (activeTasks.Count > 0)
        {
            var completedTask = await Task.WhenAny(activeTasks.ToArray()).ConfigureAwait(false);
            activeTasks.Remove(completedTask);
            if (a_throwOnFault && completedTask.IsFaulted && completedTask.Exception != null)
            {
                throw completedTask.Exception;
            }
            yield return tasks.First(kv => kv.Value == completedTask);
        }

    }

    /// <summary>
    /// Executes an async action for each item and yields results as tasks complete.
    /// </summary>
    /// <typeparam name="TInput">The type of input items.</typeparam>
    /// <param name="a_list">The collection of input items.</param>
    /// <param name="a_func">The async action to execute for each item.</param>
    /// <param name="a_throwOnFault">If true, throws when encountering a faulted task.</param>
    /// <returns>An async enumerable yielding results in completion order.</returns>
    /// <remarks>
    /// Similar to the generic version but for actions that don't return a value.
    /// </remarks>
    public static async IAsyncEnumerable<KeyValuePair<TInput, Task>> ForEachEnumerateAsCompleted<TInput>(
        this IEnumerable<TInput> a_list,
        Func<TInput, Task> a_func,
        bool a_throwOnFault = false)
        where TInput: notnull
    {
        var tasks = new Dictionary<TInput, Task>();
        foreach (var item in a_list)
        {
            tasks.Add(item, a_func(item));
        }

        var activeTasks = new List<Task>(tasks.Values);
        while (activeTasks.Count > 0)
        {
            var completedTask = await Task.WhenAny(activeTasks.ToArray()).ConfigureAwait(false);
            activeTasks.Remove(completedTask);
            yield return tasks.First(kv => kv.Value == completedTask);
        }
    }
}

