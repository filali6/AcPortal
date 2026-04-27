import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface Plugin {
  id: string;
  name: string;
  description: string;
  category: string;
  icon: string;
  ssoEnabled: boolean;
  accessUrl: string;
  addedAt?: string;
}

@Injectable({ providedIn: 'root' })
export class PluginBridgeService {
  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getAllPlugins(): Observable<Plugin[]> {
    return this.http.get<Plugin[]>(`${this.apiUrl}/plugins`);
  }

  getMyPlugins(): Observable<Plugin[]> {
    return this.http.get<Plugin[]>(`${this.apiUrl}/plugins/user/my`);
  }

  addPlugin(pluginId: string): Observable<any> {
    return this.http.post(`${this.apiUrl}/plugins/user/${pluginId}`, {});
  }

  removePlugin(pluginId: string): Observable<any> {
    return this.http.delete(`${this.apiUrl}/plugins/user/${pluginId}`);
  }
}