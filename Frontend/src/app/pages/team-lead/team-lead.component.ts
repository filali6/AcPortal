import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { AuthService } from '../../core/services/auth.service';
import { TasksService, Task } from '../../core/services/tasks.service';
import { NotificationService } from '../../core/services/notification.service';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-team-lead',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './team-lead.component.html',
  styleUrl: './team-lead.component.scss'
})
export class TeamLeadComponent implements OnInit {

  // UI
  activeView: 'tasks' | 'define-steps' = 'tasks';
  userRole = '';

  // Données
  myTasks: Task[] = [];
  myStreams: any[] = [];
  projects: { id: string, name: string }[] = [];

  // Filtres
  selectedProjectId = '';
  selectedStreamId = '';

  // Formulaire steps
  currentTask: any = null;
  steps: any[] = [
    { stepName: '', toolName: '', order: 1, dependsOnStepId: null }
  ];

  // Sélection
  selectedIds: Set<string> = new Set();

  // Messages
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
    private notificationService: NotificationService
  ) {}

  ngOnInit(): void {
    const userInfo = this.authService.getUserInfo();
    this.currentUserName = userInfo?.name || '';
    this.currentUserId= userInfo?.id || '';
    this.userRole = userInfo?.role || '';
    this.loadAll();
    this.notificationService.notifications$
      .subscribe(() => this.refreshTasks());
  }

  loadAll(): void {
    this.http.get<any[]>(`${this.api}/streams/my`).subscribe({
      next: (s) => {
        this.myStreams = s;
        this.refreshTasks();
        this.loadProjects();
        
      }
    });
  }

  loadProjects(): void {
  const seen = new Set<string>();
  this.myStreams.forEach(s => {
    if (!seen.has(s.projectId)) {
      seen.add(s.projectId);
      this.http.get<any>(`${this.api}/projects/${s.projectId}`).subscribe({
        next: (p) => {
          if (!this.projects.find(x => x.id === p.id))
            this.projects.push({ id: p.id, name: p.name });
        }
      });
    }
  });
}

  refreshTasks(): void {
  this.tasksService.getAll().subscribe({
    next: (tasks) => {
      this.myTasks = tasks.filter(t =>
        t.assignedTo === this.currentUserId
      );
    }
  });
}

  // Filtres projet → stream
  get filteredStreams(): any[] {
    if (!this.selectedProjectId) return this.myStreams;
    return this.myStreams.filter(s => s.projectId === this.selectedProjectId);
  }

  onProjectChange(): void {
    this.selectedStreamId = '';
  }

  get filteredTasks(): Task[] {
    let tasks = this.myTasks;
    if (this.selectedProjectId)
      tasks = tasks.filter(t => t.projectId === this.selectedProjectId);
    if (this.selectedStreamId)
      tasks = tasks.filter(t => t.streamId === this.selectedStreamId);
    return tasks;
  }

  getProjectName(projectId: string): string {
    return this.projects.find(p => p.id === projectId)?.name || '—';
  }

  getStreamName(streamId: string): string {
    const stream = this.myStreams.find(s => s.id === streamId);
    return stream?.name || '—';
  }

  // Clic sur tâche → ouvre formulaire steps
  onTaskClick(task: any): void {
    if (task.status === 2) return;
    this.currentTask = task;
    this.steps = [
      { stepName: '', toolName: '', order: 1, dependsOnStepId: null }
    ];
    this.successMessage = '';
    this.errorMessage = '';
    this.activeView = 'define-steps';
  }

  addStep(): void {
    this.steps.push({
      stepName: '',
      toolName: '',
      order: this.steps.length + 1,
      dependsOnStepId: null
    });
  }

  removeStep(index: number): void {
    this.steps.splice(index, 1);
    this.steps.forEach((s, i) => s.order = i + 1);
  }

  saveSteps(): void {
    if (!this.currentTask) return;

    const stream = this.myStreams.find(
      s => s.projectId === this.currentTask.projectId
    );

    if (!stream) {
      this.errorMessage = 'Stream introuvable pour ce projet';
      return;
    }

    this.loading = true;
    this.errorMessage = '';

    const payload = {
      projectId: this.currentTask.projectId,
      steps: this.steps.map(s => ({
        stepName: s.stepName,
        toolName: s.toolName,
        order: s.order,
        canBeParallel: false,
        dependsOnStepId: null,
        streamId: stream.id
      }))
    };

    this.http.post(`${this.api}/steps`, payload).subscribe({
      next: () => {
        this.successMessage = 'Steps sauvegardés !';
        this.loading = false;
        this.tasksService
          .updateStatus(this.currentTask.id, 2)
          .subscribe(() => this.refreshTasks());
      },
      error: () => {
        this.errorMessage = 'Erreur lors de la sauvegarde';
        this.loading = false;
      }
    });
  }

  // Sélection
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