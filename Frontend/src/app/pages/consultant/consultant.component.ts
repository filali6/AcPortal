import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TasksService, Task } from '../../core/services/tasks.service';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { NotificationService } from '../../core/services/notification.service';
import { PluginBridgeService } from '../../core/services/plugin-bridge.service';
import { ToastService } from '../../core/services/toast.service';
import { UtilsService } from '../../core/services/utils.service';
import { ChartService } from '../../core/services/chart.service';
import { TabsService } from '../../core/services/tabs.service';
import { AuthService } from '../../core/services/auth.service';
import { LucideAngularModule, ChevronRight, Layers } from 'lucide-angular';
import { environment } from '../../../environments/environment';
import { Subscription } from 'rxjs';
import { Chart, registerables } from 'chart.js';
import { TeamFilterPipe } from '../../core/pipes/team-filter.pipe';
Chart.register(...registerables);

@Component({
  selector: 'app-consultant',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideAngularModule, TeamFilterPipe],
  templateUrl: './consultant.component.html',
  styleUrl: './consultant.component.scss'
})
export class ConsultantComponent implements OnInit, OnDestroy {

  activeTabId: string = 'tasks';
  currentUserId = '';

  tasks: Task[] = [];
  myStreams: any[] = [];
  projects: { id: string, name: string }[] = [];
  projectNames: Map<string, string> = new Map();
  availablePlugins: any[] = [];

  selectedIds: Set<string> = new Set();
  loading = false;

  searchQuery = '';
  filterStatus = 'all';
  selectedProjectId = '';
  filterStreamId = '';

  private donutChart: Chart | null = null;
  private barChart: Chart | null = null;
  private subs: Subscription[] = [];
  private apiUrl = environment.apiUrl;

  statsBottom = {
    pendingTasks: 0,
    completionRate: 0,
    activeStreams: 0,
    toolsUsed: 0
  };

  readonly ChevronRight = ChevronRight;
  readonly Layers = Layers;

  constructor(
    private tasksService: TasksService,
    private http: HttpClient,
    private router: Router,
    private notificationService: NotificationService,
    private pluginBridge: PluginBridgeService,
    private toastService: ToastService,
    public utils: UtilsService,
    private chartService: ChartService,
    public tabsService: TabsService,
    private authService: AuthService
  ) {}

  ngOnInit(): void {
    const userInfo = this.authService.getUserInfo();
    this.currentUserId = userInfo?.id || userInfo?.sub || '';
    this.loadAll();

    this.subs.push(
      this.notificationService.notifications$.subscribe(() => this.loadTasks()),
      this.tabsService.activeTabId.subscribe(id => this.activeTabId = id)
    );
  }

  ngOnDestroy(): void {
    this.subs.forEach(s => s.unsubscribe());
  }

  loadAll(): void {
    this.loadTasks();
    this.loadProjectNames();
    this.pluginBridge.getAllPlugins().subscribe({
      next: (plugins) => this.availablePlugins = plugins
    });
    this.http.get<any[]>(`${this.apiUrl}/streams/my`).subscribe({
      next: (streams) => {
        this.myStreams = streams;
        this.statsBottom.activeStreams = streams.length;
      }
    });
  }

  loadTasks(): void {
    this.loading = true;
    this.tasksService.getMyTasks().subscribe({
      next: (tasks) => {
        this.tasks = tasks;
        this.loading = false;
        this.loadProjects();
        this.computeStats();
      },
      error: () => this.loading = false
    });
  }

  loadProjects(): void {
    this.projects = [];
    const seen = new Set<string>();
    this.tasks.forEach(t => {
      if (t.projectId && !seen.has(t.projectId)) {
        seen.add(t.projectId);
        const name = this.projectNames.get(t.projectId) || t.projectId;
        this.projects.push({ id: t.projectId, name });
      }
    });
  }

  loadProjectNames(): void {
    this.http.get<{ id: string, name: string }[]>(`${this.apiUrl}/projects`).subscribe({
      next: (projects) => {
        projects.forEach(p => this.projectNames.set(p.id, p.name));
        this.loadProjects();
      }
    });
  }

