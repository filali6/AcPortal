import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { ProjectsService, Project } from '../../core/services/projects.service';
import { TasksService, Task } from '../../core/services/tasks.service';
import { NotificationService } from '../../core/services/notification.service';
import { AuthService } from '../../core/services/auth.service';
import { UsersService } from '../../core/services/users.service';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-director',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './director.component.html',
  styleUrl: './director.component.scss'
})
export class DirectorComponent implements OnInit {

  // UI
  activeView: 'tasks' | 'assign-manager' = 'tasks';

  // Données
  myTasks: Task[] = [];
  projects: Project[] = [];
  managers: any[] = [];

  // Sélection
  selectedIds: Set<string> = new Set();
  selectedTask: any = null;
  selectedManagerId = '';

  // Messages
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
    private usersService:UsersService
  ) {}

  ngOnInit(): void {
    const userInfo = this.authService.getUserInfo();
    this.currentUserName = userInfo?.name || '';
     this.currentUserId = userInfo?.id || '';
    this.loadAll();
    this.notificationService.notifications$
      .subscribe(() => this.refreshTasks());
  }

  loadAll(): void {
    // charger les projets
    this.projectsService.getAll().subscribe({
      next: (projects) => this.projects = projects
    });

    // charger les tâches
    this.refreshTasks();

    // charger les project managers avec leur charge
    this.usersService.getProjectManagers()
  .subscribe({ next: (m) => this.managers = m });
  }

  refreshTasks(): void {
    this.tasksService.getAll().subscribe({
      next: (tasks) => {
        console.log('toutes les tâches:', tasks);
        console.log('currentUserId:', this.currentUserId);
        this.myTasks = tasks.filter(
          t => t.assignedTo === this.currentUserId
        );
        console.log('myTasks filtrées:', this.myTasks);
      }
    });
  }

  // Clic sur une tâche → ouvre la vue assignation
  onTaskClick(task: any): void {
    if (task.status === 2) return; // tâche done → pas d'action
    console.log('tâche sélectionnée:', task);
    this.selectedTask = task;
    this.selectedManagerId = '';
    this.successMessage = '';
    this.errorMessage = '';
    this.activeView = 'assign-manager';
  }

  assignManager(): void {
    if (!this.selectedManagerId || !this.selectedTask) return;
    this.loading = true;
    this.errorMessage = '';

   this.projectsService.assignManager(
  this.selectedTask.projectId,
  this.selectedManagerId
).subscribe({
      next: () => {
        this.successMessage = 'Project Manager assigné avec succès !';
        this.loading = false;
        // marquer la tâche comme done
        this.tasksService
          .updateStatus(this.selectedTask.id, 2)
          .subscribe(() => this.refreshTasks());
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

  // Sélection multiple
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

  selectedTask_(taskId: string): boolean {
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
  isSelected(taskId: string): boolean {
    return this.selectedIds.has(taskId);
}
}