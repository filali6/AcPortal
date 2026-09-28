namespace Backend.Modules.Sla.Models;

public class SlaWeeklyReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Content { get; set; } = string.Empty; // analyse markdown écrite par l'IA
    public int OverdueTasksCount { get; set; }
    public int AtRiskTasksCount { get; set; }
    public int OverdueStreamsCount { get; set; }
    public int AtRiskStreamsCount { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    // Lundi 00:00 UTC de la semaine du rapport — sert à n'avoir qu'un rapport automatique par semaine
    public DateTime WeekStart { get; set; }

    // true = généré avec le bouton "Générer maintenant" (pas de notification, ne bloque pas le job)
    public bool IsManual { get; set; }

    // Données structurées calculées par le backend (tableaux du rapport), en JSON camelCase
    public string? DataJson { get; set; }

    public static DateTime WeekStartOf(DateTime utc)
    {
        var day = utc.Date;
        var daysSinceMonday = ((int)day.DayOfWeek + 6) % 7; // lundi = 0
        return DateTime.SpecifyKind(day.AddDays(-daysSinceMonday), DateTimeKind.Utc);
    }
}