  computeStats(): void {
    this.statsBottom.pendingTasks = this.tasks.filter(t => t.status === 0).length;
    this.statsBottom.completionRate = this.tasks.length
      ? Math.round((this.tasks.filter(t => t.status === 2).length / this.tasks.length) * 100)
      : 0;
    this.statsBottom.toolsUsed = new Set(this.tasks.map(t => t.toolName).filter(Boolean)).size;
    setTimeout(() => this.renderCharts(), 100);
  }

  renderCharts(): void {
    this.donutChart = this.chartService.createDoughnut(
      'consultantDonutChart',
      ['Pending', 'Blocked', 'Done'],
      [
        this.tasks.filter(t => t.status === 0).length,
        this.tasks.filter(t => t.status === 1).length,
        this.tasks.filter(t => t.status === 2).length
      ],
      ['#f59e0b', '#ef4444', '#10b981'],
      this.donutChart
    );

    this.barChart = this.chartService.createBar(
      'consultantBarChart',
      this.chartService.getLast6MonthsLabels(),
      this.chartService.getLast6MonthsData(this.tasks, 'createdAt'),
      '#3b82f6',
      this.barChart
    );
  }

  // ===== FILTRES =====
  get filteredProjectIds(): string[] {
    return this.selectedProjectId
      ? [this.selectedProjectId]
      : [...new Set(this.tasks.map(t => t.projectId).filter((id): id is string => !!id))];
  }

  get filteredStreams(): any[] {
    if (!this.selectedProjectId) return this.myStreams;
    return this.myStreams.filter(s => s.projectId === this.selectedProjectId);
  }

  get availableTools(): string[] {
    return [...new Set(this.tasks.map(t => t.toolName).filter(Boolean))];
  }

  onProjectChange(): void {
    this.filterStreamId = '';
  }

  getTasksForProject(projectId: string): Task[] {
    let tasks = this.tasks.filter(t => t.projectId === projectId);

    if (this.filterStreamId)
      tasks = tasks.filter(t => t.streamId === this.filterStreamId);

    if (this.searchQuery)
      tasks = tasks.filter(t => t.title.toLowerCase().includes(this.searchQuery.toLowerCase()));

    if (this.filterStatus !== 'all')
      tasks = tasks.filter(t => t.status === +this.filterStatus);

    // Done en bas
    return [
      ...tasks.filter(t => t.status !== 2),
      ...tasks.filter(t => t.status === 2)
    ];
  }

  // ===== TOOLS =====
  openTool(task: Task): void {
    if (task.status === 1) return;
    const plugin = this.availablePlugins.find(p => p.id === task.toolName);
    if (plugin?.accessUrl) {
      window.open(plugin.accessUrl, '_blank');
    } else {
      const routes: { [key: string]: string } = {
        'axeIAM': '/plugins/axe-iam',
        'axeBPM': '/plugins/axe-bpm',
        'axeGUI': '/plugins/axe-gui'
      };
      const route = routes[task.toolName];
      if (route) this.router.navigate([route], { queryParams: { projectId: task.projectId } });
    }
  }

  // ===== STREAMS TAB =====
  openStreamsTab(): void {
    this.tabsService.openTab({
      id: 'my-streams',
      title: 'My Streams',
      type: 'create-project'
    });
  }

  getStreamTeamType(stream: any): string {
    // Chercher le TeamType du consultant dans ce stream
    const member = stream.members?.find((m: any) => m.consultantId === this.currentUserId);
    return member?.teamType || '—';
  }

  getProjectName(projectId: string): string {
    return this.projectNames.get(projectId) || '—';
  }

  // ===== SELECTION =====
  toggleSelect(taskId: string): void {
    if (this.selectedIds.has(taskId)) this.selectedIds.delete(taskId);
    else this.selectedIds.add(taskId);
  }

  isSelected(taskId: string): boolean { return this.selectedIds.has(taskId); }
  get hasSelection(): boolean { return this.selectedIds.size > 0; }

  isBlocked(task: Task): boolean { return task.status === 1; }

  markDone(): void {
    Array.from(this.selectedIds).forEach(id => {
      this.tasksService.updateStatus(id, 2).subscribe({
        next: () => {
          const task = this.tasks.find(t => t.id === id);
          if (task) task.status = 2;
          this.selectedIds.clear();
          this.computeStats();
        }
      });
    });
  }
}