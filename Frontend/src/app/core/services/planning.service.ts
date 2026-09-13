import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class PlanningService {
  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  generate(formData: FormData): Observable<any> {
    return this.http.post(`${this.apiUrl}/planning/generate`, formData);
  }

 refine(proposalId: string, message: string, currentPlan?: any): Observable<any> {
    return this.http.post(`${this.apiUrl}/planning/${proposalId}/refine`, {
        message,
        currentPlanJson: currentPlan ? JSON.stringify(currentPlan) : null
    });
}

  approve(proposalId: string, currentPlan?: any): Observable<any> {
    return this.http.post(`${this.apiUrl}/planning/${proposalId}/approve`, {
        currentPlanJson: currentPlan ? JSON.stringify(currentPlan) : null
    });
}

  reject(proposalId: string): Observable<any> {
    return this.http.post(`${this.apiUrl}/planning/${proposalId}/reject`, {});
  }

  getByProject(projectId: string): Observable<any> {
    return this.http.get(`${this.apiUrl}/planning/project/${projectId}`);
  }

  getStreamSteps(streamId: string): Observable<any> {
    return this.http.get(`${this.apiUrl}/planning/streams/${streamId}/steps`);
  }

  approveStreamSteps(streamId: string, steps?: any[]): Observable<any> {
    return this.http.post(`${this.apiUrl}/planning/streams/${streamId}/approve-steps`, { steps });
  }
}