import { Component, OnInit, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { AuthService } from '../../services/auth.service';
import { ChatService } from '../../services/chat.service';
import { KeycloakService } from 'keycloak-angular';
import { environment } from '../../../../environments/environment';
import { ChatPanelComponent } from '../chat-panel/chat-panel.component';

@Component({
  selector: 'app-discussions-panel',
  standalone: true,
  imports: [CommonModule, ChatPanelComponent],
  templateUrl: './discussions-panel.component.html',
  styleUrl: './discussions-panel.component.scss'
})
export class DiscussionsPanelComponent implements OnInit {

  @Input() isOpen = false;
  @Output() closed = new EventEmitter<void>();

  streamDiscussions: any[] = [];
  taskDiscussions: any[] = [];

  chatOpen = false;
  chatStreamId: string | null = null;
  chatTaskId: string | null = null;
  chatTitle = '';

  private apiUrl = environment.apiUrl;
  currentUserId = '';

  constructor(
    private http: HttpClient,
    private authService: AuthService,
    private chatService: ChatService,
    private keycloak: KeycloakService
  ) {}

  ngOnInit(): void {
    const userInfo = this.authService.getUserInfo();
    this.currentUserId = userInfo?.sub || userInfo?.id || '';
    this.loadDiscussions();
  }

  loadDiscussions(): void {
    // Charger streams
    this.http.get<any[]>(`${this.apiUrl}/streams/my`).subscribe({
      next: async (streams) => {
        this.streamDiscussions = [];
        for (const stream of streams) {
          // Dernier message du stream
          this.http.get<any[]>(`${this.apiUrl}/chat/stream/${stream.id}`).subscribe({
            next: (messages) => {
              const last = messages[messages.length - 1];
              this.streamDiscussions.push({
                id: stream.id,
                name: stream.name,
                projectName: stream.projectName || '',
                lastMessage: last?.content || 'No messages yet',
                lastMessageTime: last?.createdAt || null,
                senderName: last?.senderName || ''
              });
            }
          });
        }
      }
    });

    // Charger tâches avec stepId
    this.http.get<any[]>(`${this.apiUrl}/tasks/my`).subscribe({
      next: (tasks) => {
        const stepTasks = tasks.filter(t => t.stepId);
        this.taskDiscussions = [];
        for (const task of stepTasks) {
          this.http.get<any[]>(`${this.apiUrl}/chat/task/${task.id}`).subscribe({
            next: (messages) => {
              if (messages.length > 0) {
                const last = messages[messages.length - 1];
                this.taskDiscussions.push({
                  id: task.id,
                  title: task.title,
                  lastMessage: last.content,
                  lastMessageTime: last.createdAt,
                  senderName: last.senderName
                });
              }
            }
          });
        }
      }
    });
  }

  openStreamChat(discussion: any): void {
    this.chatStreamId = discussion.id;
    this.chatTaskId = null;
    this.chatTitle = `${discussion.name} — Team Chat`;
    this.chatOpen = true;
  }

  openTaskChat(discussion: any): void {
    this.chatTaskId = discussion.id;
    this.chatStreamId = null;
    this.chatTitle = discussion.title;
    this.chatOpen = true;
  }

  closeChat(): void {
    this.chatOpen = false;
  }

  close(): void {
    this.closed.emit();
  }

  getTimeAgo(date: string): string {
    if (!date) return '';
    const now = new Date();
    const d = new Date(date);
    const diff = Math.floor((now.getTime() - d.getTime()) / 1000);
    if (diff < 60) return 'just now';
    if (diff < 3600) return `${Math.floor(diff / 60)}m ago`;
    if (diff < 86400) return `${Math.floor(diff / 3600)}h ago`;
    return d.toLocaleDateString('en-GB');
  }
}