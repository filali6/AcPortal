import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { AuthService } from '../../core/services/auth.service';
import { TasksService, Task } from '../../core/services/tasks.service';
import { NotificationService } from '../../core/services/notification.service';
import { ProjectsService } from '../../core/services/projects.service';
import { UsersService } from '../../core/services/users.service';
import { StreamsService } from '../../core/services/streams.service';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-project-manager',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './project-manager.component.html',
  styleUrl: './project-manager.component.scss'
})
export class ProjectManagerComponent implements OnInit {

  activeView: 'tasks' | 'create-stream' = 'tasks';

  myTasks: Task[] = [];
  projects: any[] = [];
  bizLeads: any[] = [];
  techLeads: any[] = [];
  consultants: any[] = [];
  selectedConsultantIds: string[] = [];

  streamName = '';
  selectedBizLeadId = '';
  selectedTechLeadId = '';
  currentTask: any = null;

  selectedIds: Set<string> = new Set();

  loading = false;
  successMessage = '';
  errorMessage = '';

  currentUserName = '';
  currentUserId = '';

  private api = environment.apiUrl;

  constructor(
    private http: HttpClient,
    private authService: AuthService,
    private tasksService: TasksService,
    private notificationService: NotificationService,
    private projectsService: ProjectsService,
    private usersService: UsersService,
    private streamsService: StreamsService
  ) {}

  ngOnInit(): void {
    const userInfo = this.authService.getUserInfo();
    this.currentUserName = userInfo?.name || '';
    this.currentUserId= userInfo?.id|| '';
    this.loadAll();
    this.notificationService.notifications$
      .subscribe(() => this.refreshTasks());
  }

  loadAll(): void {
    this.refreshTasks();
    this.loadProjects();
    this.loadLeads();
    this.loadConsultants();
  }

  refreshTasks(): void {
    this.tasksService.getAll().subscribe({
      next: (tasks) => {
        this.myTasks = tasks.filter(
          t => t.assignedTo === this.currentUserId
        );
      }
    });
  }

  loadProjects(): void {
    this.projectsService.getManagedProjects().subscribe({
      next: (p) => this.projects = p
    });
  }

  loadLeads(): void {
    this.usersService.getLeads().subscribe({
      next: (users) => {
        this.bizLeads = users.filter(u => u.role === 'BusinessTeamLead');
        this.techLeads = users.filter(u => u.role === 'TechnicalTeamLead');
      }
    });
  }

  loadConsultants(): void {
    this.usersService.getAll().subscribe({
      next: (users) => {
        this.consultants = users.filter(u => u.role === 'Consultant');
      }
    });
  }

  onTaskClick(task: any): void {
    if (task.status === 2) return;
    this.currentTask = task;
    this.streamName = '';
    this.selectedBizLeadId = '';
    this.selectedTechLeadId = '';
    this.selectedConsultantIds = [];
    this.successMessage = '';
    this.errorMessage = '';
    this.activeView = 'create-stream';
  }

  createStream(): void {
    if (!this.streamName || !this.currentTask) return;
    this.loading = true;
    this.errorMessage = '';

    this.streamsService.create(
      this.streamName,
      this.currentTask.projectId,
      this.selectedBizLeadId || null,
      this.selectedTechLeadId || null
    ).subscribe({
      next: (stream: any) => {
        this.selectedConsultantIds.forEach(consultantId => {
          this.streamsService.addMember(stream.id, consultantId).subscribe();
        });
        this.successMessage = 'Stream créé avec succès !';
        this.loading = false;
        this.streamName = '';
        this.selectedBizLeadId = '';
        this.selectedTechLeadId = '';
        this.selectedConsultantIds = [];
      },
      error: () => {
        this.errorMessage = 'Erreur lors de la création';
        this.loading = false;
      }
    });
  }

  isConsultantSelected(id: string): boolean {
    return this.selectedConsultantIds.includes(id);
  }

  toggleConsultant(id: string): void {
    if (this.isConsultantSelected(id)) {
      this.selectedConsultantIds = this.selectedConsultantIds.filter(c => c !== id);
    } else {
      this.selectedConsultantIds.push(id);
    }
  }

  getProjectName(projectId: string): string {
    return this.projects.find(p => p.id === projectId)?.name || '—';
  }

  get hasSelection(): boolean {
    return this.selectedIds.size > 0;
  }

  toggleSelect(taskId: string): void {
    if (this.selectedIds.has(taskId)) {
      this.selectedIds.delete(taskId);
    } else {
      this.selectedIds.add(taskId);
    }
  }

  isSelected(taskId: string): boolean {
    return this.selectedIds.has(taskId);
  }

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

  getStatusLabel(status: number): string {
    return this.tasksService.getStatusLabel(status);
  }

  getStatusColor(status: number): string {
    return this.tasksService.getStatusColor(status);
  }
}