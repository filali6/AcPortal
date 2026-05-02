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
   getByProject(projectId: string): Observable<any[]> {
  return this.http.get<any[]>(`${this.apiUrl}/streams/project/${projectId}`);
}

  // POST /api/streams → créer un stream
  create(
  name: string,
  projectId: string,
  businessTeamLeadId: string | null,
  technicalTeamLeadId: string | null,
  businessTeamConsultants: string[] = [],
  technicalTeamConsultants: string[] = []
): Observable<any> {
  return this.http.post(`${this.apiUrl}/streams`, {
    name,
    projectId,
    businessTeamLeadId,
    technicalTeamLeadId,
    businessTeamConsultants,
    technicalTeamConsultants
  });
}

  // POST /api/streams/{id}/members → ajouter consultant au stream
 addMember(streamId: string, consultantId: string, teamType: number): Observable<any> {
  return this.http.post(`${this.apiUrl}/streams/${streamId}/members`, {
    consultantId,
    teamType
  });
}
 

removeMember(streamId: string, consultantId: string): Observable<any> {
  return this.http.delete(`${this.apiUrl}/streams/${streamId}/members/${consultantId}`);
}

updateLeads(streamId: string, bizLeadId?: string, techLeadId?: string): Observable<any> {
  return this.http.patch(`${this.apiUrl}/streams/${streamId}/leads`, {
    businessTeamLeadId: bizLeadId || null,
    technicalTeamLeadId: techLeadId || null
  });
}

}