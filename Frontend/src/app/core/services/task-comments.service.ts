import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class TaskCommentsService {
  private apiUrl = environment.apiUrl;
  

  constructor(private http: HttpClient) {}

  getComments(taskId: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.apiUrl}/tasks/${taskId}/comments`);
  }

  addComment(taskId: string, content: string, parentCommentId: string | null, mentions: string[]): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/tasks/${taskId}/comments`, {
      content,
      parentCommentId,
      mentions
    });
  }

  deleteComment(taskId: string, commentId: string): Observable<any> {
    return this.http.delete(`${this.apiUrl}/tasks/${taskId}/comments/${commentId}`);
  }
}