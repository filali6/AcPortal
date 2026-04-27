import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TasksService, Task } from '../../core/services/tasks.service';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { Router } from '@angular/router';
import { NotificationService } from '../../core/services/notification.service';
import { PluginBridgeService } from '../../core/services/plugin-bridge.service';

@Component({
  selector: 'app-consultant',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './consultant.component.html',
  styleUrl: './consultant.component.scss'
})
export class ConsultantComponent implements OnInit {

  tasks: Task[] = [];
  selectedIds: Set<string> = new Set();
  loading = false;
  searchQuery = '';
  activeFilter = 'All';
  viewMode: 'list' | 'grid' = 'list';
  filters = ['All', 'Tool', 'Date', 'Stat'];
  chefEquipeProjectIds: Set<string> = new Set();
  projectNames: Map<string, string> = new Map();
  activeProjectSteps: string | null = null;
  streams: any[] = [];
selectedStreamId = '';

availablePlugins: any[] = [];

  filterStat = '';
  filterTool = '';
  filterDate = 'desc';

   
  pendingSteps: any[] = [];
  stepsLoading = false;
  stepsSuccessMessage = '';
  stepsErrorMessage = '';
  chefTaskDone: Set<string> = new Set();
  tools = ['axeIAM', 'axeBPM', 'axeGUI'];
  newStep = {
    stepName: '',
    toolName: '',
    order: 1,
    canBeParallel: false,
    dependsOnStepId: null as string | null
  };

  // Rôles par outil
  toolRoles: { toolId: string; toolName: string; roles: string[] }[] = [];

  projects: {id:string, name: string}[]=[];
  selectedProjectId='';

  private apiUrl = environment.apiUrl;

  constructor(
    private tasksService: TasksService,
    private http: HttpClient,
    private router:Router,
    private notificationService:NotificationService,
    private pluginBridge: PluginBridgeService
  ) {}

  ngOnInit(): void {
    this.loadTasks();
    this.loadMyRoles();
    this.loadProjects();
    this.loadProjectNames();
    this.notificationService.notifications$.subscribe(() => this.loadTasks());
    this.http.get<any[]>(`${this.apiUrl}/streams/my`).subscribe({
  next: (s) => this.streams = s.filter(
    (x, i, arr) => arr.findIndex(y => y.id === x.id) === i
  )
});
this.pluginBridge.getAllPlugins().subscribe({
  next: (plugins) => this.availablePlugins = plugins
});
  }

  loadTasks(): void {
    this.loading = true;
    this.tasksService.getMyTasks().subscribe({
      next: (tasks) => {
        this.tasks = tasks;
        this.loading = false;
        console.log(this.tasks);
      },
      error: () => this.loading = false
    });
    console.log(this.tasks);
  }
  loadProjects(): void {
  this.tasksService.getMyTasks().subscribe({
    next: (tasks) => {
      const seen = new Set<string>();
      tasks.forEach(t => {
        if (t.projectId && !seen.has(t.projectId)) {
          seen.add(t.projectId);
          // nom depuis projectNames qui est déjà chargé
          const name = this.projectNames.get(t.projectId) || t.projectId;
          this.projects.push({ id: t.projectId, name });
        }
      });
    }
  });
}
 

  // ← AJOUT : tâches filtrées par projet sélectionné
  get filteredTasks(): Task[] {
    if (!this.selectedProjectId) return this.tasks;
    return this.tasks.filter(t => t.projectId === this.selectedProjectId);
  }

  // ← AJOUT : ouvrir l'outil directement depuis la tâche
    openTool(task: Task): void {
    if (task.status === 1) return; // bloquée

    const plugin = this.availablePlugins.find(p => p.id === task.toolName);
    if (plugin?.accessUrl) {
      window.open(plugin.accessUrl, '_blank');
    } else {
      // fallback anciens outils internes
      const routes: { [key: string]: string } = {
        'axeIAM': '/plugins/axe-iam',
        'axeBPM': '/plugins/axe-bpm',
        'axeGUI': '/plugins/axe-gui'
      };
      const route = routes[task.toolName];
      if (route) {
        this.router.navigate([route], { queryParams: { projectId: task.projectId } });
      }
    }
}


  loadMyRoles(): void {
  this.http.get<any[]>(`${this.apiUrl}/tools/my-roles`).subscribe({
    next: (roles) => {
      console.log('roles reçus:', roles);
      this.toolRoles = [...roles]; // ← force la détection de changement
    },
    error: (err) => console.error(err)
  });
}

  // Sélection
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

  get hasSelection(): boolean {
    return this.selectedIds.size > 0;
  }

  // Mark done → status = 2
  markDone(): void {
    const ids = Array.from(this.selectedIds);
    ids.forEach(id => {
        this.tasksService.updateStatus(id, 2).subscribe({ // 2 = Done
            next: () => {
                const task = this.tasks.find(t => t.id === id);
                if (task) task.status = 2;
                this.selectedIds.clear();
            }
        });
    });
}

  get pendingTasks(): Task[] {
    return this.tasks.filter(t => t.status === 0|| t.status === 1);
  }

