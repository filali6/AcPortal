import { Component, Input, OnInit, OnChanges, SimpleChanges, ViewChild, ElementRef, AfterViewChecked } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ChatService, ChatMessage } from '../../services/chat.service';
import { AuthService } from '../../services/auth.service';
import { KeycloakService } from 'keycloak-angular';
@Component({
  selector: 'app-chat-panel',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './chat-panel.component.html',
  styleUrl: './chat-panel.component.scss'
})
export class ChatPanelComponent implements OnInit, OnChanges, AfterViewChecked {

  @Input() streamId: string | null = null;
  @Input() taskId: string | null = null;
  @Input() title: string = 'Chat';
  @Input() isOpen: boolean = false;

  @ViewChild('messagesEnd') messagesEnd!: ElementRef;

  messages: ChatMessage[] = [];
  newMessage = '';
  currentUserId = '';

  constructor(
    private chatService: ChatService,
    private authService: AuthService,
    private keycloak:KeycloakService
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
}