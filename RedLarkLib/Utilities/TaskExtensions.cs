namespace RedLarkLib.Utilities;

public static class TaskExtensions
{
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

    public static async Task<IEnumerable<KeyValuePair<TInput,Task>>> ForEachAsyncDispose<TInput>(
        this IEnumerable<TInput> a_list,
        bool a_throwOnFault = false)
        where TInput : notnull, IAsyncDisposable
    {
        return await a_list.ForEachExecAndWait(c => c.DisposeAsync().AsTask(), a_throwOnFault).ConfigureAwait(false);
    }

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

