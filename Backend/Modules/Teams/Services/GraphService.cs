using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace Backend.Modules.Teams.Services;

public class GraphService
{
    private readonly GraphServiceClient _graphClient;
    private readonly ILogger<GraphService> _logger;

    public GraphService(IConfiguration configuration, ILogger<GraphService> logger)
    {
        _logger = logger;

        var clientId = configuration["MicrosoftGraph:ClientId"]!;
        var tenantId = configuration["MicrosoftGraph:TenantId"]!;
        var clientSecret = configuration["MicrosoftGraph:ClientSecret"]!;

        // ClientSecretCredential authenticates as the app (not a user)
        // This is called "app-only authentication" — the app acts on its own behalf
        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        _graphClient = new GraphServiceClient(credential);
    }

    // ─── TEAMS ────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new Team in Microsoft Teams for a project.
    /// Returns the Team ID — store it on the Project model.
    /// </summary>
    public async Task<string?> CreateTeamAsync(string projectName, string ownerEmail)
    {
        try
        {
            var team = new Team
            {
                DisplayName = projectName,
                Description = $"ACPortal project team for {projectName}",
                Visibility = TeamVisibilityType.Private,
                Members = new List<ConversationMember>
                {
                    new AadUserConversationMember
                    {
                        OdataType = "#microsoft.graph.aadUserConversationMember",
                        Roles = new List<string> { "owner" },
                        AdditionalData = new Dictionary<string, object>
                        {
                            ["user@odata.bind"] = $"https://graph.microsoft.com/v1.0/users/{ownerEmail}"
                        }
                    }
                },
                AdditionalData = new Dictionary<string, object>
                {
                    ["template@odata.bind"] = "https://graph.microsoft.com/v1.0/teamsTemplates('standard')"
                }
            };

            var result = await _graphClient.Teams.PostAsync(team);
            _logger.LogInformation("Team created for project {Project}", projectName);
            return result?.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Team for project {Project}", projectName);
            return null;
        }
    }

    // ─── CHANNELS ─────────────────────────────────────────────────

    /// <summary>
    /// Creates a channel inside a Team for a stream.
    /// Returns the Channel ID — store it on the Stream model.
    /// </summary>
    public async Task<string?> CreateChannelAsync(string teamId, string streamName)
    {
        try
        {
            var channel = new Channel
            {
                DisplayName = streamName,
                Description = $"ACPortal stream channel for {streamName}",
                MembershipType = ChannelMembershipType.Standard
            };

            var result = await _graphClient.Teams[teamId].Channels.PostAsync(channel);
            _logger.LogInformation("Channel created for stream {Stream}", streamName);
            return result?.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create channel for stream {Stream}", streamName);
            return null;
        }
    }

    // ─── MEMBERS ──────────────────────────────────────────────────

    /// <summary>
    /// Adds a user to a Team by their email.
    /// Called when a member is assigned to a project.
    /// </summary>
    public async Task AddMemberToTeamAsync(string teamId, string userEmail)
    {
        try
        {
            var member = new AadUserConversationMember
            {
                OdataType = "#microsoft.graph.aadUserConversationMember",
                Roles = new List<string> { "member" },
                AdditionalData = new Dictionary<string, object>
                {
                    ["user@odata.bind"] = $"https://graph.microsoft.com/v1.0/users/{userEmail}"
                }
            };

            await _graphClient.Teams[teamId].Members.PostAsync(member);
            _logger.LogInformation("Member {Email} added to team {TeamId}", userEmail, teamId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add member {Email} to team {TeamId}", userEmail, teamId);
        }
    }

    // ─── MESSAGES ─────────────────────────────────────────────────

    /// <summary>
    /// Posts a new message (thread) in a channel.
    /// Used for the first comment on a task — creates the thread.
    /// Returns the message ID — store it as TeamsThreadId on the task.
    /// </summary>
    public async Task<string?> PostMessageAsync(string teamId, string channelId, string content)
    {
        try
        {
            var message = new ChatMessage
            {
                Body = new ItemBody
                {
                    Content = content,
                    ContentType = BodyType.Html
                }
            };

            var result = await _graphClient.Teams[teamId].Channels[channelId].Messages.PostAsync(message);
            return result?.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post message to channel {ChannelId}", channelId);
            return null;
        }
    }

    /// <summary>
    /// Replies to an existing thread in a channel.
    /// Used for subsequent comments on the same task.
    /// </summary>
    public async Task ReplyToThreadAsync(string teamId, string channelId, string threadId, string content)
    {
        try
        {
            var reply = new ChatMessage
            {
                Body = new ItemBody
                {
                    Content = content,
                    ContentType = BodyType.Html
                }
            };

            await _graphClient.Teams[teamId].Channels[channelId].Messages[threadId].Replies.PostAsync(reply);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reply to thread {ThreadId}", threadId);
        }
    }

    /// <summary>
    /// Reads all replies in a thread.
    /// Used for AI Summary of Teams discussions.
    /// </summary>
    public async Task<List<string>> GetThreadRepliesAsync(string teamId, string channelId, string threadId)
    {
        try
        {
            var replies = await _graphClient.Teams[teamId]
                .Channels[channelId]
                .Messages[threadId]
                .Replies
                .GetAsync();

            return replies?.Value?
                .Where(r => r.Body?.Content != null)
                .Select(r => $"{r.From?.User?.DisplayName ?? "Unknown"}: {r.Body!.Content}")
                .ToList() ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get replies for thread {ThreadId}", threadId);
            return new List<string>();
        }
    }
}