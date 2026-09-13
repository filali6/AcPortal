using Backend.Data;
using Backend.Modules.Auth.Models;
using Backend.Modules.Contracts.Services;
using Backend.Modules.Events.Services;
using Backend.Modules.Git.Services;
using Backend.Modules.Planning.Models;
using Backend.Modules.Planning.Services;
using Backend.Modules.Projects.Models;
using Backend.Modules.Tools.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph.Drives.Item.Items.Item.Workbook.Functions.True;
using System.Text.Json;

namespace Backend.Modules.Planning.Controllers;

[ApiController]
[Route("api/planning")]
[Authorize(Roles = "ProjectManager")]
public class PlanningController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly FsdPlanningService _planning;
    private readonly IPdfTextExtractor _extractor;
    private readonly EventPublisher _publisher;
    private readonly GitService _gitService;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;
    private readonly ILogger<PlanningController> _logger;
    private readonly PluginRegistry _plugins;

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    public PlanningController(
        AppDbContext db, FsdPlanningService planning,
        IPdfTextExtractor extractor, EventPublisher publisher,
        GitService gitService, IWebHostEnvironment env,
        IConfiguration config, ILogger<PlanningController> logger,PluginRegistry plugins)
    {
        _db = db; _planning = planning; _extractor = extractor;
        _publisher = publisher; _gitService = gitService;
        _env = env; _config = config; _logger = logger;
        _plugins=plugins;
    }

    // POST /api/planning/generate
    [HttpPost("generate")]
    public async Task<IActionResult> Generate(
        [FromForm] IFormFile file,
        [FromForm] Guid projectId,
        [FromForm] string? guidelines)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "FSD file is required" });

        // Sauvegarder le fichier
        var folder = Path.Combine(_env.ContentRootPath,
            _config["Planning:FsdUploadFolder"] ?? "uploads/fsd");
        Directory.CreateDirectory(folder);
        var fileName = $"fsd_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(folder, fileName);

        await using (var s = System.IO.File.Create(filePath))
            await file.CopyToAsync(s);

        // Extraire le texte
        string fsdText;
        try { fsdText = _extractor.Extract(filePath); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FSD extraction failed");
            return BadRequest(new { message = "Could not read the file" });
        }

        var effectiveGuidelines = string.IsNullOrEmpty(guidelines)
            ? $"Maximum {_config.GetValue("Planning:DefaultMaxStreams", 5)} streams. Maximum {_config.GetValue("Planning:DefaultMaxConsultantsPerStream", 3)} consultants per stream."
            : guidelines;

        // Appeler l'agent (avec ses tools)
        var planJson = await _planning.GenerateAsync(fsdText, effectiveGuidelines);
        if (planJson == null)
            return StatusCode(500, new { message = "AI planning failed. Please try again." });

        var proposal = new PlanningProposal
        {
            ProjectId = projectId,
            ProposalJson = planJson,
            FsdFileName = fileName,
            Guidelines = effectiveGuidelines
        };

        _db.PlanningProposals.Add(proposal);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            proposalId = proposal.Id,
            plan = JsonSerializer.Deserialize<object>(planJson)
        });
    }

    // POST /api/planning/{id}/refine
    [HttpPost("{id:guid}/refine")]
    public async Task<IActionResult> Refine(Guid id, [FromBody] RefineRequest req)
    {
        var proposal = await _db.PlanningProposals.FindAsync(id);
        if (proposal == null) return NotFound();
        if (proposal.Status == "Approved")
            return BadRequest(new { message = "Cannot refine an approved proposal" });

        // Utilise le plan actuel du frontend (avec modifs inline) 
        // ou fallback sur la DB si non fourni
        var activePlanJson = req.CurrentPlanJson ?? proposal.ProposalJson;

        // Contexte des personnes pour que l'agent connaisse les noms
        var peopleContext = await BuildPeopleContextAsync();

        var eligiblePlugins = _plugins.GetAll()
            .Where(p => p.IsActive
                     && !string.IsNullOrEmpty(p.Url)
                     && !string.IsNullOrEmpty(p.FunctionalDomain))
            .Select(p => $"- ID:{p.Id} | Name:{p.Name} | Domain:{p.FunctionalDomain}")
            .ToList();

        var updatedJson = await _planning.RefineAsync(
            activePlanJson,
            proposal.ConversationJson,
            req.Message,
            string.Join("\n", eligiblePlugins),
            peopleContext);

        if (updatedJson == null)
            return StatusCode(500, new { message = "AI refinement failed. Please try again." });

        // Sauvegarde le plan mis à jour en DB
        var history = JsonSerializer.Deserialize<List<object>>(
            proposal.ConversationJson, JsonOpts) ?? new();
        history.Add(new { role = "user", content = req.Message, timestamp = DateTime.UtcNow });
        history.Add(new { role = "assistant", content = "Plan updated", timestamp = DateTime.UtcNow });

        proposal.ProposalJson = updatedJson;
        proposal.ConversationJson = JsonSerializer.Serialize(history);
        proposal.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { plan = JsonSerializer.Deserialize<object>(updatedJson, JsonOpts) });
    }

    // Helper — construit le contexte des personnes
    private async Task<string> BuildPeopleContextAsync()
    {
        var bizLeads = await _db.Users
            .Where(u => u.Role == GlobalRole.BusinessTeamLead)
            .Select(u => $"{u.FullName} (ID:{u.Id})")
            .ToListAsync();

        var techLeads = await _db.Users
            .Where(u => u.Role == GlobalRole.TechnicalTeamLead)
            .Select(u => $"{u.FullName} (ID:{u.Id})")
            .ToListAsync();

        var bizCons = await _db.Users
            .Where(u => u.Role == GlobalRole.Consultant && u.ConsultantType == ConsultantType.Business)
            .Select(u => $"{u.FullName} (ID:{u.Id})")
            .ToListAsync();

        var techCons = await _db.Users
            .Where(u => u.Role == GlobalRole.Consultant && u.ConsultantType == ConsultantType.Technical)
            .Select(u => $"{u.FullName} (ID:{u.Id})")
            .ToListAsync();

        return $"""
        AVAILABLE PEOPLE (use exact IDs when assigning):
        Business Leads: {string.Join(", ", bizLeads)}
        Technical Leads: {string.Join(", ", techLeads)}
        Business Consultants: {string.Join(", ", bizCons)}
        Technical Consultants: {string.Join(", ", techCons)}
        """;
    }

    // POST /api/planning/{id}/approve
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveRequest? request)
    {
        var proposal = await _db.PlanningProposals.FindAsync(id);
        if (proposal == null) return NotFound();
        if (proposal.Status == "Approved")
            return BadRequest(new { message = "Already approved" });

        var project = await _db.Projects.FindAsync(proposal.ProjectId);
        if (project == null) return NotFound();

        // Utilise le plan modifié par le PM si fourni
        var planJson = request?.CurrentPlanJson ?? proposal.ProposalJson;
        var plan = JsonSerializer.Deserialize<JsonElement>(planJson);
        var streams = plan.GetProperty("streams").EnumerateArray().ToList();

        foreach (var streamEl in streams)
        {
            var stream = new Backend.Modules.Projects.Models.Stream
            {
                Name = streamEl.GetProperty("name").GetString() ?? "Unnamed Stream",
                ProjectId = proposal.ProjectId,
                BusinessTeamLeadId = GetGuid(streamEl, "businessLeadId"),
                TechnicalTeamLeadId = GetGuid(streamEl, "technicalLeadId")
            };

            _db.Streams.Add(stream);
            await _db.SaveChangesAsync();

            // Membres Business
            if (streamEl.TryGetProperty("businessConsultantIds", out var bizCons))
                foreach (var cid in bizCons.EnumerateArray())
                    if (Guid.TryParse(cid.GetString(), out var consultantId))
                        _db.StreamMembers.Add(new StreamMember
                        {
                            StreamId = stream.Id,
                            ConsultantId = consultantId,
                            TeamType = TeamType.Business
                        });

            // Membres Technical
            if (streamEl.TryGetProperty("technicalConsultantIds", out var techCons))
                foreach (var cid in techCons.EnumerateArray())
                    if (Guid.TryParse(cid.GetString(), out var consultantId))
                        _db.StreamMembers.Add(new StreamMember
                        {
                            StreamId = stream.Id,
                            ConsultantId = consultantId,
                            TeamType = TeamType.Technical
                        });

            await _db.SaveChangesAsync();

            // Steps avec TeamType
            if (streamEl.TryGetProperty("steps", out var stepsEl))
            {
                foreach (var stepEl in stepsEl.EnumerateArray())
                {
                    var teamTypeStr = stepEl.TryGetProperty("teamType", out var tt)
                        ? tt.GetString() : "Business";

                    _db.ProjectSteps.Add(new ProjectStep
                    {
                        ProjectId = proposal.ProjectId,
                        StreamId = stream.Id,
                        StepName = stepEl.TryGetProperty("stepName", out var sn) ? sn.GetString() ?? "" : "",
                        ToolName = stepEl.TryGetProperty("pluginId", out var pid) ? pid.GetString() ?? "" : "",
                        Order = stepEl.TryGetProperty("order", out var ord) ? ord.GetInt32() : 1,
                        TeamType = teamTypeStr == "Technical" ? TeamType.Technical : TeamType.Business
                    });
                }
                await _db.SaveChangesAsync();
            }

            await _gitService.InitStreamRepoAsync(stream.Id, proposal.ProjectId);

            await _publisher.PublishAsync(new
            {
                eventType = "StreamCrééParIA",
                projectId = proposal.ProjectId,
                projectName = project.Name,
                streamId = stream.Id,
                streamName = stream.Name,
                businessTeamLeadId = stream.BusinessTeamLeadId,
                technicalTeamLeadId = stream.TechnicalTeamLeadId
            }, proposal.ProjectId, project.Name);
        }

        proposal.ProposalJson = planJson;
        proposal.Status = "Approved";
        proposal.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = $"Plan approved. {streams.Count} streams triggered." });
    }

    // POST /api/planning/{id}/reject
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id)
    {
        var proposal = await _db.PlanningProposals.FindAsync(id);
        if (proposal == null) return NotFound();
        proposal.Status = "Rejected";
        proposal.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Proposal rejected" });
    }

    // GET /api/planning/project/{projectId}
    [HttpGet("project/{projectId:guid}")]
    public async Task<IActionResult> GetByProject(Guid projectId)
    {
        var proposals = await _db.PlanningProposals
            .Where(p => p.ProjectId == projectId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return Ok(proposals.Select(p => new
        {
            p.Id,
            p.Status,
            p.Guidelines,
            p.CreatedAt,
            p.UpdatedAt,
            plan = JsonSerializer.Deserialize<object>(p.ProposalJson, JsonOpts)
        }));
    }




    [HttpGet("streams/{streamId:guid}/steps")]
    [Authorize(Roles = "BusinessTeamLead,TechnicalTeamLead,HeadOfCDS")]
    public async Task<IActionResult> GetStreamSteps(Guid streamId)
    {
        var steps = await _db.ProjectSteps
            .Where(s => s.StreamId == streamId)
            .OrderBy(s => s.Order)
            .Select(s => new
            {
                s.Id,
                s.StepName,
                s.ToolName,
                s.Order,
                s.CanBeParallel,
                s.DependsOnStepId
            })
            .ToListAsync();

        return Ok(steps);
    }

    // POST /api/planning/streams/{streamId}/approve-steps
    // Le Team Lead approuve (éventuellement après modification) les steps générés par l'IA
    [HttpPost("streams/{streamId:guid}/approve-steps")]
    [Authorize(Roles = "BusinessTeamLead,TechnicalTeamLead,HeadOfCDS")]
    public async Task<IActionResult> ApproveStreamSteps(Guid streamId, [FromBody] ApproveStepsRequest request)
    {
        var stream = await _db.Streams.FindAsync(streamId);
        if (stream == null) return NotFound(new { message = "Stream not found" });

        // Si le Team Lead a modifié les steps, on remplace les anciens
        if (request.Steps != null && request.Steps.Any())
        {
            var existingSteps = _db.ProjectSteps.Where(s => s.StreamId == streamId);
            _db.ProjectSteps.RemoveRange(existingSteps);
            await _db.SaveChangesAsync();

            foreach (var stepDto in request.Steps.OrderBy(s => s.Order))
            {
                _db.ProjectSteps.Add(new ProjectStep
                {
                    ProjectId = stream.ProjectId,
                    StreamId = streamId,
                    StepName = stepDto.StepName,
                    ToolName = stepDto.ToolName,
                    Order = stepDto.Order,
                    CanBeParallel = stepDto.CanBeParallel
                });
            }
            await _db.SaveChangesAsync();
        }

        // Même logique que ProjectStepsController.CreateSteps() —
        // déclenche StepsDéfinis → tâches créées pour les consultants
        var keycloakId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var leadUser = await _db.Users.FirstOrDefaultAsync(u => u.KeycloakId == keycloakId);
        var leadRole = leadUser?.Role.ToString() ?? "";
        var project = await _db.Projects.FindAsync(stream.ProjectId);

        await _publisher.PublishAsync(new
        {
            eventType = "StepsDéfinis",
            projectId = stream.ProjectId,
            projectName = project!.Name,
            streamId = streamId,
            leadRole = leadRole
        }, stream.ProjectId, project.Name);

        return Ok(new { message = "Steps approved — tasks being generated for consultants" });
    }

    public class ApproveStepsRequest
    {
        public List<Backend.Modules.Projects.Models.StepDto>? Steps { get; set; }
    }

    private static Guid? GetGuid(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind != JsonValueKind.Null
        && Guid.TryParse(v.GetString(), out var g) ? g : null;

    public class RefineRequest
    {
        public string Message { get; set; } = string.Empty;
        public string? CurrentPlanJson { get; set; }

    }
    public class ApproveRequest
    {
        public string? CurrentPlanJson { get; set; }
    }
}