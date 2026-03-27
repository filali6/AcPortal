import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

// Interface → structure d'un événement
export interface AcpEvent {
  id: string;
  toolName: string;
  eventType: string;
  receivedAt: string;
  generatedTaskId: string;
}

// Interface → requête pour publier un événement
export interface PublishEventRequest {
  toolName: string;
  eventType: string;
  projectId?:string;
}

@Injectable({
  providedIn: 'root'
})
export class EventsService {

  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  // POST /api/events/publish → publier un événement
  // publish(toolName: string, eventType: string): Observable<any> {
  //   const request: PublishEventRequest = { toolName, eventType };
  //   return this.http.post(`${this.apiUrl}/events/publish`, request);
  // }
   publish(toolName: string, eventType: string, projectId?: string): Observable<any> {
    const request: PublishEventRequest = { toolName, eventType, projectId };
    return this.http.post(`${this.apiUrl}/events/publish`, request);
  }

  // GET /api/events → liste tous les événements
  getAll(): Observable<AcpEvent[]> {
    return this.http.get<AcpEvent[]>(`${this.apiUrl}/events`);
  }

  // GET /api/events/{id} → détail d'un événement
  getById(id: string): Observable<AcpEvent> {
    return this.http.get<AcpEvent>(`${this.apiUrl}/events/${id}`);
  }
}