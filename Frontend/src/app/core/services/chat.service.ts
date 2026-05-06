import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface ChatMessage {
  id: string;
  content: string;
  senderName: string;
  senderKeycloakId: string;
  createdAt: string;
}

@Injectable({ providedIn: 'root' })
export class ChatService {

  private hubConnection: signalR.HubConnection | null = null;
  private messages$ = new BehaviorSubject<ChatMessage[]>([]);
  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

 async startConnection(token: string): Promise<void> {
  if (this.hubConnection?.state === signalR.HubConnectionState.Connected) return;
  if (this.hubConnection?.state === signalR.HubConnectionState.Connecting) return;

  this.hubConnection = new signalR.HubConnectionBuilder()
    .withUrl('http://localhost:5281/hubs/chat', {
      accessTokenFactory: () => token
    })
    .withAutomaticReconnect()
    .build();

  this.hubConnection.on('ReceiveMessage', (message: ChatMessage) => {
    const current = this.messages$.value;
    this.messages$.next([...current, message]);
  });

  try {
    await this.hubConnection.start();
  } catch (err) {
    console.error('Chat connection failed:', err);
  }
}

  async joinStreamChat(streamId: string): Promise<void> {
  await this.hubConnection?.invoke('JoinStreamChat', streamId);
}

  async joinTaskChat(taskId: string): Promise<void> {
    this.messages$.next([]);
    await this.hubConnection?.invoke('JoinTaskChat', taskId);
  }

  async sendStreamMessage(streamId: string, content: string): Promise<void> {
    await this.hubConnection?.invoke('SendStreamMessage', streamId, content);
  }

  async sendTaskMessage(taskId: string, content: string): Promise<void> {
    await this.hubConnection?.invoke('SendTaskMessage', taskId, content);
  }

  getMessages(): Observable<ChatMessage[]> {
    return this.messages$.asObservable();
  }

  loadStreamMessages(streamId: string): void {
    this.http.get<ChatMessage[]>(`${this.apiUrl}/chat/stream/${streamId}`).subscribe({
      next: (messages) => this.messages$.next(messages)
    });
  }

  loadTaskMessages(taskId: string): void {
    this.http.get<ChatMessage[]>(`${this.apiUrl}/chat/task/${taskId}`).subscribe({
      next: (messages) => this.messages$.next(messages)
    });
  }
  async joinAllUserStreams(streamIds: string[]): Promise<void> {
  for (const id of streamIds) {
    await this.hubConnection?.invoke('JoinStreamChat', id);
  }
}
}