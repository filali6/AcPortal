import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface PluginDto {
  id: string;
  name: string;
  description: string;
  category: string;
  accessUrl: string;
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

 

  update(id: string, plugin: PluginDto): Observable<any> {
    return this.http.put(`${this.apiUrl}/plugins/${id}`, plugin);
  }

  
}