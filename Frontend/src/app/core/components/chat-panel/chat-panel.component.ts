import { Component, Input, OnInit, OnChanges, SimpleChanges, ViewChild, ElementRef, AfterViewChecked , Output, EventEmitter} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ChatService, ChatMessage } from '../../services/chat.service';
import { TaskCommentsService } from '../../services/task-comments.service';
import { AuthService } from '../../services/auth.service';
import { KeycloakService } from 'keycloak-angular';
import { TranslateModule } from '@ngx-translate/core';
import { HttpClient, HttpClientModule } from '@angular/common/http';
import { environment } from '../../../../environments/environment';
@Component({
  selector: 'app-chat-panel',
  standalone: true,
  imports: [CommonModule, FormsModule,TranslateModule],
  templateUrl: './chat-panel.component.html',
  styleUrl: './chat-panel.component.scss'
})
export class ChatPanelComponent implements OnInit, OnChanges, AfterViewChecked {

  @Input() streamId: string | null = null;
  @Input() taskId: string | null = null;
  @Input() title: string = 'Chat';
  @Input() isOpen: boolean = false;
  @Input() taskStatus: number | null = null;
  @Input() messagingChannelUrl: string | null = null;
@Input() toolName: string | null = null

  @ViewChild('messagesEnd') messagesEnd!: ElementRef;

  @Output() closed=new EventEmitter<void>();

  messages: ChatMessage[] = [];
  newMessage = '';
  currentUserId = '';
  minimized=false;
  summarizing = false;
chatSummary: string | null = null;
summaryOpen = true;

// Comments
comments: any[] = [];
newComment = '';
replyingTo: any = null;
mentions: string[] = [];

// @mention
showMentions = false;
mentionQuery = '';
streamMembers: any[] = [];
filteredMembers: any[] = [];

  constructor(
    private chatService: ChatService,
    private authService: AuthService,
    private keycloak:KeycloakService,
    private taskCommentsService:TaskCommentsService,
    private http: HttpClient
  ) {}

 ngOnInit(): void {
  const userInfo = this.authService.getUserInfo();
  this.currentUserId = userInfo?.sub || userInfo?.id || '';

  const token = this.keycloak.getKeycloakInstance().token || '';
  
  this.chatService.startConnection(token).then(() => {
    // Rejoindre le groupe même si le panel est fermé
    if (this.streamId) {
      this.chatService.joinStreamChat(this.streamId);
      this.chatService.loadStreamMessages(this.streamId);
    } else if (this.taskId) {
      this.chatService.joinTaskChat(this.taskId);
      this.chatService.loadTaskMessages(this.taskId);
    }
  });
  this.initChatConnection();

  this.chatService.getMessages().subscribe(messages => {
    this.messages = messages;
  });
}
  ngOnChanges(changes: SimpleChanges): void {
  if ((changes['streamId'] || changes['taskId']) && (this.streamId || this.taskId)) {
    this.loadMessages();
  }
  if (changes['taskId'] && this.taskId) {
    this.loadComments();
  }
  if (changes['streamId'] && this.streamId) {
    this.loadStreamMembers();
  }
}

  ngAfterViewChecked(): void {
    this.scrollToBottom();
  }

  loadMessages(): void {
    if (this.streamId) {
      this.chatService.loadStreamMessages(this.streamId);
      this.chatService.joinStreamChat(this.streamId);
    } else if (this.taskId) {
      this.chatService.loadTaskMessages(this.taskId);
      this.chatService.joinTaskChat(this.taskId);
    }
  }

  async sendMessage(): Promise<void> {
    if (!this.newMessage.trim()) return;
    const content = this.newMessage.trim();
    this.newMessage = '';

    if (this.streamId) {
      await this.chatService.sendStreamMessage(this.streamId, content);
    } else if (this.taskId) {
      await this.chatService.sendTaskMessage(this.taskId, content);
    }
  }

  scrollToBottom(): void {
    try {
      this.messagesEnd?.nativeElement.scrollIntoView({ behavior: 'smooth' });
    } catch {}
  }

