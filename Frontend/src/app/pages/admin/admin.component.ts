import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { ProjectsService, Project, Portfolio } from '../../core/services/projects.service';
import { UsersService, User } from '../../core/services/users.service';
import { TasksService, Task } from '../../core/services/tasks.service';
import { NotificationService } from '../../core/services/notification.service';
import { AuthService } from '../../core/services/auth.service';
import { TabsService } from '../../core/services/tabs.service';
import { ContractsService } from '../../core/services/contracts.service';
import { environment } from '../../../environments/environment';
import { Router, ActivatedRoute } from '@angular/router';

@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './admin.component.html',
  styleUrl: './admin.component.scss'
})
export class AdminComponent implements OnInit {

  activeTabId = 'tasks';
  showProjectsView = false;

  projects: Project[] = [];
  portfolios: Portfolio[] = [];
  directors: User[] = [];
  tasks: Task[] = [];
  myTasks: Task[] = [];
  projectTeams: any[] = [];
  selectedProjectDetail: any = null;
  stats = { total: 0, inProgress: 0, contracts: 0 };

  // Onglet edition projet
  editingProject: any = null;

  openTabs: { [tabId: string]: {
    task: any,
    projectName: string,
    projectDescription: string,
    targetDate: string,
    selectedPortfolioId: string,
    showCreatePortfolio: boolean,
    portfolioName: string,
    portfolioDescription: string,
    selectedDirectorId: string,
    contractInfo: any | null
  }} = {};

  // Onglets édition projet
  editTabs: { [tabId: string]: {
    project: any,
    name: string,
    description: string,
    targetDate: string
  }} = {};

  loading = false;
  successMessage = '';
  errorMessage = '';
  currentUserName = '';
  currentUserId = '';
  selectedIds: Set<string> = new Set();

  constructor(
    private projectsService: ProjectsService,
    private usersService: UsersService,
    private tasksService: TasksService,
    private notificationService: NotificationService,
    private authService: AuthService,
    private http: HttpClient,
    private route: ActivatedRoute,
    private router: Router,
    private tabsService: TabsService,
    private contractsService: ContractsService
  ) {}

  ngOnInit(): void {
    const userInfo = this.authService.getUserInfo();
    this.currentUserId = userInfo?.id || userInfo?.sub || '';
    this.currentUserName = userInfo?.name || '';
    this.loadAll();

    this.route.queryParams.subscribe(params => {
      if (params['view'] === 'projects') {
        this.showProjectsView = true;
        this.tabsService.setActiveTab('tasks');
      } else {
        this.showProjectsView = false;
      }
    });

    this.notificationService.notifications$.subscribe(() => this.refreshTasks());
    this.tabsService.activeTabId.subscribe(id => {
      this.activeTabId = id;
      if (id === 'tasks') this.showProjectsView = false;
    });
  }

  loadAll(): void {
    this.projectsService.getPortfolioDirectors().subscribe({ next: (d) => this.directors = d });
    this.projectsService.getAllPortfolios().subscribe({ next: (p) => this.portfolios = p });
    this.projectsService.getAll().subscribe({ next: (p) => this.projects = p });
    this.projectsService.getStats().subscribe({ next: (s) => this.stats = s });
    this.tasksService.getAll().subscribe({
      next: (tasks) => {
        this.tasks = tasks;
        this.myTasks = tasks.filter(t => t.assignedTo === this.currentUserId);
      }
    });
  }

  refreshTasks(): void {
    this.tasksService.getAll().subscribe({
      next: (tasks) => {
        this.tasks = tasks;
        this.myTasks = tasks.filter(t => t.assignedTo === this.currentUserId);
      }
    });
  }

  onMyTaskClick(task: any): void {
    if (task.status === 2) return;
    const tabId = `create-project-${task.id}`;
    if (!this.openTabs[tabId]) {
      this.openTabs[tabId] = {
        task,
        projectName: '',
        projectDescription: '',
        targetDate: '',
        selectedPortfolioId: '',
        showCreatePortfolio: false,
        portfolioName: '',
        portfolioDescription: '',
        selectedDirectorId: '',
        contractInfo: null
      };

      // Charger les infos du contrat si contractId disponible
      if (task.contractId) {
        this.contractsService.getById(task.contractId).subscribe({
          next: (contract) => {
            this.openTabs[tabId].contractInfo = contract;
          }
        });
      }
    }
    this.tabsService.openTab({
      id: tabId,
      title: task.title,
      type: 'create-project',
      data: task
    });
  }

