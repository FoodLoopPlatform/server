using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Options;
using FoodLoop.Infrastructure.Persistence;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Services;

public class FirebasePushNotificationService : IFirebasePushNotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly FirebaseOptions _options;
    private readonly ILogger<FirebasePushNotificationService> _logger;

    public FirebasePushNotificationService(
        ApplicationDbContext db,
        IOptions<FirebaseOptions> options,
        ILogger<FirebasePushNotificationService> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendToUserAsync(Guid userId, string title, string body, string type, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Firebase push is disabled; skipping mobile notification for user {UserId}", userId);
            return;
        }

        var tokens = await _db.Set<UserDeviceToken>()
            .Where(t => t.UserId == userId && t.IsActive)
            .Select(t => t.Token)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (tokens.Count == 0)
        {
            return;
        }

        try
        {
            await EnsureFirebaseInitializedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Firebase initialization failed for user {UserId}", userId);
            return;
        }

        foreach (var token in tokens)
        {
            try
            {
                var message = new Message
                {
                    Token = token,
                    Notification = new FirebaseAdmin.Messaging.Notification
                    {
                        Title = title,
                        Body = body
                    },
                    Data = new Dictionary<string, string>
                    {
                        ["type"] = type,
                        ["userId"] = userId.ToString(),
                        ["body"] = body,
                        ["title"] = title
                    }
                };

                var messaging = FirebaseMessaging.GetMessaging(FirebaseApp.DefaultInstance);
                var response = await messaging.SendAsync(message, cancellationToken);
                _logger.LogInformation("Firebase push sent to token for user {UserId}. MessageId: {MessageId}", userId, response);
            }
            catch (FirebaseMessagingException ex)
            {
                _logger.LogError(ex, "Firebase messaging exception while sending push to user {UserId} with token {Token}", userId, token);
                if (ex.MessagingErrorCode == MessagingErrorCode.Unregistered || 
                    ex.MessagingErrorCode == MessagingErrorCode.InvalidArgument ||
                    ex.ErrorCode == ErrorCode.InvalidArgument ||
                    ex.Message.Contains("unregistered", StringComparison.OrdinalIgnoreCase) ||
                    ex.Message.Contains("invalid-argument", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Stale or invalid FCM token detected for user {UserId}. Marking token as inactive: {Token}", userId, token);
                    var dbToken = await _db.Set<UserDeviceToken>()
                        .FirstOrDefaultAsync(t => t.UserId == userId && t.Token == token, cancellationToken);
                    if (dbToken != null)
                    {
                        dbToken.IsActive = false;
                        await _db.SaveChangesAsync(cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected exception while sending Firebase push to user {UserId} with token {Token}", userId, token);
            }
        }
    }

    private async Task EnsureFirebaseInitializedAsync()
    {
        if (FirebaseApp.DefaultInstance is not null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.ServiceAccountJson) && string.IsNullOrWhiteSpace(_options.ServiceAccountJsonPath))
        {
            throw new InvalidOperationException("Firebase service account is not configured.");
        }

        var json = !string.IsNullOrWhiteSpace(_options.ServiceAccountJson)
            ? _options.ServiceAccountJson
            : await System.IO.File.ReadAllTextAsync(_options.ServiceAccountJsonPath);

        var credential = GoogleCredential.FromJson(json);
        FirebaseApp.Create(new AppOptions
        {
            Credential = credential,
            ProjectId = _options.ProjectId
        });
    }
}
