import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { AuthService } from '../../core/services/auth.service';
import { ChatService, ChatMessage } from '../../core/services/chat.service';
import { KeycloakService } from 'keycloak-angular';
import { environment } from '../../../environments/environment';
import { LucideAngularModule, MessageSquare, Hash, User } from 'lucide-angular';
import { ActivatedRoute } from '@angular/router';

@Component({
    selector: 'app-discussions',
    standalone: true,
    imports: [CommonModule, FormsModule, LucideAngularModule],
    templateUrl: './discussions.component.html',
    styleUrl: './discussions.component.scss'
})
export class DiscussionsComponent implements OnInit, OnDestroy {

    streamDiscussions: any[] = [];
    taskDiscussions: any[] = [];

    selectedConversation: any = null;
    selectedType: 'stream' | 'task' | null = null;

    messages: ChatMessage[] = [];
    newMessage = '';
    currentUserId = '';
    currentUserName = '';

    loading = false;
    loadingMessages = false;
    summarizing = false;
chatSummary: string | null = null;
summaryOpen = true;

    private apiUrl = environment.apiUrl;

    readonly MessageSquare = MessageSquare;
    readonly Hash = Hash;
    readonly User = User;

    constructor(
        private http: HttpClient,
        private authService: AuthService,
        private chatService: ChatService,
        private keycloak: KeycloakService,
        private route: ActivatedRoute
    ) {}

    ngOnInit(): void {
        const userInfo = this.authService.getUserInfo();
        this.currentUserId = userInfo?.sub || userInfo?.id || '';
        this.currentUserName = userInfo?.name || '';

        const token = this.keycloak.getKeycloakInstance().token || '';
        this.chatService.startConnection(token).then(() => {
            this.loadDiscussions();
        });

        this.chatService.getMessages().subscribe(messages => {
            this.messages = messages;
        });
        this.route.queryParams.subscribe(params => {
  if (params['type'] && params['id']) {
    // attendre que les discussions soient chargées
    setTimeout(() => {
      if (params['type'] === 'stream') {
        const d = this.streamDiscussions.find(s => s.id === params['id']);
        if (d) this.selectStream(d);
      } else {
        const d = this.taskDiscussions.find(t => t.id === params['id']);
        if (d) this.selectTask(d);
      }
    }, 1000);
  }
});
    }

    ngOnDestroy(): void {}

    loadDiscussions(): void {
        this.loading = true;
        this.http.get<any[]>(`${this.apiUrl}/streams/my`).subscribe({
            next: (streams) => {
                this.streamDiscussions = [];
                for (const stream of streams) {
                    this.chatService.joinStreamChat(stream.id);
                    this.http.get<any[]>(`${this.apiUrl}/chat/stream/${stream.id}`).subscribe({
                        next: (messages) => {
                            const last = messages[messages.length - 1];
                            this.streamDiscussions.push({
                                id: stream.id,
                                name: stream.name,
                                projectName: stream.projectName || '',
                                lastMessage: last?.content || 'No messages yet',
                                lastMessageTime: last?.createdAt || null,
                                senderName: last?.senderName || '',
                                unreadCount: 0
                            });
                        }
                    });

                    this.http.get<any[]>(`${this.apiUrl}/tasks/stream/${stream.id}`).subscribe({
                        next: (tasks) => {
                            for (const task of tasks) {
                                this.http.get<any[]>(`${this.apiUrl}/chat/task/${task.id}`).subscribe({
                                    next: (messages) => {
                                        const last = messages[messages.length - 1];
                                        const already = this.taskDiscussions.find(d => d.id === task.id);
                                        if (!already && messages.length > 0) {
                                            this.taskDiscussions.push({
                                                id: task.id,
                                                title: task.title,
                                                lastMessage: last?.content || '',
                                                lastMessageTime: last?.createdAt || null,
                                                senderName: last?.senderName || '',
                                                unreadCount: 0
                                            });
                                        }
                                    }
                                });
                            }
                        }
                    });
                }
                this.loading = false;
            }
        });
    }

    selectStream(discussion: any): void {
        this.selectedConversation = discussion;
        this.selectedType = 'stream';
        this.chatService.loadStreamMessages(discussion.id);
    }

    selectTask(discussion: any): void {
        this.selectedConversation = discussion;
        this.selectedType = 'task';
        this.chatService.loadTaskMessages(discussion.id);
        this.chatService.joinTaskChat(discussion.id);
    }

    async sendMessage(): Promise<void> {
        if (!this.newMessage.trim() || !this.selectedConversation) return;
        const content = this.newMessage.trim();
        this.newMessage = '';

        if (this.selectedType === 'stream') {
            await this.chatService.sendStreamMessage(this.selectedConversation.id, content);
        } else if (this.selectedType === 'task') {
            await this.chatService.sendTaskMessage(this.selectedConversation.id, content);
        }
    }

    isMyMessage(msg: ChatMessage): boolean {
        return msg.senderKeycloakId === this.currentUserId;
    }

    getInitials(name: string): string {
        if (!name) return '?';
        return name.split(' ').map(w => w[0]).join('').toUpperCase().slice(0, 2);
    }

    getTime(date: string): string {
        return new Date(date).toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' });
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

    getConversationTitle(): string {
        if (!this.selectedConversation) return '';
        return this.selectedType === 'stream'
            ? this.selectedConversation.name
            : this.selectedConversation.title;
    }
    summarizeChat(): void {
  this.summarizing = true;
  this.chatSummary = null;

  const body = this.selectedType === 'stream'
    ? { streamId: this.selectedConversation.id }
    : { taskId: this.selectedConversation.id };

  this.http.post<{ summary: string }>(`${this.apiUrl}/chat/summarize`, body)
    .subscribe({
      next: (res) => {
        this.chatSummary = res.summary;
        this.summaryOpen = true;
        this.summarizing = false;
      },
      error: () => this.summarizing = false
    });
}
}