namespace Backend.Modules.Sla.Models;

// Données du rapport hebdomadaire — toutes calculées par le backend (aucun chiffre inventé par l'IA)
public record WeeklyReportData(
    WeeklyKpis Kpis,
    WeeklyKpis? Previous,                  // chiffres de la semaine précédente (null = premier rapport)
    SlaComplianceStats Compliance,         // respect des SLA sur les tâches terminées cette semaine
    List<ProjectHealthRow> Projects,       // santé par projet actif
    List<BottleneckRow> Bottlenecks,       // personnes les plus chargées / en retard
    List<OverdueTaskRow> NewOverdue,       // tâches passées en retard cette semaine
    List<OverdueTaskRow> LongestOverdue);  // retards les plus anciens

public record WeeklyKpis(int OverdueTasks, int AtRiskTasks, int OverdueStreams, int AtRiskStreams);

public record SlaComplianceStats(int CompletedWithDueDate, int CompletedOnTime, int? OnTimeRatePercent);

public record ProjectHealthRow(
    Guid ProjectId,
    string ProjectName,
    int OpenTasks,
    int OverdueTasks,
    int AtRiskTasks,
    int OverdueStreams,
    int WorstDelayDays,
    string Health);                        // "Red" | "Orange" | "Green"

public record BottleneckRow(string Name, int OpenTasks, int OverdueTasks);

public record OverdueTaskRow(
    string Title,
    string? ProjectName,
    string? StreamName,
    string? AssignedToName,
    DateTime DueDate,
    int DaysLate);