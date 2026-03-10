import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { TasksService,Task } from '../../core/services/tasks.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent implements OnInit{
  tasks:Task[]=[];
  loading=true;

  // Les 3 tools avec leurs infos
  tools = [
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

  constructor(
    private tasksService: TasksService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadTasks();
  }

  loadTasks(): void {
    this.loading = true;
    this.tasksService.getAll().subscribe({
      next: (tasks) => {
        this.tasks = tasks;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
      }
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