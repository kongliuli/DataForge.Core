using System;
using System.Collections.Generic;
using System.Threading;

namespace WebDts.Blazor.State
{
    public class TaskState
    {
        private List<string> _tasks;
        private string _selectedTaskId;
        private readonly object _lock = new object();

        public event Action OnChange;

        public List<string> Tasks
        {
            get
            {
                lock (_lock)
                {
                    return new List<string>(_tasks ?? new List<string>());
                }
            }
        }

        public string SelectedTaskId
        {
            get
            {
                lock (_lock)
                {
                    return _selectedTaskId;
                }
            }
            set
            {
                lock (_lock)
                {
                    if (_selectedTaskId != value)
                    {
                        _selectedTaskId = value;
                        NotifyStateChanged();
                    }
                }
            }
        }

        public TaskState()
        {
            _tasks = new List<string>();
        }

        public void SetTasks(List<string> tasks)
        {
            lock (_lock)
            {
                _tasks = new List<string>(tasks);
                NotifyStateChanged();
            }
        }

        public void AddTask(string taskId)
        {
            lock (_lock)
            {
                if (!_tasks.Contains(taskId))
                {
                    _tasks.Add(taskId);
                    NotifyStateChanged();
                }
            }
        }

        public void RemoveTask(string taskId)
        {
            lock (_lock)
            {
                if (_tasks.Remove(taskId))
                {
                    if (_selectedTaskId == taskId)
                    {
                        _selectedTaskId = null;
                    }
                    NotifyStateChanged();
                }
            }
        }

        private void NotifyStateChanged()
        {
            OnChange?.Invoke();
        }
    }
}
