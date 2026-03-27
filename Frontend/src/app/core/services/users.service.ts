import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

// Interface → structure d'un user
export interface User {
  id: string;
  fullName: string;
  email: string;
  role: string;
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

  // Filtre les Directors depuis la liste
  getDirectors(users: User[]): User[] {
    return users.filter(u => u.role === 'PortfolioDirector');
  }

  // Filtre les Consultants depuis la liste
  getConsultants(users: User[]): User[] {
    return users.filter(u => u.role === 'Consultant');
  }
}