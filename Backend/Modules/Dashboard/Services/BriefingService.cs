using Backend.Data;
using Backend.Modules.Contracts.Services;
using Backend.Modules.Tasks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Backend.Modules.Dashboard.Services;

public class BriefingService
{
    private readonly AppDbContext _db;
    private readonly IContractSummaryService _summaryService;
    private readonly IMemoryCache _cache;

    private static readonly TimeSpan CacheDuration=TimeSpan.FromHours(24);

    public BriefingService(AppDbContext db, IContractSummaryService summaryService,IMemoryCache cache)
    {
        _db = db;
        _summaryService = summaryService;
        _cache=cache;
    }

    public async Task<string> GenerateBriefingAsync(Guid userId, string keycloakId, string userFullName, string role)
    {
        var cacheKey = $"briefing:{userId}";

        // Return from cache if available
        if (_cache.TryGetValue(cacheKey, out string? cached) && cached != null)
            return cached;
        var context = role switch
        {
            "ProjectManager" => await BuildPMContextAsync(userId, keycloakId, userFullName),
            "HeadOfCDS" => await BuildHeadOfCDSContextAsync(userFullName,keycloakId),
            "Consultant" => await BuildConsultantContextAsync(keycloakId, userFullName),
            "BusinessTeamLead" or
            "TechnicalTeamLead" => await BuildTeamLeadContextAsync(keycloakId, userFullName, role),
            "PortfolioDirector" => await BuildPortfolioDirectorContextAsync(userId, userFullName,keycloakId),
            _ => $"User: {userFullName}, Role: {role}."
        };

        var prompt = $"""
    You are a smart assistant for a project management portal called ACPortal.
    Write a short personal briefing for this user in 2 sentences maximum.

    Rules:
    - Narrative style, like a smart assistant talking to a manager
    - Highlight the most critical issue first (projects without PM, high pending tasks, etc.)
    - Use exact numbers from the data
    - Business tone, direct, no fluff
    - Never start with "Good morning" or "Hello"
    - Start with the most urgent information

    USER DATA:
    {context}
    """;

        try
        {
            var result= await _summaryService.SummarizeAsync(context, prompt);
            _cache.Set(cacheKey, result, CacheDuration);
            return result;
        }
        catch (Exception)
        {
            return context;
        }
    }
    private async Task<string> BuildPMContextAsync(Guid userId, string keycloakId, string name)
    {
        var projects = await _db.Projects
            .Where(p => p.ProjectManagerId == userId)
            .CountAsync();

        var tasks = await _db.AcpTasks
            .Where(t => t.AssignedTo == keycloakId)
            .ToListAsync();

        var pending = tasks.Count(t => t.Status == AcpTaskStatus.Pending);
        var done = tasks.Count(t => t.Status == AcpTaskStatus.Done);

        return $"Name: {name}, Role: Project Manager, " +
               $"Projects managed: {projects}, " +
               $"Pending tasks: {pending}, Completed tasks: {done}";
    }
    private async Task<string> BuildHeadOfCDSContextAsync(string name, string keycloakId)
    {
        var totalProjects = await _db.Projects.CountAsync();
        var projectsWithoutPM = await _db.Projects.CountAsync(p => p.ProjectManagerId == null);
        var pendingTasks = await _db.AcpTasks.CountAsync(t => t.Status == AcpTaskStatus.Pending);
        var doneTasks = await _db.AcpTasks.CountAsync(t => t.Status == AcpTaskStatus.Done);
        var myOwnPending = await _db.AcpTasks.CountAsync(t => t.AssignedTo == keycloakId && t.Status == AcpTaskStatus.Pending);

        return $"Name: {name}, Role: Head of CDS, " +
               $"Total projects: {totalProjects}, Projects without PM: {projectsWithoutPM}, " +
               $"Pending tasks across all projects: {pendingTasks}, Completed tasks: {doneTasks}, " +
               $"My own personal pending tasks: {myOwnPending}";
    }

    private async Task<string> BuildConsultantContextAsync(string keycloakId, string name)
    {
        var tasks = await _db.AcpTasks
            .Where(t => t.AssignedTo == keycloakId)
            .ToListAsync();

        var pending = tasks.Count(t => t.Status == AcpTaskStatus.Pending);
        var done = tasks.Count(t => t.Status == AcpTaskStatus.Done);

        var streams = await _db.StreamMembers
            .Where(m => m.Consultant.KeycloakId == keycloakId)
            .CountAsync();

        return $"Name: {name}, Role: Consultant, " +
               $"Active streams: {streams}, Pending tasks: {pending}, Completed tasks: {done}";
    }

    private async Task<string> BuildTeamLeadContextAsync(string keycloakId, string name, string role)
    {
        var tasks = await _db.AcpTasks
            .Where(t => t.AssignedTo == keycloakId)
            .ToListAsync();

        var pending = tasks.Count(t => t.Status == AcpTaskStatus.Pending);
        var done = tasks.Count(t => t.Status == AcpTaskStatus.Done);

        return $"Name: {name}, Role: {role}, " +
               $"Pending tasks: {pending}, Completed tasks: {done}";
    }

    private async Task<string> BuildPortfolioDirectorContextAsync(Guid userId, string name, string keycloakId)
    {
        var portfolios = await _db.Portfolios.Where(p => p.PortfolioDirectorId == userId).CountAsync();
        var projects = await _db.Projects.Where(p => p.Portfolio != null && p.Portfolio.PortfolioDirectorId == userId).CountAsync();
        var projectsWithoutPM = await _db.Projects.Where(p => p.Portfolio != null && p.Portfolio.PortfolioDirectorId == userId && p.ProjectManagerId == null).CountAsync();
        var myOwnPending = await _db.AcpTasks.CountAsync(t => t.AssignedTo == keycloakId && t.Status == AcpTaskStatus.Pending);

        return $"Name: {name}, Role: Portfolio Director, " +
               $"Portfolios managed: {portfolios}, Total projects: {projects}, " +
               $"Projects without PM: {projectsWithoutPM}, " +
               $"My own personal pending tasks: {myOwnPending}";
    }
}