namespace Backend.Modules.Messaging.Services;

public interface IMessagingProvider
{
    Task<(string? ChannelId, string? ChannelUrl)> CreateStreamChannelAsync(
        string projectName,
        string streamName,
        List<string> memberEmails);

    Task AddMemberAsync(string channelId, string userEmail);

    Task<(string? ThreadId, string? ThreadUrl)> PostThreadMessageAsync(
        string channelId,
        string message);

    Task ReplyToThreadAsync(string channelId, string threadId, string message);
    Task<string> GetUserNameAsync(string slackUserId);
}