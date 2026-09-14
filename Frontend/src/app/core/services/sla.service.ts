import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface SlaRule {
  id: string;
  name: string;
  description?: string;
  type: number; // 0 = Stream, 1 = Task
  slaDays: number;
  createdAt: string;
}

export interface SlaTaskItem {
  id: string;
  title: string;
  status: number;
  dueDate: string;
  assignedTo: string;
  projectId: string;
  streamId: string;
  slaStatus: 'OnTrack' | 'AtRisk' | 'Overdue';
  daysRemaining: number;
}

export interface SlaStreamItem {
  id: string;
  name: string;
  dueDate: string;
  projectName: string;
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
  };
}

@Injectable({
  providedIn: 'root'
})
export class SlaService {

  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  // SLA Rules CRUD
  getAllRules(): Observable<SlaRule[]> {
    return this.http.get<SlaRule[]>(`${this.apiUrl}/sla-rules`);
  }

  createRule(data: {
    name: string;
    description?: string;
    type: number;
    slaDays: number;
  }): Observable<any> {
    return this.http.post(`${this.apiUrl}/sla-rules`, data);
  }

  updateRule(id: string, data: {
    name?: string;
    description?: string;
    type?: number;
    slaDays?: number;
  }): Observable<any> {
    return this.http.patch(`${this.apiUrl}/sla-rules/${id}`, data);
  }

  deleteRule(id: string): Observable<any> {
    return this.http.delete(`${this.apiUrl}/sla-rules/${id}`);
  }

  // Dashboard
  getDashboard(): Observable<SlaDashboard> {
    return this.http.get<SlaDashboard>(`${this.apiUrl}/sla/dashboard`);
  }

  // Consultant tasks with SLA
  getMyTasksWithSla(): Observable<SlaTaskItem[]> {
    return this.http.get<SlaTaskItem[]>(`${this.apiUrl}/sla/my-tasks`);
  }

  // Stream due date
  setStreamDueDate(streamId: string, dueDate: string | null): Observable<any> {
    return this.http.patch(
      `${this.apiUrl}/sla/streams/${streamId}/due-date`,
      { dueDate }
    );
  }

  // Apply rules to all tasks
  applyRules(): Observable<any> {
    return this.http.post(`${this.apiUrl}/sla/apply-rules`, {});
  }

  // ─────────────────────────────────────────────────────────────
  // Agent IA SLA (Sprint 8 — US75/76/77)
  // ─────────────────────────────────────────────────────────────

  // US75/76 — Analyse des risques + recommandations pour un projet (Chef de Projet)
  getProjectRiskAnalysis(projectId: string): Observable<any> {
    return this.http.get(`${this.apiUrl}/sla-agent/risk-analysis`, {
      params: { projectId }
    });
  }

  // US77 — Dernier rapport hebdomadaire généré (Responsable CDS)
  getLatestWeeklyReport(): Observable<any> {
    return this.http.get(`${this.apiUrl}/sla-agent/weekly-report/latest`);
  }

  // US77 — Génération manuelle du rapport hebdomadaire (test / à la demande)
  generateWeeklyReport(): Observable<any> {
    return this.http.post(`${this.apiUrl}/sla-agent/weekly-report/generate`, {});
  }
}