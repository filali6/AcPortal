import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface User {
  id: string;
  fullName: string;
  email: string;
  role: string;
  projectCount?: number;
  streamCount?: number;
}

@Injectable({
  providedIn: 'root'
})
export class UsersService {

  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  // GET /api/auth/users → tous les users
  getAll(): Observable<User[]> {
    return this.http.get<User[]>(`${this.apiUrl}/auth/users`);
  }

  // GET /api/auth/users/project-managers → PM avec projectCount
  getProjectManagers(): Observable<User[]> {
    return this.http.get<User[]>(
      `${this.apiUrl}/auth/users/project-managers`
    );
  }

  // GET /api/auth/users/leads → leads avec streamCount
  getLeads(): Observable<User[]> {
    return this.http.get<User[]>(`${this.apiUrl}/auth/users/leads`);
  }

  // helpers locaux
  getDirectors(users: User[]): User[] {
    return users.filter(u => u.role === 'PortfolioDirector');
  }

  getConsultants(users: User[]): User[] {
    return users.filter(u => u.role === 'Consultant');
  }
}