import { Component, OnInit, HostListener, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { NotificationService } from '../../services/notification.service';
import { environment } from '../../../../environments/environment';
import { LucideAngularModule, Bell } from 'lucide-angular';

@Component({
  selector: 'app-notifications-dropdown',
  standalone: true,
  imports: [CommonModule, LucideAngularModule],
  templateUrl: './notifications-dropdown.component.html',
  styleUrl: './notifications-dropdown.component.scss'
})
export class NotificationsDropdownComponent implements OnInit {

  notifications: any[] = [];
  groupedNotifs: { label: string, items: any[] }[] = [];
  unreadCount = 0;
  isOpen = false;
  private apiUrl = environment.apiUrl;
  readonly Bell = Bell;

  constructor(
    private http: HttpClient,
    private notificationService: NotificationService,
    private elementRef: ElementRef
  ) {}

  ngOnInit(): void {
    this.loadNotifications();
    this.notificationService.notifications$.subscribe(() => {
      this.loadNotifications();
    });
  }

  loadNotifications(): void {
    this.http.get<any[]>(`${this.apiUrl}/notifications`).subscribe({
      next: (notifs) => {
        this.notifications = notifs;
        this.unreadCount = notifs.filter(n => !n.isRead).length;
        this.computeGroups();
      }
    });
  }

  computeGroups(): void {
    const groups: { [key: string]: any[] } = {};
    this.notifications.forEach(n => {
      const label = this.getDateLabel(n.createdAt);
      if (!groups[label]) groups[label] = [];
      groups[label].push(n);
    });
    this.groupedNotifs = Object.keys(groups).map(label => ({
      label,
      items: groups[label]
    }));
  }

  toggleDropdown(): void {
    this.isOpen = !this.isOpen;
  }

  @HostListener('document:click', ['$event'])
  onClickOutside(event: Event): void {
    if (!this.elementRef.nativeElement.contains(event.target)) {
      this.isOpen = false;
    }
  }

  markAsRead(id: string): void {
    this.http.patch(`${this.apiUrl}/notifications/${id}/read`, {}).subscribe({
      next: () => {
        const notif = this.notifications.find(n => n.id === id);
        if (notif) notif.isRead = true;
        this.unreadCount = this.notifications.filter(n => !n.isRead).length;
        this.computeGroups();
      }
    });
  }

  markAllAsRead(): void {
    this.http.patch(`${this.apiUrl}/notifications/read-all`, {}).subscribe({
      next: () => {
        this.notifications.forEach(n => n.isRead = true);
        this.unreadCount = 0;
        this.computeGroups();
      }
    });
  }

  getTimeAgo(date: string): string {
    const now = new Date();
    const d = new Date(date);
    const diff = Math.floor((now.getTime() - d.getTime()) / 1000);
    if (diff < 60) return 'just now';
    if (diff < 3600) return `${Math.floor(diff / 60)}m ago`;
    if (diff < 86400) return `${Math.floor(diff / 3600)}h ago`;
    return d.toLocaleDateString('en-GB');
  }

  getDateLabel(date: string): string {
    const d = new Date(date);
    const today = new Date();
    const yesterday = new Date();
    yesterday.setDate(today.getDate() - 1);
    if (d.toDateString() === today.toDateString()) return 'TODAY';
    if (d.toDateString() === yesterday.toDateString()) return 'YESTERDAY';
    return d.toLocaleDateString('en-GB');
  }

  getInitials(name: string): string {
    return name.split(' ').map(w => w[0]).join('').toUpperCase().slice(0, 2);
  }
}