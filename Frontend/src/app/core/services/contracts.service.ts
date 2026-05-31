import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
export interface Contract {
  id: string;
  clientName: string;
  description: string;
  status: number;
  createdAt: string;
  projectId: string | null;
  filesPaths: string[];
  summary: string | null;
  extractedText: string | null;
  summarizedAt: string | null;
  summaryStatus: number; // 0=None, 1=Pending, 2=Completed, 3=Failed
}
@Injectable({ providedIn: 'root' })
export class ContractsService {
  private api = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getMyContracts(): Observable<any> {
    return this.http.get(`${this.api}/contracts/my`);
  }

  getById(id: string): Observable<any> {
    return this.http.get(`${this.api}/contracts/${id}`);
  }

  create(formData: FormData): Observable<any> {
    return this.http.post(`${this.api}/contracts`, formData);
  }

  addFiles(contractId: string, formData: FormData): Observable<any> {
    return this.http.patch(`${this.api}/contracts/${contractId}/files`, formData);
  }

  getFileUrl(fileName: string): string {
    return `${this.api}/contracts/files/${fileName}`;
  }
  update(contractId: string, formData: FormData): Observable<any> {
  return this.http.put(`${this.api}/contracts/${contractId}`, formData);
}

deleteFile(contractId: string, fileName: string): Observable<any> {
  return this.http.delete(`${this.api}/contracts/${contractId}/files/${encodeURIComponent(fileName)}`);
}
getAll(): Observable<any[]> {
  return this.http.get<any[]>(`${this.api}/contracts/all`);
}
}