  getInitials(name: string): string {
    return name.split(' ').map(w => w[0]).join('').toUpperCase().slice(0, 2);
  }

  getTime(date: string): string {
    return new Date(date).toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' });
  }

  isMyMessage(msg: ChatMessage): boolean {
    return msg.senderKeycloakId === this.currentUserId;
  }
   private async initChatConnection(): Promise<void> {
  const token = this.keycloak.getKeycloakInstance().token || '';
  await this.chatService.startConnection(token);
}
close():void{this.closed.emit();}
getStatusColor(status: number): string {
    const colors: { [key: number]: string } = {
        0: '#f59e0b',
        1: '#ef4444',
        2: '#10b981'
    };
    return colors[status] || '#9ca3af';
}
openExternalLink(url: string): void {
    window.open(url, '_blank');
}
getStatusLabel(status: number): string {
    const labels: { [key: number]: string } = {
        0: 'Pending',
        1: 'Blocked',
        2: 'Done'
    };
    return labels[status] || '—';
}
summarizeChat(): void {
  this.summarizing = true;
  this.chatSummary = null;

  const body = this.streamId
    ? { streamId: this.streamId }
    : { taskId: this.taskId };

  this.http.post<{ summary: string }>(`${environment.apiUrl}/chat/summarize`, body)
    .subscribe({
      next: (res) => {
        this.chatSummary = res.summary;
        this.summaryOpen = true;
        this.summarizing = false;
      },
      error: () => this.summarizing = false
    });
}
loadComments(): void {
  if (!this.taskId) return;
  this.taskCommentsService.getComments(this.taskId)
    .subscribe({ next: (data) => this.comments = data });
}

loadStreamMembers(): void {
  if (!this.streamId) return;
  this.http.get<any[]>(`${environment.apiUrl}/streams/${this.streamId}/members`)
    .subscribe({ next: (data) => this.streamMembers = data });
}

postComment(): void {
  if (!this.newComment.trim() || !this.taskId) return;
  this.taskCommentsService.addComment(
    this.taskId,
    this.newComment.trim(),
    this.replyingTo?.id ?? null,
    this.mentions
  ).subscribe({
    next: (comment) => {
      if (this.replyingTo) {
        const parent = this.comments.find(c => c.id === this.replyingTo.id);
        if (parent) parent.replies = [...(parent.replies || []), comment];
      } else {
        this.comments = [...this.comments, { ...comment, replies: [] }];
      }
      this.newComment = '';
      this.replyingTo = null;
      this.mentions = [];
    }
  });
}

onCommentInput(event: Event): void {
  const value = (event.target as HTMLInputElement).value;
  const atIndex = value.lastIndexOf('@');
  if (atIndex !== -1) {
    this.mentionQuery = value.slice(atIndex + 1).toLowerCase();
    this.filteredMembers = this.streamMembers.filter(m =>
      m.fullName.toLowerCase().includes(this.mentionQuery)
    );
    this.showMentions = this.filteredMembers.length > 0;
  } else {
    this.showMentions = false;
  }
}

selectMention(member: any): void {
  const atIndex = this.newComment.lastIndexOf('@');
  this.newComment = this.newComment.slice(0, atIndex) + '@' + member.fullName + ' ';
  this.mentions.push(member.keycloakId);
  this.showMentions = false;
}

setReply(comment: any): void {
  this.replyingTo = comment;
}

cancelReply(): void {
  this.replyingTo = null;
}

getInitialsFrom(name: string): string {
  return name.split(' ').map((w: string) => w[0]).join('').toUpperCase().slice(0, 2);
}

deleteComment(commentId: string): void {
  if (!this.taskId) return;
  this.taskCommentsService.deleteComment(this.taskId, commentId)
    .subscribe({
      next: () => {
        this.comments = this.comments.filter(c => c.id !== commentId);
      }
    });
}
highlightMentions(content: string): string {
  return content.replace(/@([A-Za-zÀ-ÿ]+(?: [A-Za-zÀ-ÿ]+)*)/g,
    '<span class="mention-highlight">@$1</span>');
}
}