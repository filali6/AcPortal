import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ProjectsService, Project } from '../../core/services/projects.service';
import { TasksService, Task } from '../../core/services/tasks.service';
import { NotificationService } from '../../core/services/notification.service';
import { AuthService } from '../../core/services/auth.service';
import { UsersService } from '../../core/services/users.service';
import { TabsService } from '../../core/services/tabs.service';
import { ToastService } from '../../core/services/toast.service';
import { UtilsService } from '../../core/services/utils.service';
import { ChartService } from '../../core/services/chart.service';
import { ModalComponent } from '../../core/components/modal/modal.component';
import { LucideAngularModule, ChevronRight, Briefcase } from 'lucide-angular';
import { Subscription } from 'rxjs';
import { Chart, registerables } from 'chart.js';
Chart.register(...registerables);

@Component({
  selector: 'app-director',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideAngularModule, ModalComponent],
  templateUrl: './director.component.html',
  styleUrl: './director.component.scss'
})
export class DirectorComponent implements OnInit, OnDestroy {

  activeTabId: string = 'tasks';
  myTasks: Task[] = [];
  projects: any[] = [];
  managers: any[] = [];
  myPortfolio: any = null;
  selectedProject: any = null;

  // Modal réassigner PM
  showReassignModal = false;
  reassignProjectId: string = '';
  reassignProjectName: string = '';
  selectedManagerId: string = '';

  selectedIds: Set<string> = new Set();
  openTabs: { [tabId: string]: { task: any, selectedManagerId: string } } = {};

  searchQuery = '';
  filterStatus = 'all';

  loading = false;
  currentUserId = '';

  private donutChart: Chart | null = null;
  private barChart: Chart | null = null;
  private subs: Subscription[] = [];

  statsBottom = {
    pendingTasks: 0,
    completionRate: 0,
    activeProjects: 0,
    nextDelivery: { name: '', date: '' }
  };

  readonly ChevronRight = ChevronRight;
  readonly Briefcase = Briefcase;

  constructor(
    private projectsService: ProjectsService,
    private tasksService: TasksService,
    private notificationService: NotificationService,
    private authService: AuthService,
    private usersService: UsersService,
    private tabsService: TabsService,
    private toastService: ToastService,
    public utils: UtilsService,
    private chartService: ChartService
  ) {}

  ngOnInit(): void {
    const userInfo = this.authService.getUserInfo();
    this.currentUserId = userInfo?.id || userInfo?.sub || '';
    this.loadAll();

    this.subs.push(
      this.notificationService.notifications$.subscribe(() => this.refreshTasks()),
      this.tabsService.activeTabId.subscribe(id => this.activeTabId = id)
    );
  }

  ngOnDestroy(): void {
    this.subs.forEach(s => s.unsubscribe());
  }

  loadAll(): void {
    this.projectsService.getMyProjects().subscribe({
      next: (p) => {
        this.projects = p;
        this.computeStats();
      }
    });
    this.projectsService.getMyPortfolios().subscribe({
      next: (portfolios) => this.myPortfolio = portfolios[0] || null
    });
    this.usersService.getProjectManagers().subscribe({
      next: (m) => this.managers = m
    });
    this.refreshTasks();
  }

  refreshTasks(): void {
    this.tasksService.getAll().subscribe({
      next: (tasks) => {
        this.myTasks = tasks
          .filter(t => t.assignedTo === this.currentUserId)
          .sort((a, b) => a.status - b.status);
        this.computeStats();
      }
    });
  }

  computeStats(): void {
    this.statsBottom.pendingTasks = this.myTasks.filter(t => t.status === 0).length;
    this.statsBottom.completionRate = this.myTasks.length
      ? Math.round((this.myTasks.filter(t => t.status === 2).length / this.myTasks.length) * 100)
      : 0;
    this.statsBottom.activeProjects = this.projects.length;

    const upcoming = this.projects
      .filter((p: any) => p.targetDate)
      .sort((a: any, b: any) => new Date(a.targetDate).getTime() - new Date(b.targetDate).getTime());

    this.statsBottom.nextDelivery = upcoming[0]
      ? { name: (upcoming[0] as any).name, date: new Date((upcoming[0] as any).targetDate).toLocaleDateString('en-GB') }
      : { name: '—', date: '—' };

    setTimeout(() => this.renderCharts(), 100);
  }

  renderCharts(): void {
    this.donutChart = this.chartService.createDoughnut(
      'directorDonutChart',
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
      'directorBarChart',
      this.chartService.getLast6MonthsLabels(),
      this.chartService.getLast6MonthsData(this.projects),
      '#3b82f6',
      this.barChart
    );
  }

  // ===== TASK INBOX =====

  get filteredTasks(): Task[] {
    return this.myTasks.filter(t => {
      const matchSearch = t.title.toLowerCase().includes(this.searchQuery.toLowerCase());
      const matchStatus = this.filterStatus === 'all' || t.status === +this.filterStatus;
      return matchSearch && matchStatus;
    });
  }

  onTaskClick(task: any): void {
    if (task.status === 2) return;
    const tabId = `assign-manager-${task.id}`;
    if (!this.openTabs[tabId]) {
      this.openTabs[tabId] = { task, selectedManagerId: '' };
    }
    this.tabsService.openTab({
      id: tabId,
      title: task.title,
      type: 'assign-manager',
      data: task
    });
  }

  getTabData(tabId: string) {
    return this.openTabs[tabId] || null;
  }

  getOpenTabIds(): string[] {
    return Object.keys(this.openTabs);
  }

  assignManager(tabId: string): void {
    const tab = this.openTabs[tabId];
    if (!tab || !tab.selectedManagerId) return;
    this.loading = true;

    this.projectsService.assignManager(tab.task.projectId, tab.selectedManagerId).subscribe({
      next: () => {
        this.toastService.show('Project Manager assigned!', 'success');
        this.loading = false;
        this.endTask(tabId);
      },
      error: () => {
        this.toastService.show('Error assigning manager', 'error');
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

  // ===== PORTFOLIO TAB =====

  openPortfolioTab(): void {
    this.tabsService.openTab({
      id: 'my-portfolio',
      title: 'My Portfolio',
      type: 'create-project'
    });
  }

  selectProject(project: any): void {
    this.projectsService.getDetails(project.id).subscribe({
      next: (d) => {
        this.selectedProject = d;
        this.tabsService.openTab({
          id: `project-detail-${project.id}`,
          title: project.name,
          type: 'create-project'
        });
      }
    });
  }

  // ===== REASSIGN PM =====

  openReassignModal(project: any): void {
    this.reassignProjectId = project.id;
    this.reassignProjectName = project.name;
    this.selectedManagerId = project.projectManagerId || '';
    this.showReassignModal = true;
  }

  closeReassignModal(): void {
    this.showReassignModal = false;
    this.reassignProjectId = '';
    this.selectedManagerId = '';
  }

  reassignManager(): void {
    if (!this.selectedManagerId || !this.reassignProjectId) return;
    this.loading = true;
    this.projectsService.assignManager(this.reassignProjectId, this.selectedManagerId).subscribe({
      next: () => {
        this.toastService.show('PM reassigned!', 'success');
        this.loading = false;
        this.closeReassignModal();
        this.loadAll();
      },
      error: () => {
        this.toastService.show('Error reassigning PM', 'error');
        this.loading = false;
      }
    });
  }

  // ===== SELECTION =====

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

  getProjectName(projectId: string): string {
    return this.projects.find((p: any) => p.id === projectId)?.name || '—';
  }
}