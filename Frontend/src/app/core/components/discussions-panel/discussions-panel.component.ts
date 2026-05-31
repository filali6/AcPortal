import { Component, OnInit, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { ChatService } from '../../services/chat.service';  // ← ajoute
import { TranslateModule } from '@ngx-translate/core';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-discussions-panel',
  standalone: true,
  imports: [CommonModule, TranslateModule],
  templateUrl: './discussions-panel.component.html',
  styleUrl: './discussions-panel.component.scss'
})
export class DiscussionsPanelComponent implements OnInit {

  @Input() isOpen = false;
  @Output() closed = new EventEmitter<void>();

  streamDiscussions: any[] = [];
  taskDiscussions: any[] = [];

  private apiUrl = environment.apiUrl;

  constructor(
    private http: HttpClient,
    private authService: AuthService,
    private router: Router,
    private chatService: ChatService  // ← ajoute
  ) {}

  ngOnInit(): void {
    this.loadDiscussions();

    // ← ajoute tout ça
    this.chatService.getMessages().subscribe(messages => {
      const last = messages[messages.length - 1];
      if (!last) return;

      if (last.streamId) {
        const d = this.streamDiscussions.find(s => s.id === last.streamId);
        if (d) {
          d.lastMessage = last.content;
          d.lastMessageTime = last.createdAt;
          d.senderName = last.senderName;
          d.hasNewMessage = true;
          this.streamDiscussions.sort((a, b) =>
            new Date(b.lastMessageTime || 0).getTime() - new Date(a.lastMessageTime || 0).getTime()
          );
        }
      }

      if (last.taskId) {
        const d = this.taskDiscussions.find(t => t.id === last.taskId);
        if (d) {
          d.lastMessage = last.content;
          d.lastMessageTime = last.createdAt;
          d.senderName = last.senderName;
          d.hasNewMessage = true;
          this.taskDiscussions.sort((a, b) =>
            new Date(b.lastMessageTime || 0).getTime() - new Date(a.lastMessageTime || 0).getTime()
          );
        }
      }
    });
  }

  loadDiscussions(): void {
    this.http.get<any[]>(`${this.apiUrl}/streams/my`).subscribe({
      next: (streams) => {
        this.streamDiscussions = [];
        for (const stream of streams) {
          this.http.get<any[]>(`${this.apiUrl}/chat/stream/${stream.id}`).subscribe({
            next: (messages) => {
              const last = messages[messages.length - 1];
              this.streamDiscussions.push({
                id: stream.id,
                name: stream.name,
                projectName: stream.projectName || '',
                lastMessage: last?.content || '',
                lastMessageTime: last?.createdAt || null,
                senderName: last?.senderName || '',
                hasNewMessage: false
              });
              this.streamDiscussions.sort((a, b) =>
                new Date(b.lastMessageTime || 0).getTime() - new Date(a.lastMessageTime || 0).getTime()
              );
            }
          });

          this.http.get<any[]>(`${this.apiUrl}/tasks/stream/${stream.id}`).subscribe({
            next: (tasks) => {
              for (const task of tasks) {
                this.http.get<any[]>(`${this.apiUrl}/chat/task/${task.id}`).subscribe({
                  next: (messages) => {
                    if (messages.length > 0) {
                      const last = messages[messages.length - 1];
                      const already = this.taskDiscussions.find(d => d.id === task.id);
                      if (!already) {
                        this.taskDiscussions.push({
                          id: task.id,
                          title: task.title,
                          lastMessage: last?.content || '',
                          lastMessageTime: last?.createdAt || null,
                          senderName: last?.senderName || '',
                          hasNewMessage: false
                        });
                        this.taskDiscussions.sort((a, b) =>
                          new Date(b.lastMessageTime || 0).getTime() - new Date(a.lastMessageTime || 0).getTime()
                        );
                      }
                    }
                  }
                });
              }
            }
          });
        }
      }
    });
  }

  openDiscussion(type: 'stream' | 'task', id: string): void {
    // reset hasNewMessage
    if (type === 'stream') {
      const d = this.streamDiscussions.find(s => s.id === id);
      if (d) d.hasNewMessage = false;
    } else {
      const d = this.taskDiscussions.find(t => t.id === id);
      if (d) d.hasNewMessage = false;
    }
    this.close();
    this.router.navigate(['/discussions'], { queryParams: { type, id } });
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