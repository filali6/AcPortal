using SlackNet;
using SlackNet.WebApi;

namespace Backend.Modules.Messaging.Services;

public class SlackMessagingProvider : IMessagingProvider
{
    private readonly ISlackApiClient? _slack;
    private readonly ILogger<SlackMessagingProvider> _logger;
    private readonly bool _isConfigured;
    private string? _teamId;

    public SlackMessagingProvider(IConfiguration config, ILogger<SlackMessagingProvider> logger)
    {
        _logger = logger;
        var botToken = config["Messaging:Slack:BotToken"];

        _isConfigured = !string.IsNullOrWhiteSpace(botToken);

        if (!_isConfigured)
        {
            _logger.LogWarning(
                "Messaging:Slack:BotToken est vide ou absent — SlackMessagingProvider est désactivé (no-op).");
            return;
        }

        _slack = new SlackServiceBuilder().UseApiToken(botToken).GetApiClient();
    }

    public async Task<(string?, string?)> CreateStreamChannelAsync(
        string projectName, string streamName, List<string> memberEmails)
    {
        if (!_isConfigured || _slack == null)
            return (null, null); // no-op silencieux si Slack n'est pas configuré

        var channelName = SanitizeChannelName($"proj-{projectName}-{streamName}");

        try
        {
            var channel = await _slack.Conversations.Create(channelName, isPrivate: false);

            foreach (var email in memberEmails)
            {
                await AddMemberAsync(channel.Id, email);
            }

            var teamId = await GetTeamIdAsync();
            var url = $"https://app.slack.com/client/{teamId}/{channel.Id}";
            return (channel.Id, url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Slack channel {Name}", channelName);
            return (null, null);
        }
    }

    public async Task AddMemberAsync(string channelId, string userEmail)
    {
        if (!_isConfigured || _slack == null) return;

        try
        {
            var user = await _slack.Users.LookupByEmail(userEmail);
            await _slack.Conversations.Invite(channelId, new[] { user.Id });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to add {Email} to channel {ChannelId}", userEmail, channelId);
        }
    }

    public async Task<(string?, string?)> PostThreadMessageAsync(string channelId, string message)
    {
        if (!_isConfigured || _slack == null)
            return (null, null);

        try
        {
            var response = await _slack.Chat.PostMessage(new Message
            {
                Channel = channelId,
                Text = message
            });

            var teamId = await GetTeamIdAsync();
            var url = $"https://app.slack.com/client/{teamId}/{channelId}/p{response.Ts.Replace(".", "")}";
            return (response.Ts, url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post message to channel {ChannelId}", channelId);
            return (null, null);
        }
    }

    public async Task ReplyToThreadAsync(string channelId, string threadId, string message)
    {
        if (!_isConfigured || _slack == null) return;

        try
        {
            await _slack.Chat.PostMessage(new Message
            {
                Channel = channelId,
                Text = message,
                ThreadTs = threadId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reply to thread {ThreadId}", threadId);
        }
    }

    private static string SanitizeChannelName(string name)
    {
        var clean = name.ToLowerInvariant().Replace(" ", "-");
        clean = System.Text.RegularExpressions.Regex.Replace(clean, "[^a-z0-9-_]", "");
        return clean.Length > 80 ? clean[..80] : clean;
    }

    private async Task<string> GetTeamIdAsync()
    {
        if (_teamId != null) return _teamId;
        if (_slack == null) return string.Empty;
        var authTest = await _slack.Auth.Test();
        _teamId = authTest.TeamId;
        return _teamId;
    }

    public async Task<string> GetUserNameAsync(string slackUserId)
    {
        if (!_isConfigured || _slack == null) return "Slack User";

        try
        {
            var user = await _slack.Users.Info(slackUserId);
            return user.Profile?.RealName ?? user.Name ?? "Slack User";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve Slack user {UserId}", slackUserId);
            return "Slack User";
        }
    }
}