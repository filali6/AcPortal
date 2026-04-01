import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface Stream {
  id: string;
  name: string;
  projectId: string;
  businessTeamLeadId: string;
  technicalTeamLeadId: string;
  createdAt: string;
}

@Injectable({
  providedIn: 'root'
})
export class StreamsService {

  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  // GET /api/streams/my → streams du lead connecté
  getMyStreams(): Observable<Stream[]> {
    return this.http.get<Stream[]>(`${this.apiUrl}/streams/my`);
  }

  // GET /api/streams/project/{projectId} → streams d'un projet
  getByProject(projectId: string): Observable<Stream[]> {
    return this.http.get<Stream[]>(
      `${this.apiUrl}/streams/project/${projectId}`
    );
  }

  // POST /api/streams → créer un stream
  create(
    name: string,
    projectId: string,
    businessTeamLeadId: string | null,
    technicalTeamLeadId: string | null
  ): Observable<any> {
    return this.http.post(`${this.apiUrl}/streams`, {
      name,
      projectId,
      businessTeamLeadId,
      technicalTeamLeadId
    });
  }

  // POST /api/streams/{id}/members → ajouter consultant au stream
  addMember(streamId: string, consultantId: string): Observable<any> {
    return this.http.post(
      `${this.apiUrl}/streams/${streamId}/members`,
      { consultantId }
    );
  }
}