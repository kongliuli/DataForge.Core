using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace WebDts.Blazor.Hubs;

[Authorize]
public class MigrationHub : Hub
{
    private readonly ILogger<MigrationHub> _logger;

    public MigrationHub(ILogger<MigrationHub> logger)
    {
        _logger = logger;
    }

    public async Task JoinTaskGroup(string taskId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, taskId);
        _logger.LogInformation("Client {ConnectionId} joined task group {TaskId}", 
            Context.ConnectionId, taskId);
    }

    public async Task LeaveTaskGroup(string taskId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, taskId);
        _logger.LogInformation("Client {ConnectionId} left task group {TaskId}", 
            Context.ConnectionId, taskId);
    }

    public async Task SendProgress(string taskId, int progress, string message)
    {
        await Clients.Group(taskId).SendAsync("ReceiveProgress", taskId, progress, message);
    }

    public async Task SendLog(string taskId, string level, string message)
    {
        await Clients.Group(taskId).SendAsync("ReceiveLog", taskId, level, message);
    }

    public async Task SendStatus(string taskId, string status, string? details = null)
    {
        await Clients.Group(taskId).SendAsync("ReceiveStatus", taskId, status, details);
    }

    public async Task SendSystemMessage(string message)
    {
        await Clients.All.SendAsync("ReceiveSystemMessage", message);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}, User: {UserId}", 
            Context.ConnectionId, Context.User?.Identity?.Name);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}, User: {UserId}", 
            Context.ConnectionId, Context.User?.Identity?.Name);
        await base.OnDisconnectedAsync(exception);
    }
}