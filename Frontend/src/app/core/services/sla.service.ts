import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

// Étape 1 — la règle SANS domaine est la règle par défaut de son type
export interface SlaRule {
  id: string;
  name: string;
  description?: string;
  type: 'Task' | 'Stream';
  slaDays: number;
  functionalDomain?: string | null;
  isDefault: boolean;
  createdAt: string;
}

export interface SlaRuleRequest {
  name: string;
  description?: string;
  type: 'Task' | 'Stream';
  slaDays: number;
  functionalDomain?: string | null;
}

// Étape 4 — dashboard enrichi
export interface SlaTaskItem {
  id: string;
  title: string;
  status: number;
  dueDate: string;
  assignedTo: string;
  assignedToName?: string;
  projectId: string;
  projectName?: string;
  streamId: string;
  streamName?: string;
  taskThreadUrl?: string;
  streamChannelUrl?: string;
  slaStatus: 'OnTrack' | 'AtRisk' | 'Overdue';
  daysRemaining: number;
}

export interface SlaStreamItem {
  id: string;
  name: string;
  dueDate: string;
  projectId?: string;
  projectName: string;
  businessLeadName?: string;
  technicalLeadName?: string;
  messagingChannelUrl?: string;
  slaStatus: 'OnTrack' | 'AtRisk' | 'Overdue';
  daysRemaining: number;
}

export interface SlaDashboard {
  tasks: SlaTaskItem[];
  streams: SlaStreamItem[];
  summary: {
    totalOverdueTasks: number;
    totalAtRiskTasks: number;
    totalOverdueStreams: number;
    totalAtRiskStreams: number;
  };}
  // Étape 6 — rapport hebdomadaire détaillé
export interface WeeklyKpis {
  overdueTasks: number;
  atRiskTasks: number;
  overdueStreams: number;
  atRiskStreams: number;
}

export interface WeeklyOverdueRow {
  title: string;
  projectName?: string;
  streamName?: string;
  assignedToName?: string;
  dueDate: string;
  daysLate: number;
}

export interface WeeklyReportData {
  kpis: WeeklyKpis;
  previous: WeeklyKpis | null;
  compliance: { completedWithDueDate: number; completedOnTime: number; onTimeRatePercent: number | null };
  projects: {
    projectId: string; projectName: string; openTasks: number; overdueTasks: number;
    atRiskTasks: number; overdueStreams: number; worstDelayDays: number;
    health: 'Red' | 'Orange' | 'Green';
  }[];
  bottlenecks: { name: string; openTasks: number; overdueTasks: number }[];
  newOverdue: WeeklyOverdueRow[];
  longestOverdue: WeeklyOverdueRow[];
}

export interface WeeklyReportSummary {
  id: string;
  generatedAt: string;
  weekStart: string;
  isManual: boolean;
  overdueTasksCount: number;
  atRiskTasksCount: number;
  overdueStreamsCount: number;
  atRiskStreamsCount: number;
}

export interface WeeklyReport extends WeeklyReportSummary {
  content: string;
  data: WeeklyReportData | null; // null pour les anciens rapports (texte seulement)
}


@Injectable({
  providedIn: 'root'
})
export class SlaService {

  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  // ───────────── Règles SLA (nouveaux endpoints, avec vérifications) ─────────────

  getAllRules(): Observable<SlaRule[]> {
    return this.http.get<SlaRule[]>(`${this.apiUrl}/sla/rules`);
  }

  // Alias utilisé par l'écran du PM (étape 5.1)
  getRules(): Observable<SlaRule[]> {
    return this.getAllRules();
  }

  createRule(data: SlaRuleRequest): Observable<any> {
    return this.http.post(`${this.apiUrl}/sla/rules`, data);
  }

  // PUT : on envoie la règle complète
  updateRule(id: string, data: SlaRuleRequest): Observable<any> {
    return this.http.put(`${this.apiUrl}/sla/rules/${id}`, data);
  }

  deleteRule(id: string): Observable<any> {
    return this.http.delete(`${this.apiUrl}/sla/rules/${id}`);
  }

  // Domaines des plugins (liste déroulante + badges)
  getPluginDomains(): Observable<{ domains: string[]; plugins: { pluginId: string; domain: string }[] }> {
    return this.http.get<any>(`${this.apiUrl}/sla/plugin-domains`);
  }

  // ───────────── Dashboard ─────────────

  getDashboard(): Observable<SlaDashboard> {
    return this.http.get<SlaDashboard>(`${this.apiUrl}/sla/dashboard`);
  }

  getMyTasksWithSla(): Observable<SlaTaskItem[]> {
    return this.http.get<SlaTaskItem[]>(`${this.apiUrl}/sla/my-tasks`);
  }

  // ───────────── Dates limites ─────────────

  setStreamDueDate(streamId: string, dueDate: string | null): Observable<any> {
    return this.http.patch(`${this.apiUrl}/sla/streams/${streamId}/due-date`, { dueDate });
  }

  setTaskDueDate(taskId: string, dueDate: string): Observable<any> {
    return this.http.patch(`${this.apiUrl}/sla/tasks/${taskId}/due-date`, { dueDate });
  }

  // Reste disponible, mais les règles s'appliquent maintenant automatiquement
  applyRules(): Observable<any> {
    return this.http.post(`${this.apiUrl}/sla/apply-rules`, {});
  }

  // ───────────── Agent IA SLA (Sprint 8 — US75/76/77) ─────────────

  getProjectRiskAnalysis(projectId: string): Observable<any> {
    return this.http.get(`${this.apiUrl}/sla-agent/risk-analysis`, {
      params: { projectId }
    });
  }

    getLatestWeeklyReport(): Observable<WeeklyReport> {
    return this.http.get<WeeklyReport>(`${this.apiUrl}/sla-agent/weekly-report/latest`);
  }

  generateWeeklyReport(): Observable<WeeklyReport> {
    return this.http.post<WeeklyReport>(`${this.apiUrl}/sla-agent/weekly-report/generate`, {});
  }

  getWeeklyReportHistory(): Observable<WeeklyReportSummary[]> {
    return this.http.get<WeeklyReportSummary[]>(`${this.apiUrl}/sla-agent/weekly-report/history`);
  }

  getWeeklyReport(id: string): Observable<WeeklyReport> {
    return this.http.get<WeeklyReport>(`${this.apiUrl}/sla-agent/weekly-report/${id}`);
  }
}