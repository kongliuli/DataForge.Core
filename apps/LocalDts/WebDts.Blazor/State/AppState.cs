using System;
using System.Threading;

namespace WebDts.Blazor.State
{
    public class AppState
    {
        private bool _isLoading;
        private string _errorMessage;
        private readonly object _lock = new object();

        public event Action OnChange;

        public bool IsLoading
        {
            get
            {
                lock (_lock)
                {
                    return _isLoading;
                }
            }
            set
            {
                lock (_lock)
                {
                    if (_isLoading != value)
                    {
                        _isLoading = value;
                        NotifyStateChanged();
                    }
                }
            }
        }

        public string ErrorMessage
        {
            get
            {
                lock (_lock)
                {
                    return _errorMessage;
                }
            }
            set
            {
                lock (_lock)
                {
                    if (_errorMessage != value)
                    {
                        _errorMessage = value;
                        NotifyStateChanged();
                    }
                }
            }
        }

        private void NotifyStateChanged()
        {
            OnChange?.Invoke();
        }

        public void ClearError()
        {
            ErrorMessage = null;
        }
    }
}
