using System;
using System.Collections.Generic;
using System.Threading;

namespace WebDts.Blazor.State
{
    public class PluginState
    {
        private List<string> _plugins;
        private string _selectedPluginId;
        private readonly object _lock = new object();

        public event Action OnChange;

        public List<string> Plugins
        {
            get
            {
                lock (_lock)
                {
                    return new List<string>(_plugins ?? new List<string>());
                }
            }
        }

        public string SelectedPluginId
        {
            get
            {
                lock (_lock)
                {
                    return _selectedPluginId;
                }
            }
            set
            {
                lock (_lock)
                {
                    if (_selectedPluginId != value)
                    {
                        _selectedPluginId = value;
                        NotifyStateChanged();
                    }
                }
            }
        }

        public PluginState()
        {
            _plugins = new List<string>();
        }

        public void SetPlugins(List<string> plugins)
        {
            lock (_lock)
            {
                _plugins = new List<string>(plugins);
                NotifyStateChanged();
            }
        }

        public void AddPlugin(string pluginId)
        {
            lock (_lock)
            {
                if (!_plugins.Contains(pluginId))
                {
                    _plugins.Add(pluginId);
                    NotifyStateChanged();
                }
            }
        }

        public void RemovePlugin(string pluginId)
        {
            lock (_lock)
            {
                if (_plugins.Remove(pluginId))
                {
                    if (_selectedPluginId == pluginId)
                    {
                        _selectedPluginId = null;
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
