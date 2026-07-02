using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using WebDts.Blazor.Models;

namespace WebDts.Blazor.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly string _settingsFile;
        private WebDtsSettings _settings;

        public SettingsService(IWebHostEnvironment environment)
        {
            _environment = environment;
            _settingsFile = Path.Combine(_environment.ContentRootPath, "user-settings.json");
        }

        public async Task<WebDtsSettings> GetSettingsAsync()
        {
            if (_settings == null)
            {
                await LoadSettingsAsync();
            }
            return _settings;
        }

        public async Task<WebDtsSettings> UpdateSettingsAsync(WebDtsSettings settings)
        {
            _settings = settings;
            await SaveSettingsAsync();
            return _settings;
        }

        public async Task<bool> SaveSettingsAsync()
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_settingsFile, json);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> ResetSettingsAsync()
        {
            _settings = new WebDtsSettings();
            return await SaveSettingsAsync();
        }

        private async Task LoadSettingsAsync()
        {
            try
            {
                if (File.Exists(_settingsFile))
                {
                    var json = await File.ReadAllTextAsync(_settingsFile);
                    _settings = JsonSerializer.Deserialize<WebDtsSettings>(json) ?? new WebDtsSettings();
                }
                else
                {
                    _settings = new WebDtsSettings();
                    await SaveSettingsAsync();
                }
            }
            catch (Exception)
            {
                _settings = new WebDtsSettings();
            }
        }
    }
}