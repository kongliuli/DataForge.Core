using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WebDts.Blazor.Models;

namespace WebDts.Blazor.Services
{
    public interface ILogService
    {
        Task<IEnumerable<LogEntry>> GetLogsAsync(int page = 1, int pageSize = 50);
        Task<IEnumerable<LogEntry>> GetLogsByTaskIdAsync(Guid taskId, int page = 1, int pageSize = 50);
        Task AddLogAsync(LogEntry logEntry);
        Task ClearLogsAsync();
        Task<IEnumerable<LogEntry>> GetLogsByLevelAsync(string level, int page = 1, int pageSize = 50);
        Task<IEnumerable<LogEntry>> GetLogsByKeywordAsync(string keyword, int page = 1, int pageSize = 50);
    }
}