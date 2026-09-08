import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface GitFileDto {
  fileName: string;
  stepName: string;
  toolName: string;
  commitHash: string;
  downloadUrl: string;
  lastModified: string;
}

export interface StepConfigFile {
  id: string;
  fileName: string;
  commitHash: string;
  version: number;
  uploadedAt: string;
  uploadedBy: string;
  stepName: string;
  toolName: string;
}

export interface StreamConfigsResponse {
  repoUrl: string;
  gitFiles: GitFileDto[];
  dbFiles: StepConfigFile[];
}

export interface UploadConfigResponse {
  message: string;
  fileName: string;
  commitHash: string;
  version: number;
  repoUrl: string;
}

@Injectable({
  providedIn: 'root'
})
export class GitService {

  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  // Upload un vrai fichier de config sur un step
  uploadConfig(stepId: string, file: File): Observable<UploadConfigResponse> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<UploadConfigResponse>(
      `${this.apiUrl}/git/steps/${stepId}/config`,
      formData
    );
  }

  // Génère un mock selon l'outil du step
  generateMock(stepId: string): Observable<UploadConfigResponse> {
    return this.http.post<UploadConfigResponse>(
      `${this.apiUrl}/git/steps/${stepId}/config/mock`,
      {}
    );
  }

  // Liste tous les fichiers du repo d'un stream
  getStreamConfigs(streamId: string): Observable<StreamConfigsResponse> {
    return this.http.get<StreamConfigsResponse>(
      `${this.apiUrl}/git/streams/${streamId}/configs`
    );
  }

  // Valide le stream → crée tag v1.0
  validateStream(streamId: string): Observable<any> {
    return this.http.put(
      `${this.apiUrl}/git/streams/${streamId}/validate`,
      {}
    );
  }
}