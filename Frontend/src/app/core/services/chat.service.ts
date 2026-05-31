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
  taskId?: string;
  streamId?: string;
}

@Injectable({ providedIn: 'root' })
export class ChatService {

  private hubConnection: signalR.HubConnection | null = null;
  private messages$ = new BehaviorSubject<ChatMessage[]>([]);
  private apiUrl = environment.apiUrl;
  private unreadTaskIds$ = new BehaviorSubject<Set<string>>(new Set());
  private unreadDiscussions$ = new BehaviorSubject<number>(0);
  private currentUserId = '';

  constructor(private http: HttpClient) {}

  setCurrentUser(id: string): void {
    this.currentUserId = id;
  }

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

      // ignorer ses propres messages pour les unread
      if (message.senderKeycloakId === this.currentUserId) return;

      if (message.taskId) {
        const ids = new Set(this.unreadTaskIds$.value);
        ids.add(message.taskId);
        this.unreadTaskIds$.next(ids);
        this.unreadDiscussions$.next(this.unreadDiscussions$.value + 1);
      } else if (message.streamId) {
        this.unreadDiscussions$.next(this.unreadDiscussions$.value + 1);
      }
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

  markTaskAsRead(taskId: string): void {
    const ids = new Set(this.unreadTaskIds$.value);
    ids.delete(taskId);
    this.unreadTaskIds$.next(ids);
  }

  clearDiscussionsBadge(): void {
    this.unreadDiscussions$.next(0);
  }

  getUnreadTaskIds(): Observable<Set<string>> {
    return this.unreadTaskIds$.asObservable();
  }

  getUnreadDiscussionsCount(): Observable<number> {
    return this.unreadDiscussions$.asObservable();
  }
  async joinTaskChatSilent(taskId: string): Promise<void> {
  await this.hubConnection?.invoke('JoinTaskChat', taskId);
}
}