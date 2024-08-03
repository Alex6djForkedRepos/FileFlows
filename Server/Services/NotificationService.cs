using FileFlows.Plugin;
using FileFlows.Server.Hubs;
using FileFlows.Shared.Models;

namespace FileFlows.Server.Services;

/// <summary>
/// Service for notifications
/// </summary>
public class NotificationService : INotificationService
{
    List<Notification> _Notifications = new ();
    private readonly Queue<Notification> _LowNotifications = new();
    private FairSemaphore _semaphore = new(1);
    private DateTime? dbOfflineRecordedAt; 

    /// <inheritdoc />
    public async Task Record(NotificationSeverity severity, string title, string? message = null)
    {
        await _semaphore.WaitAsync();
        try
        {
            Notification notification = new()
            {
                Uid = Guid.NewGuid(),
                Date = DateTime.UtcNow,
                Severity = severity,
                Title = title,
                Read = severity is NotificationSeverity.Information, // dont count info, thats alwayus unread
                Message = message ?? string.Empty
            };
            if (severity is NotificationSeverity.Information)
            {
                _LowNotifications.Enqueue(notification);
                if (_LowNotifications.Count >= 1000)
                {
                    _LowNotifications.Dequeue(); // Remove the oldest item
                }
            }
            else
            {
                _Notifications.Add(notification);
                ClientServiceManager.Instance.SendNotification(severity, title);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }


    /// <summary>
    /// Gets all the notifications
    /// </summary>
    /// <param name="markAsRead">If the notifications should now be marked as read</param>
    /// <returns>all the notifications</returns>
    public async Task<IEnumerable<Notification>> GetAll(bool markAsRead = false)
    {
        await _semaphore.WaitAsync();
        try
        {
            var list = _Notifications.Union(_LowNotifications).OrderByDescending(x => x.Date).ToList();
            if (markAsRead)
            {
                foreach (var n in list)
                    n.Read = true;
            }

            return list;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Records the database has gone offline
    /// </summary>
    public async Task RecordDatabaseOffline()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (dbOfflineRecordedAt != null && dbOfflineRecordedAt > DateTime.UtcNow.AddHours(-1))
                return; // already reported
            dbOfflineRecordedAt = DateTime.UtcNow;
        }
        finally
        {
            _semaphore.Release();
        }
        
        await Record(NotificationSeverity.Critical, "Database Offline");
    }

    /// <summary>
    /// Gets the number of unread notifications
    /// </summary>
    /// <returns>the number of unread notifications</returns>
    /// <exception cref="NotImplementedException"></exception>
    public async Task<UnreadNotifications> GetUnreadNotificationsCount()
    {
        await _semaphore.WaitAsync();
        try
        {
            var critical =  _Notifications.Count(x => x is { Severity: NotificationSeverity.Critical, Read: false });
            var error =  _Notifications.Count(x => x is { Severity: NotificationSeverity.Error, Read: false });
            var warning =  _Notifications.Count(x => x is { Severity: NotificationSeverity.Warning, Read: false });
            
            return new()
            {
                Critical = critical,
                Error = error,
                Warning = warning
            };
        }
        finally
        {
            _semaphore.Release();
        }
    }
}