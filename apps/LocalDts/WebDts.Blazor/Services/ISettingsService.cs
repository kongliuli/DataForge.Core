using System.Threading.Tasks;
using WebDts.Blazor.Models;

namespace WebDts.Blazor.Services
{
    public interface ISettingsService
    {
        Task<WebDtsSettings> GetSettingsAsync();
        Task<WebDtsSettings> UpdateSettingsAsync(WebDtsSettings settings);
        Task<bool> SaveSettingsAsync();
        Task<bool> ResetSettingsAsync();
    }
}