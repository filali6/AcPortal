import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface PluginDto {
  id: string;
  name: string;
  description: string;
  category: string;
  url: string;
  icon: string;
  ssoEnabled: boolean;
  isActive: boolean;
  allowedRoles: string[];
}

@Injectable({ providedIn: 'root' })
export class PluginsAdminService {

  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

 getAll(): Observable<PluginDto[]> {
    return this.http.get<PluginDto[]>(`${this.apiUrl}/plugins/all`);
}

create(plugin: PluginDto): Observable<any> {
    return this.http.post(`${this.apiUrl}/plugins`, plugin);
  }

 

  update(id: string, plugin: PluginDto): Observable<any> {
    return this.http.put(`${this.apiUrl}/plugins/${id}`, plugin);
  }

  delete(id: string): Observable<any> {
    return this.http.delete(`${this.apiUrl}/plugins/${id}`);
  }

  
}