  getTabData(tabId: string) {
    return this.openTabs[tabId] || null;
  }

  getOpenTabIds(): string[] {
    return Object.keys(this.openTabs);
  }

  getEditTabIds(): string[] {
    return Object.keys(this.editTabs);
  }

  getEditTabData(tabId: string) {
    return this.editTabs[tabId] || null;
  }

  openEditTab(project: any): void {
    const tabId = `edit-project-${project.id}`;
    if (!this.editTabs[tabId]) {
      this.editTabs[tabId] = {
        project,
        name: project.name,
        description: project.description,
        targetDate: project.targetDate ? project.targetDate.substring(0, 10) : ''
      };
    }
    this.tabsService.openTab({
      id: tabId,
      title: `Modifier — ${project.name}`,
      type: 'create-project',
      data: project
    });
  }

  updateProject(tabId: string): void {
    const tab = this.editTabs[tabId];
    if (!tab || !tab.name) return;
    this.loading = true;

    this.projectsService.update(tab.project.id, tab.name, tab.description, tab.targetDate || undefined).subscribe({
      next: () => {
        this.successMessage = 'Projet modifié avec succès !';
        this.loading = false;
        this.loadAll();
      },
      error: () => {
        this.errorMessage = 'Erreur lors de la modification';
        this.loading = false;
      }
    });
  }

  endEditTask(tabId: string): void {
    this.tabsService.closeTab(tabId);
    delete this.editTabs[tabId];
    this.successMessage = '';
  }

  createProject(tabId: string): void {
    const tab = this.openTabs[tabId];
    if (!tab || !tab.projectName || !tab.selectedPortfolioId) {
      this.errorMessage = 'Nom et Portfolio sont obligatoires';
      return;
    }
    this.loading = true;
    this.errorMessage = '';
    this.projectsService.create(
      tab.projectName,
      tab.projectDescription,
      tab.selectedPortfolioId,
      tab.targetDate || undefined
    ).subscribe({
      next: () => {
        this.successMessage = 'Projet créé avec succès !';
        this.loading = false;
        this.loadAll();
      },
      error: () => {
        this.errorMessage = 'Erreur lors de la création du projet';
        this.loading = false;
      }
    });
  }

  endTask(tabId: string): void {
    const tab = this.openTabs[tabId];
    if (!tab) return;
    this.tasksService.updateStatus(tab.task.id, 2).subscribe(() => {
      this.refreshTasks();
      this.tabsService.closeTab(tabId);
      delete this.openTabs[tabId];
    });
  }

  createPortfolio(tabId: string): void {
    const tab = this.openTabs[tabId];
    if (!tab || !tab.portfolioName || !tab.selectedDirectorId) {
      this.errorMessage = 'Nom et Director sont obligatoires';
      return;
    }
    this.loading = true;
    this.projectsService.createPortfolio(tab.portfolioName, tab.portfolioDescription, tab.selectedDirectorId).subscribe({
      next: () => {
        tab.portfolioName = '';
        tab.portfolioDescription = '';
        tab.selectedDirectorId = '';
        tab.showCreatePortfolio = false;
        this.loading = false;
        this.projectsService.getAllPortfolios().subscribe({ next: (p) => this.portfolios = p });
      },
      error: () => {
        this.errorMessage = 'Erreur lors de la création du portfolio';
        this.loading = false;
      }
    });
  }

  goToTasks(): void {
    this.showProjectsView = false;
    this.tabsService.setActiveTab('tasks');
    this.router.navigate([], { queryParams: {}, replaceUrl: true });
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

  getProjectName(id: string): string {
    return this.projects.find(p => p.id === id)?.name || '—';
  }

  getDirectorName(directorId: string): string {
    return this.directors.find(d => d.id === directorId)?.fullName || '—';
  }

  getFileUrl(fileName: string): string {
    return this.contractsService.getFileUrl(fileName);
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
}