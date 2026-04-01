using Backend.Modules.Events.Models;

namespace Backend.Modules.Events.Handlers;

public interface IActionHandler
{
    // identifie quel actionType ce handler gère
    // doit correspondre exactement à ce qui est dans le JSON
    string ActionType { get; }

    Task HandleAsync(
        WorkflowRule rule,
        AcpEventDto eventDto,
        Guid? projectId);
}