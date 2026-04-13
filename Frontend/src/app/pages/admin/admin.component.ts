import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { ProjectsService, Project, Portfolio } from '../../core/services/projects.service';
import { UsersService, User } from '../../core/services/users.service';
import { TasksService, Task } from '../../core/services/tasks.service';
import { NotificationService } from '../../core/services/notification.service';
import { AuthService } from '../../core/services/auth.service';
import { environment } from '../../../environments/environment';
import { Router,ActivatedRoute } from '@angular/router';
@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './admin.component.html',
  styleUrl: './admin.component.scss'
})
export class AdminComponent implements OnInit {

  // --- Création projet ---
  projectName = '';
  projectDescription = '';
  selectedPortfolioId = ''; 
  showCreateProject = false;


  portfolioName = '';
  portfolioDescription = '';
  selectedDirectorId = '';         
  showCreatePortfolio = false;
  // --- Données ---
  projects: Project[] = [];
   portfolios: Portfolio[] = [];
  directors: User[] = [];
  tasks: Task[] = [];

  // --- HeadOfCDS ---
  currentUserId = '';
  myTasks: Task[] = [];

  // --- Supervision par projet ---
  selectedProjectId = '';
  projectTeams: any[] = [];

  // --- UI ---
  loading = false;
  successMessage = '';
  errorMessage = '';
  currentUserName = '';

  activeView: 'tasks' | 'create-project' | 'projects' = 'tasks';
selectedProjectDetail: any = null; // projet cliqué dans la liste
 

  constructor(
    private projectsService: ProjectsService,
    private usersService: UsersService,
    private tasksService: TasksService,
    private notificationService: NotificationService,
    private authService: AuthService,
    private http: HttpClient,
    private route: ActivatedRoute,
    private router: Router,
  ) {}

  ngOnInit(): void {
    const userInfo = this.authService.getUserInfo();
    this.currentUserId = userInfo?.id || userInfo?.sub || '';
    this.currentUserName = userInfo?.name || '';

    this.loadAll();
    this.route.queryParams.subscribe(params => {
  if (params['view'] === 'projects') {
    this.activeView = 'projects';
  } else {
    this.activeView = 'tasks';
  }
});

    // intact — notif existante
    this.notificationService.notifications$.subscribe(() => this.refreshTasks());
  }

  loadAll(): void {
  // Charger les directors pour créer un portfolio
  this.projectsService.getPortfolioDirectors().subscribe({
    next: (directors) => this.directors = directors
  });

  // Charger les portfolios pour créer un projet
  this.projectsService.getAllPortfolios().subscribe({
    next: (portfolios) => this.portfolios = portfolios
  });

  this.projectsService.getAll().subscribe({
    next: (projects) => this.projects = projects
  });

  this.tasksService.getAll().subscribe({
    next: (tasks) => {
      this.tasks = tasks;
      this.myTasks = tasks.filter(t => t.assignedTo === this.currentUserId);
    }
  });
}

  // Clic sur une tâche HeadOfCDS → ouvre le formulaire
  onMyTaskClick(): void {
    this.activeView = 'create-project';
    this.successMessage = '';
    this.errorMessage = '';
  }
  goToTasks(): void {
    this.activeView = 'tasks';
    this.router.navigate([], { queryParams: {}, replaceUrl: true });
}

  // Supervision — chargement équipes + membres du projet
  onProjectChange(): void {
    this.projectTeams = [];
    if (!this.selectedProjectId) return;
    this.http.get<any[]>(`${environment.apiUrl}/teams/project/${this.selectedProjectId}`).subscribe({
      next: (teams) => {
        this.projectTeams = teams;
      }
    });
  }

  getProjectName(id: string): string {
    return this.projects.find(p => p.id === id)?.name || '—';
  }

  // intact
  createProject(): void {
    if (!this.projectName || !this.selectedPortfolioId) {
      this.errorMessage = 'Nom et Director sont obligatoires';
      return;
    }
    this.loading = true;
    this.errorMessage = '';
    this.projectsService.create(
      this.projectName,
      this.projectDescription,
      this.selectedPortfolioId
    ).subscribe({
      next: () => {
        this.successMessage = 'Projet créé avec succès !';
        this.projectName = '';
        this.projectDescription = '';
        this.selectedPortfolioId = '';
        this.loading = false;
        this.showCreateProject = false;
        this.loadAll();
      },
      error: () => {
        this.errorMessage = 'Erreur lors de la création du projet';
        this.loading = false;
      }
    });
  }

  // intact
  getDirectorName(directorId: string): string {
    const director = this.directors.find(d => d.id === directorId);
    return director?.fullName || '—';
  }

  getStatusLabel(status: number): string {
    return this.tasksService.getStatusLabel(status);
  }

  getStatusColor(status: number): string {
    return this.tasksService.getStatusColor(status);
  }

  refreshTasks(): void {
    this.tasksService.getAll().subscribe({
      next: (tasks) => {
        this.tasks = tasks;
        this.myTasks = tasks.filter(t => t.assignedTo === this.currentUserId);
      }
    });
  }

selectedIds: Set<string> = new Set();

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
  const ids = Array.from(this.selectedIds);
  ids.forEach(id => {
    this.tasksService.updateStatus(id, 2).subscribe({
      next: () => {
        const task = this.myTasks.find(t => t.id === id);
        if (task) task.status = 2;
        this.selectedIds.clear();
      }
    });
  });
}
selectProject(project: any): void {
  this.selectedProjectDetail = project;
  this.projectTeams = [];
  this.http.get<any[]>(`${environment.apiUrl}/teams/project/${project.id}`).subscribe({
    next: (teams) => this.projectTeams = teams
  });
}

backToProjects(): void {
  this.selectedProjectDetail = null;
  this.projectTeams = [];
}
createPortfolio(): void {
  if (!this.portfolioName || !this.selectedDirectorId) {
    this.errorMessage = 'Nom et Director sont obligatoires';
    return;
  }
  this.loading = true;
  this.projectsService.createPortfolio(
    this.portfolioName,
    this.portfolioDescription,
    this.selectedDirectorId
  ).subscribe({
    next: () => {
      this.portfolioName = '';
      this.portfolioDescription = '';
      this.selectedDirectorId = '';
      this.showCreatePortfolio = false;  // ✅ ferme le formulaire inline
      this.loading = false;
      // ✅ recharge les portfolios pour que le select se mette à jour
      this.projectsService.getAllPortfolios().subscribe({
        next: (portfolios) => this.portfolios = portfolios
      });
    },
    error: () => {
      this.errorMessage = 'Erreur lors de la création du portfolio';
      this.loading = false;
    }
  });
}

 
}