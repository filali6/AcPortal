namespace Backend.Modules.Teams.Models;

public class TeamsActivity
{
    public string Type { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public TeamsActivityFrom? From { get; set; }
    public TeamsActivityBody? Body { get; set; }
    public string? ChannelId { get; set; }
    public string? TeamId { get; set; }
    public string? ReplyToId { get; set; }  // ID of the thread this is replying to
}

public class TeamsActivityFrom
{
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public string? Id { get; set; }
}

public class TeamsActivityBody
{
    public string? Content { get; set; }
    public string? ContentType { get; set; }
}