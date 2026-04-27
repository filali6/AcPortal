import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { ProjectsService, Project } from '../../core/services/projects.service';
import { TasksService, Task } from '../../core/services/tasks.service';
import { NotificationService } from '../../core/services/notification.service';
import { AuthService } from '../../core/services/auth.service';
import { UsersService } from '../../core/services/users.service';
import { TabsService } from '../../core/services/tabs.service';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-director',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './director.component.html',
  styleUrl: './director.component.scss'
})
export class DirectorComponent implements OnInit {

  activeTabId = 'tasks';
  myTasks: Task[] = [];
  projects: Project[] = [];
  managers: any[] = [];
  selectedIds: Set<string> = new Set();
  openTabs: { [tabId: string]: { task: any, selectedManagerId: string } } = {};

  loading = false;
  successMessage = '';
  errorMessage = '';
  currentUserName = '';
  currentUserId = '';
  private api = environment.apiUrl;

  constructor(
    private projectsService: ProjectsService,
    private tasksService: TasksService,
    private notificationService: NotificationService,
    private authService: AuthService,
    private http: HttpClient,
    private usersService: UsersService,
    private tabsService: TabsService
  ) {}

  ngOnInit(): void {
    const userInfo = this.authService.getUserInfo();
    this.currentUserName = userInfo?.name || '';
    this.currentUserId = userInfo?.id || '';
    this.loadAll();
    this.notificationService.notifications$.subscribe(() => this.refreshTasks());
    this.tabsService.activeTabId.subscribe(id => this.activeTabId = id);
  }

  loadAll(): void {
    this.projectsService.getAll().subscribe({ next: (p) => this.projects = p });
    this.refreshTasks();
    this.usersService.getProjectManagers().subscribe({ next: (m) => this.managers = m });
  }

  refreshTasks(): void {
    this.tasksService.getAll().subscribe({
      next: (tasks) => {
        this.myTasks = tasks.filter(t => t.assignedTo === this.currentUserId);
      }
    });
  }

  onTaskClick(task: any): void {
    if (task.status === 2) return;
    const tabId = `assign-manager-${task.id}`;
    if (!this.openTabs[tabId]) {
      this.openTabs[tabId] = { task, selectedManagerId: '' };
    }
    this.tabsService.openTab({
      id: tabId,
      title: task.title,
      type: 'assign-manager',
      data: task
    });
  }

  getTabData(tabId: string) {
    return this.openTabs[tabId] || null;
  }

  getOpenTabIds(): string[] {
    return Object.keys(this.openTabs);
  }

  assignManager(tabId: string): void {
    const tab = this.openTabs[tabId];
    if (!tab || !tab.selectedManagerId) return;
    this.loading = true;
    this.errorMessage = '';

    this.projectsService.assignManager(tab.task.projectId, tab.selectedManagerId).subscribe({
      next: () => {
        this.successMessage = 'Project Manager assigné avec succès !';
        this.loading = false;
        this.refreshTasks();
        this.successMessage = 'Project Manager assigné avec succès !';
      },
      error: () => {
        this.errorMessage = 'Erreur lors de l\'assignation';
        this.loading = false;
      }
    });
  }

  getProjectName(projectId: string): string {
    return this.projects.find(p => p.id === projectId)?.name || '—';
  }

  get hasSelection(): boolean { return this.selectedIds.size > 0; }

  toggleSelect(taskId: string): void {
    if (this.selectedIds.has(taskId)) this.selectedIds.delete(taskId);
    else this.selectedIds.add(taskId);
  }

  isSelected(taskId: string): boolean { return this.selectedIds.has(taskId); }

  markDone(): void {
    Array.from(this.selectedIds).forEach(id => {
      this.tasksService.updateStatus(id, 2).subscribe({
        next: () => {
          const task = this.myTasks.find(t => t.id === id);
          if (task) task.status = 2;
          this.selectedIds.clear();
        }
      });
    });
  }

  getStatusLabel(status: number): string { return this.tasksService.getStatusLabel(status); }
  getStatusColor(status: number): string { return this.tasksService.getStatusColor(status); }
  endTask(tabId: string): void {
  const tab = this.openTabs[tabId];
  if (!tab) return;
  this.tasksService.updateStatus(tab.task.id, 2).subscribe(() => {
    this.refreshTasks();
    this.tabsService.closeTab(tabId);
    delete this.openTabs[tabId];
  });
}
}