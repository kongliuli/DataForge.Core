using System.Runtime.ExceptionServices;

namespace DataMigration.Core;

public class ErrorHandling
{
    public static async Task<T> ExecuteWithRetry<T>(
        Func<Task<T>> operation,
        int maxRetries = 3,
        TimeSpan initialDelay = default,
        Func<Exception, bool> shouldRetry = null,
        CancellationToken ct = default)
    {
        if (initialDelay == default)
        {
            initialDelay = TimeSpan.FromMilliseconds(100);
        }

        if (shouldRetry == null)
        {
            shouldRetry = ex => ex is TransientException || IsTransientException(ex);
        }

        int retryCount = 0;
        while (true)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (retryCount < maxRetries && shouldRetry(ex))
            {
                retryCount++;
                var delayMs = initialDelay.TotalMilliseconds * Math.Pow(2, retryCount - 1);
                await Task.Delay(TimeSpan.FromMilliseconds(delayMs), ct);
            }
        }
    }

    public static async Task ExecuteWithRetry(
        Func<Task> operation,
        int maxRetries = 3,
        TimeSpan initialDelay = default,
        Func<Exception, bool> shouldRetry = null,
        CancellationToken ct = default)
    {
        await ExecuteWithRetry(
            async () => { await operation(); return true; },
            maxRetries,
            initialDelay,
            shouldRetry,
            ct);
    }

    private static bool IsTransientException(Exception ex)
    {
        // 检查常见的临时异常类型
        return ex is TimeoutException ||
               ex is IOException ||
               ex is System.Net.Sockets.SocketException ||
               ex is System.Net.Http.HttpRequestException ||
               (ex is System.Data.Common.DbException dbEx && IsTransientDbException(dbEx));
    }

    private static bool IsTransientDbException(System.Data.Common.DbException ex)
    {
        // 检查数据库临时异常
        return ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("deadlock", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("network", StringComparison.OrdinalIgnoreCase);
    }
}

public class TransientException : Exception
{
    public TransientException() { }
    public TransientException(string message) : base(message) { }
    public TransientException(string message, Exception inner) : base(message, inner) { }
}

