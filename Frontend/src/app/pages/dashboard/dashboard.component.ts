import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { TasksService, Task } from '../../core/services/tasks.service';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent implements OnInit {

  tasks: Task[] = [];
  loading = true;

  // Tous les outils avec leurs infos statiques
  allTools = [
    {
      name: 'axeIAM',
      description: 'Gestion des roles et accès utilisateur',
      color: '#3b6fd4',
      bg: '#eef2ff',
      icon: '🔐',
      route: '/plugins/axe-iam'
    },
    {
      name: 'axeBPM',
      description: 'Gestion des regles métier',
      color: '#7c3aed',
      bg: '#f5f3ff',
      icon: '⚙️',
      route: '/plugins/axe-bpm'
    },
    {
      name: 'axeGUI',
      description: 'Gestion UI',
      color: '#059669',
      bg: '#d1fae5',
      icon: '🖥️',
      route: '/plugins/axe-gui'
    }
  ];

  // Outils avec rôles du consultant connecté
  myToolRoles: { toolId: string; toolName: string; roles: string[] }[] = [];

  // Cards à afficher → seulement outils avec rôle
  get myTools() {
    return this.allTools.filter(tool =>
      this.myToolRoles.some(t => t.toolName === tool.name && t.roles.length > 0)
    );
  }

  // Rôles d'un outil spécifique
  getRoles(toolName: string): string[] {
    return this.myToolRoles.find(t => t.toolName === toolName)?.roles || [];
  }

  private apiUrl = environment.apiUrl;

  constructor(
    private tasksService: TasksService,
    private router: Router,
    private http: HttpClient
  ) {}

  ngOnInit(): void {
    this.loadTasks();
    this.loadMyRoles();
  }

  loadTasks(): void {
    this.loading = true;
    this.tasksService.getMyTasks().subscribe({
      next: (tasks) => {
        this.tasks = tasks;
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  loadMyRoles(): void {
    this.http.get<any[]>(`${this.apiUrl}/tools/my-roles`).subscribe({
      next: (roles) => this.myToolRoles = [...roles]
    });
  }

  openTool(route: string): void {
    this.router.navigate([route]);
  }

  getStatusLabel(status: number): string {
    return this.tasksService.getStatusLabel(status);
  }

  getStatusColor(status: number): string {
    return this.tasksService.getStatusColor(status);
  }
}