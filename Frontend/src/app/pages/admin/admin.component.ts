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
import { Router, ActivatedRoute } from '@angular/router';
import { Chart, registerables } from 'chart.js';
Chart.register(...registerables);
import { ToastService } from '../../core/services/toast.service';
import { LucideAngularModule, FolderOpen, BarChart2, FileText, CheckSquare, Clock, TrendingUp, AlertCircle, Calendar, ChevronRight } from 'lucide-angular';
import { PortfoliosTabComponent } from './portfolios-tab/portfolios-tab.component';
import { ProjectsTabComponent } from './projects-tab/projects-tab.component';
import { ContractsTabComponent } from './contracts-tab/contracts-tab.component';
import { ChartService } from '../../core/services/chart.service';
import { UtilsService } from '../../core/services/utils.service';
import { TranslateModule } from '@ngx-translate/core';
import { BriefingCardComponent } from '../../core/components/briefing-card/briefing-card.component';
import { MarkdownModule} from 'ngx-markdown';
import { SlaService, SlaDashboard } from '../../core/services/sla.service';
@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideAngularModule, PortfoliosTabComponent, ProjectsTabComponent, ContractsTabComponent, TranslateModule,BriefingCardComponent,MarkdownModule],
  templateUrl: './admin.component.html',
  styleUrl: './admin.component.scss'
})
export class AdminComponent implements OnInit {

  activeTabId = 'tasks';

