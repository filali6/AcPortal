import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

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
}