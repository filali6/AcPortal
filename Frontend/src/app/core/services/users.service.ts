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
  getAllForAdmin(): Observable<User[]> {
  return this.http.get<User[]>(`${this.apiUrl}/auth/users/all`);
}

createUser(data: { fullName: string, email: string, password: string, role: string }): Observable<any> {
  return this.http.post(`${this.apiUrl}/auth/users`, {
    fullName: data.fullName,
    email: data.email,
    password: data.password,
    role: this.getRoleNumber(data.role)
  });
}

updateUser(id: string, data: { fullName?: string, role?: string }): Observable<any> {
  return this.http.patch(`${this.apiUrl}/auth/users/${id}`, data);
}

deleteUser(id: string): Observable<any> {
  return this.http.delete(`${this.apiUrl}/auth/users/${id}`);
}
private getRoleNumber(role: string): number {
  const roles: { [key: string]: number } = {
    'HeadOfCDS': 0,
    'PortfolioDirector': 1,
    'ProjectManager': 2,
    'BusinessTeamLead': 3,
    'TechnicalTeamLead': 4,
    'Consultant': 5,
    'DAF': 6,
    'SuperAdmin': 7
  };
  return roles[role] ?? 5;
}
}