using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WebDts.Blazor.Models;

namespace WebDts.Blazor.Services
{
    public interface ITaskService
    {
        Task<IEnumerable<WebMigrationTask>> GetTasksAsync();
        Task<WebMigrationTask> GetTaskAsync(Guid taskId);
        Task<WebMigrationTask> CreateTaskAsync(WebMigrationTask task);
        Task<WebMigrationTask> UpdateTaskAsync(WebMigrationTask task);
        Task DeleteTaskAsync(Guid taskId);
        Task StartTaskAsync(Guid taskId);
        Task StopTaskAsync(Guid taskId);
        Task<WebMigrationTask> GetTaskStatusAsync(Guid taskId);
        Task FailTaskAsync(Guid taskId, string errorMessage);
    }
}
