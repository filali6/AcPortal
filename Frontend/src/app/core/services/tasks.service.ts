import { Injectable } from '@angular/core';
import {HttpClient} from '@angular/common/http';
import {Observable} from 'rxjs';
import { environment } from '../../../environments/environment';

export interface Task {
  id: string;
  title: string;
  description: string;
  toolName: string;
  status: number;       // 0=Pending, 3=Done
  assignedTo: string;
  createdAt: string;
  sourceEventId: string;
  projectId?:string;
  stepId?: string;
  streamId?:string;
  
}
export interface UpdateStatusRequest {
  status: number;
}
@Injectable({
  providedIn: 'root'
})
export class TasksService {
  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  // GET /api/tasks → liste toutes les tâches
  getAll(): Observable<Task[]> {
    return this.http.get<Task[]>(`${this.apiUrl}/tasks`);
  }

  // GET /api/tasks/{id} → détail d'une tâche
  getById(id: string): Observable<Task> {
    return this.http.get<Task>(`${this.apiUrl}/tasks/${id}`);
  }

  // PATCH /api/tasks/{id}/status → changer le statut
  updateStatus(id: string, status: number): Observable<any> {
    return this.http.patch(`${this.apiUrl}/tasks/${id}/status`, { status });
  }

  // PATCH /api/tasks/{id}/assign → assigner à un utilisateur
  assign(id: string, assignedTo: string): Observable<any> {
    return this.http.patch(`${this.apiUrl}/tasks/${id}/assign`, { assignedTo });
  }

  // Helper → convertit le numéro de statut en texte
  getStatusLabel(status: number): string {
    switch(status) {
        case 0: return 'Pending';
        case 1: return 'Bloquée';
        case 2: return 'Done';
        default: return 'Unknown';
    }
}


  // Helper → retourne la couleur selon le statut
  getStatusColor(status: number): string {
    switch(status) {
        case 0: return '#f59e0b'; // orange → Pending
        case 1: return '#ef4444'; // rouge  → Bloquée
        case 2: return '#10b981'; // vert   → Done
        default: return '#6b7280';
    }
}
  getMyTasks(): Observable<Task[]> {
  return this.http.get<Task[]>(`${this.apiUrl}/tasks/my`);
}

   
}
