using Backend.Data;
using Backend.Modules.Events.Models;
using Backend.Modules.Tasks.Models;
using Backend.Modules.Auth.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Backend.Hubs;
using System.Text.Json;
using Backend.Modules.Events.Handlers;

namespace Backend.Modules.Events.Services;

public class EventProcessorService
{
    
    private readonly ILogger<EventProcessorService> _logger;
     
    private readonly WorkflowRulesService _workflowRules;
    private readonly Dictionary<string, IActionHandler> _handlers;

    public EventProcessorService(
         ILogger<EventProcessorService> logger,
         WorkflowRulesService workflowRules, 
         IEnumerable<IActionHandler> handlers)
    {
         _logger = logger;
         _workflowRules = workflowRules;
        _handlers = handlers.ToDictionary(h => h.ActionType);
        _logger.LogInformation(
            "Handlers chargés : {Handlers}",
            string.Join(", ", _handlers.Keys));
    
    }

    public async Task ProcessAsync(
        string messageValue,
        Guid? projectId = null)
    {
        var eventDto = JsonSerializer.Deserialize<AcpEventDto>(
            messageValue,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        if (eventDto == null)
        {
            _logger.LogWarning(
                "Message invalide — impossible de désérialiser.");
            return;
        }

        // ── AVANT : switch(eventDto.EventType) hard-codé
        // ── APRÈS : on lit la règle depuis le JSON
        var rule = _workflowRules.GetRule(eventDto.EventType);

        if (rule == null)
        {
            _logger.LogWarning(
                "Aucune règle trouvée pour l'event : {EventType}",
                eventDto.EventType);
            return;
        }
        if (!_handlers.TryGetValue(rule.ActionType, out var handler))
        {
            _logger.LogWarning(
                "Aucun handler pour l'action : {ActionType}",
                rule.ActionType);
            return;
        }

        _logger.LogInformation(
            "Exécution : {EventType} → {ActionType}",
            eventDto.EventType, rule.ActionType);

        await handler.HandleAsync(rule, eventDto, projectId);

    }
}