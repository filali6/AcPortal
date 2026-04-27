import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface Project {
  id: string;
  name: string;
  description: string;
  portfolioDirectorId: string;
  projectManagerId: string;
  createdAt: string;
}

@Injectable({
  providedIn: 'root'
})
export class ProjectsService {

  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  // GET /api/projects → tous les projets (HeadOfCDS)
  getAll(): Observable<Project[]> {
    return this.http.get<Project[]>(`${this.apiUrl}/projects`);
  }

  // GET /api/projects/my → projets du Director connecté
  getMyProjects(): Observable<Project[]> {
    return this.http.get<Project[]>(`${this.apiUrl}/projects/my`);
  }

  // GET /api/projects/managed → projets du ProjectManager connecté
  getManagedProjects(): Observable<Project[]> {
    return this.http.get<Project[]>(`${this.apiUrl}/projects/managed`);
  }

  // POST /api/projects → créer un projet
  create(
    name: string,
    description: string,
    portfolioDirectorId: string
  ): Observable<any> {
    return this.http.post(`${this.apiUrl}/projects`, {
      name,
      description,
      portfolioDirectorId
    });
  }

  // PATCH /api/projects/{id}/assign → affecter un Director
  assignDirector(projectId: string, directorId: string): Observable<any> {
    return this.http.patch(`${this.apiUrl}/projects/${projectId}/assign`, {
      directorId
    });
  }

  // PATCH /api/projects/{id}/assign-manager → affecter un ProjectManager
  assignManager(
    projectId: string,
    projectManagerId: string
  ): Observable<any> {
    return this.http.patch(
      `${this.apiUrl}/projects/${projectId}/assign-manager`,
      { projectManagerId }
    );
  }
}