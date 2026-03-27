import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ProjectsService, Project } from '../../core/services/projects.service';
import { UsersService, User } from '../../core/services/users.service';
import { TasksService, Task } from '../../core/services/tasks.service';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { NotificationService } from '../../core/services/notification.service';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-director',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './director.component.html',
  styleUrl: './director.component.scss'
})
export class DirectorComponent implements OnInit {

  // Projets
  projectsWithTeam: any[] = [];    // projets qui ont une équipe
  projectsWithoutTeam: Project[] = [];
  projects: Project[] = []; // projets sans équipe

  // Consultants disponibles
  consultants: User[] = [];

  // Tâches
  tasks: Task[] = [];
  myTasks: Task[] = [];

  currentUserName = '';
activeView: 'tasks' | 'dashboard' = 'tasks';

  // Accordion → quel projet est ouvert
  openProjectId: string | null = null;

  selectedIds: Set<string> = new Set();

  // Détails équipe par projectId
  teamDetails: { [projectId: string]: any } = {};

  // Formulaire création équipe
  teamForms: { [projectId: string]: {
    teamName: string;
    selectedConsultantIds: string[];
    selectedChefId: string;
    loading: boolean;
    successMessage: string;
    errorMessage: string;
  }} = {};

  private apiUrl = environment.apiUrl;

  constructor(
    private projectsService: ProjectsService,
    private usersService: UsersService,
    private tasksService: TasksService,
    private http: HttpClient,
    private notificationService:NotificationService,
     private authService: AuthService,
  ) {}

  ngOnInit(): void {
    const userInfo = this.authService.getUserInfo();
    this.currentUserName = userInfo?.name || '';
    this.loadAll();
    //this.notificationService.notifications$.subscribe(() => this.loadAll());
    this.notificationService.notifications$.subscribe(() => this.refreshTasks());
    this.notificationService.notifications$.subscribe(() => this.refreshProjects());
}

  loadAll(): void {
    // this.projectsWithTeam = [];    
    // this.projectsWithoutTeam = [];

    
    // Charge les consultants
    this.usersService.getAll().subscribe({
      next: (users) => {
        this.consultants = this.usersService.getConsultants(users);
      }
  
    });

    // Charge ses projets
    this.projectsService.getMyProjects().subscribe({
      next: (projects) => {
         console.log('projets reçus:', projects.map(p => p.id));
        // Pour chaque projet → vérifie si une équipe existe
        projects.forEach(project => {
          this.http.get(`${this.apiUrl}/teams/project/${project.id}`).subscribe({
            next: (team: any) => {
              // Projet AVEC équipe
              this.projectsWithTeam.push({ ...project, team });
            },
            error: () => {
              // Projet SANS équipe → 404
              this.projectsWithoutTeam.push(project);
              // Initialise le formulaire pour ce projet
              this.teamForms[project.id] = {
                teamName: '',
                selectedConsultantIds: [],
                selectedChefId: '',
                loading: false,
                successMessage: '',
                errorMessage: ''
              };
            }
          });
        });
      }
    });

    // Charge les tâches
    this.tasksService.getAll().subscribe({
      next: (tasks) => {this.tasks = tasks;
        this.myTasks = tasks.filter(t => t.assignedTo === this.currentUserName);}
    });
  }

  // Accordion → ouvre/ferme un projet
  toggleProject(projectId: string): void {
    this.openProjectId = this.openProjectId === projectId ? null : projectId;
  }

  isOpen(projectId: string): boolean {
    return this.openProjectId === projectId;
  }

  // Gestion des consultants dans le formulaire
  isSelected(projectId: string, consultantId: string): boolean {
    return this.teamForms[projectId]?.selectedConsultantIds.includes(consultantId);
  }

  toggleConsultant(projectId: string, consultantId: string): void {
    const form = this.teamForms[projectId];
    if (this.isSelected(projectId, consultantId)) {
      form.selectedConsultantIds = form.selectedConsultantIds
        .filter(id => id !== consultantId);
      if (form.selectedChefId === consultantId) {
        form.selectedChefId = '';
      }
    } else {
      form.selectedConsultantIds.push(consultantId);
    }
  }

  getSelectedConsultants(projectId: string): User[] {
    return this.consultants.filter(c =>
      this.teamForms[projectId]?.selectedConsultantIds.includes(c.id)
    );
  }

  createTeam(projectId: string): void {
    const form = this.teamForms[projectId];

    if (!form.teamName || !form.selectedChefId) {
      form.errorMessage = 'Nom et chef d\'équipe sont obligatoires';
      return;
    }
    if (form.selectedConsultantIds.length === 0) {
      form.errorMessage = 'Sélectionnez au moins un consultant';
      return;
    }

    form.loading = true;
    form.errorMessage = '';

    // Étape 1 → Créer l'équipe
    this.http.post(`${this.apiUrl}/teams`, {
      name: form.teamName,
      projectId: projectId,
      chefEquipeId: form.selectedChefId
    }).subscribe({
      next: (team: any) => {
        // Étape 2 → Ajouter les membres
        this.http.post(`${this.apiUrl}/teams/${team.data.id}/members`, {
          consultantIds: form.selectedConsultantIds
        }).subscribe({
          next: () => {
            form.successMessage = 'Équipe créée avec succès !';
            form.loading = false;
            // Recharge les projets
            this.projectsWithTeam = [];
            this.projectsWithoutTeam = [];
            this.loadAll();
          },
          error: () => {
            form.errorMessage = 'Erreur lors de l\'ajout des membres';
            form.loading = false;
          }
        });
      },
      error: () => {
        form.errorMessage = 'Erreur lors de la création de l\'équipe';
        form.loading = false;
      }
    });
  }

  getStatusLabel(status: number): string {
    return this.tasksService.getStatusLabel(status);
  }

  getStatusColor(status: number): string {
    return this.tasksService.getStatusColor(status);
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

selectedTask(taskId: string): boolean {
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
refreshTasks(): void {
    this.tasksService.getAll().subscribe({
      next: (tasks) => {
        this.tasks = tasks;
        this.myTasks = tasks.filter(t => t.assignedTo === this.currentUserName);
      }
    });
  }

  refreshProjects(): void {
    this.projectsService.getMyProjects().subscribe({
    next: (projects) => this.projects = projects
});
  }
  

}