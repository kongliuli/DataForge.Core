using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using WebDts.Blazor.Models;

namespace WebDts.Blazor.Services
{
    public class LogService : ILogService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly string _logsDirectory;

        public LogService(IWebHostEnvironment environment)
        {
            _environment = environment;
            _logsDirectory = Path.Combine(_environment.ContentRootPath, "logs");
        }

        public async Task<IEnumerable<LogEntry>> GetLogsAsync(int page = 1, int pageSize = 50)
        {
            var logs = await ReadAllLogsAsync();
            return Paginate(logs, page, pageSize);
        }

        public async Task<IEnumerable<LogEntry>> GetLogsByTaskIdAsync(Guid taskId, int page = 1, int pageSize = 50)
        {
            var logs = await ReadAllLogsAsync();
            var filteredLogs = logs.Where(log => log.Message.Contains(taskId.ToString())).ToList();
            return Paginate(filteredLogs, page, pageSize);
        }

        public async Task<IEnumerable<LogEntry>> GetLogsByLevelAsync(string level, int page = 1, int pageSize = 50)
        {
            var logs = await ReadAllLogsAsync();
            var filteredLogs = logs.Where(log => log.Level.Equals(level, StringComparison.OrdinalIgnoreCase)).ToList();
            return Paginate(filteredLogs, page, pageSize);
        }

        public async Task<IEnumerable<LogEntry>> GetLogsByKeywordAsync(string keyword, int page = 1, int pageSize = 50)
        {
            var logs = await ReadAllLogsAsync();
            var filteredLogs = logs.Where(log => log.Message.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();
            return Paginate(filteredLogs, page, pageSize);
        }

        public Task AddLogAsync(LogEntry logEntry)
        {
            // 这里可以实现添加日志的逻辑
            // 目前简单返回成功
            return Task.CompletedTask;
        }

        public Task ClearLogsAsync()
        {
            try
            {
                if (Directory.Exists(_logsDirectory))
                {
                    var logFiles = Directory.GetFiles(_logsDirectory, "webdts-*.log*");
                    foreach (var file in logFiles)
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch
                        {
                            // 忽略无法删除的文件（可能正在使用）
                        }
                    }
                }
                return Task.CompletedTask;
            }
            catch (Exception)
            {
                return Task.CompletedTask;
            }
        }

        private async Task<List<LogEntry>> ReadAllLogsAsync()
        {
            var logs = new List<LogEntry>();

            if (!Directory.Exists(_logsDirectory))
            {
                return logs;
            }

            var logFiles = Directory.GetFiles(_logsDirectory, "webdts-*.log*")
                .OrderByDescending(f => f);

            foreach (var file in logFiles)
            {
                try
                {
                    using (var reader = new StreamReader(file))
                    {
                        string line;
                        while ((line = await reader.ReadLineAsync()) != null)
                        {
                            var logEntry = ParseLogLine(line);
                            if (logEntry != null)
                            {
                                logs.Add(logEntry);
                            }
                        }
                    }
                }
                catch
                {
                    // 忽略无法读取的文件（可能正在使用）
                }
            }

            return logs.OrderByDescending(log => log.Timestamp).ToList();
        }

        private LogEntry ParseLogLine(string line)
        {
            // 解析 Serilog 日志格式
            // 示例格式: 2026-04-08 12:34:56.789 +08:00 [INF] WebDts.Blazor.Program - Application started
            var pattern = @"(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} \+\d{2}:\d{2}) \[(\w+)\] ([^-]+) - (.*)";
            var match = Regex.Match(line, pattern);

            if (match.Success)
            {
                return new LogEntry
                {
                    Timestamp = DateTime.Parse(match.Groups[1].Value),
                    Level = match.Groups[2].Value,
                    Message = match.Groups[4].Value
                };
            }

            return null;
        }

        private IEnumerable<LogEntry> Paginate(List<LogEntry> logs, int page, int pageSize)
        {
            var skip = (page - 1) * pageSize;
            return logs.Skip(skip).Take(pageSize);
        }
    }
}