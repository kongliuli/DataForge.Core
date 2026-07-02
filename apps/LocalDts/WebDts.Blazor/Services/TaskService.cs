using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using WebDts.Blazor.Models;

namespace WebDts.Blazor.Services
{
    public class TaskService : ITaskService
    {
        private readonly ConcurrentDictionary<Guid, WebMigrationTask> _tasks = new ConcurrentDictionary<Guid, WebMigrationTask>();

        public Task<IEnumerable<WebMigrationTask>> GetTasksAsync()
        {
            return Task.FromResult<IEnumerable<WebMigrationTask>>(_tasks.Values);
        }

        public Task<WebMigrationTask> GetTaskAsync(Guid taskId)
        {
            if (_tasks.TryGetValue(taskId, out var task))
            {
                return Task.FromResult(task);
            }
            throw new KeyNotFoundException($"Task with id {taskId} not found");
        }

        public Task<WebMigrationTask> CreateTaskAsync(WebMigrationTask task)
        {
            var newTask = new WebMigrationTask
            {
                Id = Guid.NewGuid(),
                Name = task.Name,
                Description = task.Description,
                SourcePluginId = task.SourcePluginId,
                TargetPluginId = task.TargetPluginId,
                SourceConfig = task.SourceConfig,
                TargetConfig = task.TargetConfig,
                Transformations = task.Transformations,
                Status = MigrationTaskStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            if (_tasks.TryAdd(newTask.Id, newTask))
            {
                return Task.FromResult(newTask);
            }
            throw new Exception("Failed to create task");
        }

        public Task<WebMigrationTask> UpdateTaskAsync(WebMigrationTask task)
        {
            if (_tasks.TryGetValue(task.Id, out var existingTask))
            {
                var updatedTask = existingTask with
                {
                    Name = task.Name,
                    Description = task.Description,
                    SourcePluginId = task.SourcePluginId,
                    TargetPluginId = task.TargetPluginId,
                    SourceConfig = task.SourceConfig,
                    TargetConfig = task.TargetConfig,
                    Transformations = task.Transformations,
                    UpdatedAt = DateTime.UtcNow
                };

                if (_tasks.TryUpdate(task.Id, updatedTask, existingTask))
                {
                    return Task.FromResult(updatedTask);
                }
            }
            throw new KeyNotFoundException($"Task with id {task.Id} not found");
        }

        public Task DeleteTaskAsync(Guid taskId)
        {
            if (_tasks.TryRemove(taskId, out _))
            {
                return Task.CompletedTask;
            }
            throw new KeyNotFoundException($"Task with id {taskId} not found");
        }

        public Task StartTaskAsync(Guid taskId)
        {
            if (_tasks.TryGetValue(taskId, out var task))
            {
                var updatedTask = task with
                {
                    Status = MigrationTaskStatus.Running,
                    UpdatedAt = DateTime.UtcNow
                };

                if (_tasks.TryUpdate(taskId, updatedTask, task))
                {
                    return Task.CompletedTask;
                }
            }
            throw new KeyNotFoundException($"Task with id {taskId} not found");
        }

        public Task StopTaskAsync(Guid taskId)
        {
            if (_tasks.TryGetValue(taskId, out var task))
            {
                var updatedTask = task with
                {
                    Status = MigrationTaskStatus.Stopped,
                    UpdatedAt = DateTime.UtcNow
                };

                if (_tasks.TryUpdate(taskId, updatedTask, task))
                {
                    return Task.CompletedTask;
                }
            }
            throw new KeyNotFoundException($"Task with id {taskId} not found");
        }

        public Task<WebMigrationTask> GetTaskStatusAsync(Guid taskId)
        {
            return GetTaskAsync(taskId);
        }

        public Task FailTaskAsync(Guid taskId, string errorMessage)
        {
            if (_tasks.TryGetValue(taskId, out var task))
            {
                var updatedTask = task with
                {
                    Status = MigrationTaskStatus.Failed,
                    ErrorMessage = errorMessage,
                    UpdatedAt = DateTime.UtcNow
                };

                if (_tasks.TryUpdate(taskId, updatedTask, task))
                {
                    return Task.CompletedTask;
                }
            }
            throw new KeyNotFoundException($"Task with id {taskId} not found");
        }
    }
}