  get doneTasks(): Task[] {
    return this.tasks.filter(t => t.status === 2);
  }

  isBlocked(task: Task): boolean {
      return task.status===1;
  }

  getStatusLabel(status: number): string {
      return this.tasksService.getStatusLabel(status);
  }

  getStatusColor(status: number): string {
      return this.tasksService.getStatusColor(status);
  }
  

  loadProjectNames(): void {
    this.http.get<{ id: string, name: string }[]>(`${this.apiUrl}/projects`).subscribe({
      next: (projects) => projects.forEach(p => this.projectNames.set(p.id, p.name))
    });
  }

  

  getProjectName(projectId: string): string {
    return this.projectNames.get(projectId) || '';
  }

  get projectIds(): string[] {
  return [...new Set(this.tasks.map(t => t.projectId).filter((id): id is string => !!id))];
}

  // getTasksForProject(projectId: string): Task[] {
  //   return this.tasks.filter(t => {
  //       if (t.projectId !== projectId) return false;
  //       if (t.status === 2) return false; // biffée si Done

  //       if (this.activeFilter === 'Bloquée') return t.status === 1;
  //       if (this.activeFilter === 'En attente') return t.status === 0;

  //       return true;
  //   });
  // }
//   getTasksForProject(projectId: string): Task[] {
//     let tasks = this.tasks.filter(t => t.projectId === projectId);
//     if (this.selectedStreamId) {
//     const stream = this.streams.find(s => s.id === this.selectedStreamId);
//     if (stream) tasks = tasks.filter(t => t.stepId);
//     // on garde cette logique simple pour l'instant
//   }

//     if (this.activeFilter === 'Stat' && this.filterStat !== '') {
//         tasks = tasks.filter(t => t.status === +this.filterStat);
//     }

//     if (this.activeFilter === 'Tool' && this.filterTool) {
//         tasks = tasks.filter(t => t.toolName === this.filterTool);
//     }

//     if (this.activeFilter === 'Date') {
//         tasks = [...tasks].sort((a, b) => {
//             const diff = new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime();
//             return this.filterDate === 'asc' ? diff : -diff;
//         });
//     }

//     return tasks;
// }

  goToChefEquipe(projectId: string): void {
    this.router.navigate(['/chef-equipe'], { queryParams: { projectId } });
}
openSteps(projectId: string): void {
  this.activeProjectSteps = projectId;
  this.pendingSteps = [];
  this.stepsSuccessMessage = '';
  this.stepsErrorMessage = '';
  this.newStep = { stepName: '', toolName: '', order: 1, canBeParallel: false, dependsOnStepId: null };
}


goBack(): void {
  this.activeProjectSteps = null;
}
addStep(): void {
  if (!this.newStep.stepName || !this.newStep.toolName) {
    this.stepsErrorMessage = 'StepName et ToolName sont obligatoires.';
    return;
  }
  this.pendingSteps.push({ ...this.newStep });
  this.newStep = { stepName: '', toolName: '', order: this.pendingSteps.length + 1, canBeParallel: false, dependsOnStepId: null };
  this.stepsErrorMessage = '';
}

removeStep(index: number): void {
  this.pendingSteps.splice(index, 1);
}

submitSteps(): void {
  if (!this.activeProjectSteps || this.pendingSteps.length === 0) return;
  this.stepsLoading = true;
  this.http.post(`${this.apiUrl}/steps`, {
    projectId: this.activeProjectSteps,
    steps: this.pendingSteps
  }).subscribe({
    next: () => {
      this.stepsSuccessMessage = 'Steps créés avec succès !';
      this.stepsLoading = false;
      this.pendingSteps = [];
    },
    error: () => {
      this.stepsErrorMessage = 'Erreur lors de la création des steps.';
      this.stepsLoading = false;
    }
  });
}
get filteredProjectIds(): string[] {
  let ids = this.selectedProjectId 
    ? [this.selectedProjectId] 
    : this.projectIds;
  return ids;
}
get availableTools(): string[] {
    return [...new Set(this.tasks.map(t => t.toolName).filter(Boolean))];
}
get filteredStreams(): any[] {
  if (!this.selectedProjectId) return this.streams;
  return this.streams.filter(s => s.projectId === this.selectedProjectId);
}

// Quand on change de projet, reset le stream sélectionné
onProjectChange(): void {
  this.selectedStreamId = '';
}

getTasksForProject(projectId: string): Task[] {
  let tasks = this.tasks.filter(t => t.projectId === projectId);

  if (this.selectedStreamId) {
    tasks = tasks.filter(t => t.streamId === this.selectedStreamId);
  }

  if (this.activeFilter === 'Stat' && this.filterStat !== '') {
    tasks = tasks.filter(t => t.status === +this.filterStat);
  }
  if (this.activeFilter === 'Tool' && this.filterTool) {
    tasks = tasks.filter(t => t.toolName === this.filterTool);
  }
  if (this.activeFilter === 'Date') {
    tasks = [...tasks].sort((a, b) => {
      const diff = new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime();
      return this.filterDate === 'asc' ? diff : -diff;
    });
  }
  return tasks;
}



}