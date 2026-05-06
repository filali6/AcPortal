import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class WorkflowService {

  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getRules(): Observable<any[]> {
    return this.http.get<any[]>(`${this.apiUrl}/workflow`);
  }

  updateRules(config: any): Observable<any> {
    return this.http.put(`${this.apiUrl}/workflow`, config);
  }

  getActionTypes(): Observable<string[]> {
    return this.http.get<string[]>(`${this.apiUrl}/workflow/action-types`);
  }

  getTargetTypes(): Observable<string[]> {
    return this.http.get<string[]>(`${this.apiUrl}/workflow/target-types`);
  }
}