  projects: Project[] = [];
  portfolios: Portfolio[] = [];
  directors: User[] = [];
  tasks: Task[] = [];
  myTasks: Task[] = [];
  allContracts: any[] = [];
  stats = { total: 0, inProgress: 0, contracts: 0 };

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
    contractInfo: any | null,
    contractId: string | null
  }} = {};

  editTabs: { [tabId: string]: {
    project: any,
    name: string,
    description: string,
    targetDate: string
  }} = {};

  loading = false;
  currentUserName = '';
  currentUserId = '';
  selectedIds: Set<string> = new Set();
  searchQuery = '';
  filterStatus = 'all';
  private donutChart: Chart | null = null;
  private barChart: Chart | null = null;
  filterDateFrom = '';
  filterDateTo = '';
  showProjectsView = false;

  statsBottom = {
    pendingTasks: 0,
    completionRate: 0,
    contractsNotTreated: 0,
    activeProjects: 0
  };
  portfolioGlobalStats = {
  avgProgress: 0,
  totalPortfolios: 0,
  totalProjects: 0
};

  // US64 — Dashboard SLA (Responsable CDS) : tâches / streams en retard ou à risque
  slaDashboard: SlaDashboard | null = null;
  loadingSlaDashboard = false;

  // US77 — Rapport hebdomadaire SLA généré par l'agent IA
  weeklyReport: any = null;
  loadingWeeklyReport = false;
  generatingWeeklyReport = false;

  readonly FileText = FileText;
  readonly FolderOpen = FolderOpen;
  readonly BarChart2 = BarChart2;
  readonly ChevronRight = ChevronRight;
  readonly CheckSquare = CheckSquare;
  readonly Clock = Clock;
  readonly TrendingUp = TrendingUp;
  readonly AlertCircle = AlertCircle;
  readonly Calendar = Calendar;

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
    private contractsService: ContractsService,
    private toastService: ToastService,
    private chartService: ChartService,
    public utils: UtilsService,
    private slaService: SlaService
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
      if (id === 'tasks') {this.showProjectsView = false;setTimeout(() => this.renderCharts(), 100);}
    });
  }

  loadAll(): void {
    this.projectsService.getPortfolioDirectors().subscribe({ next: (d) => this.directors = d });
this.projectsService.getAllPortfolios().subscribe({
    next: (p) => {
      this.portfolios = p;
      this.computePortfolioGlobalStats();   
    }
  });    this.projectsService.getStats().subscribe({ next: (s) => this.stats = s });
    
    this.contractsService.getAll().subscribe({ next: (c) => {
      this.allContracts = c;
      this.computeBottomStats();
    }});

    this.projectsService.getAll().subscribe({ next: (p) => {
      this.projects = p;
      this.tasksService.getAll().subscribe({
        next: (tasks) => {
          this.tasks = tasks;
          this.myTasks = tasks
            .filter(t => t.assignedTo === this.currentUserId)
            .sort((a, b) => a.status - b.status);
          this.computeBottomStats();
        }
      });
    }});

    this.loadSlaDashboard();
  }

  computeBottomStats(): void {
    this.statsBottom.pendingTasks = this.myTasks.filter(t => t.status === 0).length;

    this.statsBottom.completionRate = this.myTasks.length
      ? Math.round((this.myTasks.filter(t => t.status === 2).length / this.myTasks.length) * 100)
      : 0;

    this.statsBottom.contractsNotTreated = this.allContracts.filter(c => !c.projectId).length;

    this.statsBottom.activeProjects = this.projects.length;

    setTimeout(() => this.renderCharts(), 100);
  }

  renderCharts(): void {
    this.donutChart = this.chartService.createDoughnut(
      'adminDonutChart',
      ['Pending', 'Blocked', 'Done'],
      [
        this.myTasks.filter(t => t.status === 0).length,
        this.myTasks.filter(t => t.status === 1).length,
        this.myTasks.filter(t => t.status === 2).length
      ],
      ['#f59e0b', '#ef4444', '#10b981'],
      this.donutChart
    );

  this.barChart = this.chartService.createBar(
  'adminBarChart',
  this.chartService.getLast6MonthsLabels(),
  this.chartService.getLast6MonthsData(this.tasks.filter(t => t.status === 2), 'createdAt'),
  '#10b981',  // vert pour "done"
  this.barChart
);
  }

  refreshTasks(): void {
    this.tasksService.getAll().subscribe({
      next: (tasks) => {
        this.tasks = tasks;
        this.myTasks = tasks
          .filter(t => t.assignedTo === this.currentUserId)
          .sort((a, b) => a.status - b.status);
        this.computeBottomStats();
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
        contractInfo: null,
        contractId: task.contractId || null
      };

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

  resetFilters(): void {
    this.searchQuery = '';
    this.filterStatus = 'all';
    this.filterDateFrom = '';
    this.filterDateTo = '';
  }

  openAccessTab(type: 'portfolios' | 'projects' | 'contracts' | 'sla'): void {
    const tabId = `${type}-tab`;
    const titles = { portfolios: 'Portfolios', projects: 'Projects', contracts: 'Contracts', sla: 'SLA Dashboard' };
    this.tabsService.openTab({ id: tabId, title: titles[type], type: 'create-project' });

    // On rafraîchit les données SLA à chaque ouverture de l'onglet pour éviter d'afficher du stale data
    if (type === 'sla') {
      this.loadSlaDashboard();
      this.loadLatestWeeklyReport();
    }
  }

  getTabData(tabId: string) { return this.openTabs[tabId] || null; }
  getOpenTabIds(): string[] { return Object.keys(this.openTabs); }
  getEditTabIds(): string[] { return Object.keys(this.editTabs); }
  getEditTabData(tabId: string) { return this.editTabs[tabId] || null; }

  get filteredTasks(): Task[] {
    return this.myTasks.filter(t => {
      const matchSearch = t.title.toLowerCase().includes(this.searchQuery.toLowerCase());
      const matchStatus = this.filterStatus === 'all' || t.status === +this.filterStatus;
      return matchSearch && matchStatus;
    });
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
        this.toastService.show('Project updated!', 'success');
        this.loading = false;
        this.loadAll();
      },
      error: () => {
        this.toastService.show('Error updating project', 'error');
        this.loading = false;
      }
    });
  }

  endEditTask(tabId: string): void {
    this.tabsService.closeTab(tabId);
    delete this.editTabs[tabId];
  }

  createProject(tabId: string): void {
    const tab = this.openTabs[tabId];
    if (!tab || !tab.projectName || !tab.selectedPortfolioId) {
      this.toastService.show('Project name and Portfolio are required', 'error');
      return;
    }
    this.loading = true;
    this.projectsService.create(
      tab.projectName,
      tab.projectDescription,
      tab.selectedPortfolioId,
      tab.targetDate || undefined,
      tab.contractId || undefined
    ).subscribe({
      next: () => {
        this.toastService.show('Project created successfully!', 'success');
        this.loading = false;
        this.loadAll();
        this.endTask(tabId);
      },
      error: () => {
        this.toastService.show('Error creating project', 'error');
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
      this.toastService.show('Portfolio name and Director are required', 'error');
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
        this.toastService.show('Portfolio created!', 'success');
        this.projectsService.getAllPortfolios().subscribe({ next: (p) => this.portfolios = p });
      },
      error: () => {
        this.toastService.show('Error creating portfolio', 'error');
        this.loading = false;
      }
    });
  }

  goToTasks(): void {
    this.showProjectsView = false;
    this.tabsService.setActiveTab('tasks');
    this.router.navigate([], { queryParams: {}, replaceUrl: true });
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
  getFileName(filePath: string): string {
    
    const parts = filePath.split('_');
   
    if (parts.length > 1 && parts[0].length === 36) {
        return parts.slice(1).join('_');
    }
   
    return filePath.split('/').pop() || filePath;
}
retrySummary(contractId: string): void {
  this.loading = true;
  this.contractsService.retrySummary(contractId).subscribe({
    next: () => {
      this.toastService.show('Résumé en cours de génération...', 'success');
      this.loading = false;
    },
    error: () => {
      this.toastService.show('Erreur lors de la relance', 'error');
      this.loading = false;
    }
  });
}
computePortfolioGlobalStats(): void {
  this.portfolioGlobalStats.totalPortfolios = this.portfolios.length;
  this.portfolioGlobalStats.totalProjects = this.portfolios.reduce((sum, p) => sum + (p.projectCount || 0), 0);
  this.portfolioGlobalStats.avgProgress = this.portfolios.length
    ? Math.round(this.portfolios.reduce((sum, p) => sum + (p.progress || 0), 0) / this.portfolios.length)
    : 0;
}

  // ===== SLA — US64 : Dashboard des tâches / streams en retard ou à risque =====

  loadSlaDashboard(): void {
    this.loadingSlaDashboard = true;
    this.slaService.getDashboard().subscribe({
      next: (dashboard) => {
        this.slaDashboard = dashboard;
        this.loadingSlaDashboard = false;
      },
      error: () => {
        this.loadingSlaDashboard = false;
      }
    });
  }

  // Le backend sérialise l'enum SlaStatus (OnTrack=0, AtRisk=1, Overdue=2) en nombre.
  private readonly SLA_STATUS_LABELS = ['OnTrack', 'AtRisk', 'Overdue'];

  getSlaStatusLabel(status: number | string): string {
    return typeof status === 'number' ? (this.SLA_STATUS_LABELS[status] || 'OnTrack') : status;
  }

  getSlaBadgeClass(status: number | string): string {
    const s = this.getSlaStatusLabel(status);
    if (s === 'Overdue') return 'sla-overdue';
    if (s === 'AtRisk') return 'sla-atrisk';
    return 'sla-ontrack';
  }

  get slaOverdueTotal(): number {
    return (this.slaDashboard?.summary?.totalOverdueTasks || 0) + (this.slaDashboard?.summary?.totalOverdueStreams || 0);
  }

  // ===== SLA — US77 : Rapport hebdomadaire généré par l'agent IA =====

  loadLatestWeeklyReport(): void {
    this.loadingWeeklyReport = true;
    this.slaService.getLatestWeeklyReport().subscribe({
      next: (r: any) => {
        this.weeklyReport = r;
        this.loadingWeeklyReport = false;
      },
      error: () => {
        // 404 attendu si aucun rapport n'a encore été généré — pas une vraie erreur
        this.weeklyReport = null;
        this.loadingWeeklyReport = false;
      }
    });
  }

  generateWeeklyReport(): void {
    this.generatingWeeklyReport = true;
    this.slaService.generateWeeklyReport().subscribe({
      next: (r: any) => {
        this.weeklyReport = r;
        this.generatingWeeklyReport = false;
        this.toastService.show('Weekly SLA report generated!', 'success');
      },
      error: () => {
        this.toastService.show('Error generating weekly report', 'error');
        this.generatingWeeklyReport = false;
      }
    });
  }
}