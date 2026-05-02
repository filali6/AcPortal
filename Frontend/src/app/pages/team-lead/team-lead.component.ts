import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { AuthService } from '../../core/services/auth.service';
import { TasksService, Task } from '../../core/services/tasks.service';
import { NotificationService } from '../../core/services/notification.service';
import { TabsService } from '../../core/services/tabs.service';
import { ToastService } from '../../core/services/toast.service';
import { UtilsService } from '../../core/services/utils.service';
import { ChartService } from '../../core/services/chart.service';
import { PluginBridgeService } from '../../core/services/plugin-bridge.service';
import { LucideAngularModule, ChevronRight, Layers } from 'lucide-angular';
import { environment } from '../../../environments/environment';
import { Subscription } from 'rxjs';
import { Chart, registerables } from 'chart.js';
Chart.register(...registerables);
import { TeamFilterPipe } from '../../core/pipes/team-filter.pipe';

@Component({
  selector: 'app-team-lead',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideAngularModule,TeamFilterPipe],
  templateUrl: './team-lead.component.html',
  styleUrl: './team-lead.component.scss'
})
export class TeamLeadComponent implements OnInit, OnDestroy {

  activeTabId: string = 'tasks';
  userRole = '';
  currentUserId = '';

  myTasks: Task[] = [];
  myStreams: any[] = [];
  projects: any[] = [];
  availablePlugins: any[] = [];

  // Filtres
  filterProjectId = '';
  filterStreamId = '';
  filterStatus = 'all';
  searchQuery = '';
  selectedStream: any = null;

  selectedIds: Set<string> = new Set();
  openTabs: { [tabId: string]: { task: any, steps: any[] } } = {};

  loading = false;
  private api = environment.apiUrl;
  private subs: Subscription[] = [];

  private donutChart: Chart | null = null;
  private barChart: Chart | null = null;

  statsBottom = {
    pendingTasks: 0,
    completionRate: 0,
    activeStreams: 0,
    nextDelivery: { name: '', date: '' }
  };

  readonly ChevronRight = ChevronRight;
  readonly Layers = Layers;

  constructor(
    private http: HttpClient,
    private authService: AuthService,
    private tasksService: TasksService,
    private notificationService: NotificationService,
    public tabsService: TabsService,
    private toastService: ToastService,
    public utils: UtilsService,
    private chartService: ChartService,
    private pluginBridge: PluginBridgeService
  ) {}

  ngOnInit(): void {
    const userInfo = this.authService.getUserInfo();
    this.currentUserId = userInfo?.id || userInfo?.sub || '';
    this.userRole = userInfo?.role || '';
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
    this.http.get<any[]>(`${this.api}/streams/my`).subscribe({
      next: (streams) => {
        this.myStreams = streams;
        this.loadProjects();
        this.refreshTasks();
        this.pluginBridge.getAllPlugins().subscribe({
          next: (plugins) => this.availablePlugins = plugins
        });
      }
    });
  }

  loadProjects(): void {
    const seen = new Set<string>();
    this.myStreams.forEach(s => {
      if (!seen.has(s.projectId)) {
        seen.add(s.projectId);
        this.http.get<any>(`${this.api}/projects/${s.projectId}`).subscribe({
          next: (p) => {
            if (!this.projects.find(x => x.id === p.id))
              this.projects.push(p);
          }
        });
      }
    });
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
    this.statsBottom.activeStreams = this.myStreams.length;

    const upcoming = this.projects
      .filter(p => p.targetDate)
      .sort((a, b) => new Date(a.targetDate).getTime() - new Date(b.targetDate).getTime());

    this.statsBottom.nextDelivery = upcoming[0]
      ? { name: upcoming[0].name, date: new Date(upcoming[0].targetDate).toLocaleDateString('en-GB') }
      : { name: '—', date: '—' };

    setTimeout(() => this.renderCharts(), 100);
  }

  renderCharts(): void {
    this.donutChart = this.chartService.createDoughnut(
      'tlDonutChart',
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
      'tlBarChart',
      this.chartService.getLast6MonthsLabels(),
      this.chartService.getLast6MonthsData(this.myTasks, 'createdAt'),
      '#3b82f6',
      this.barChart
    );
  }

  // ===== FILTRES =====
  get filteredStreams(): any[] {
    if (!this.filterProjectId) return this.myStreams;
    return this.myStreams.filter(s => s.projectId === this.filterProjectId);
  }

  get filteredTasks(): Task[] {
    return this.myTasks.filter(t => {
      const matchSearch = t.title.toLowerCase().includes(this.searchQuery.toLowerCase());
      const matchStatus = this.filterStatus === 'all' || t.status === +this.filterStatus;
      const matchProject = !this.filterProjectId || t.projectId === this.filterProjectId;
      const matchStream = !this.filterStreamId || t.streamId === this.filterStreamId;
      return matchSearch && matchStatus && matchProject && matchStream;
    });
  }

  get workflowTasks(): Task[] {
    return this.filteredTasks.filter(t => !t.stepId);
  }

  get stepTasks(): Task[] {
    return this.filteredTasks.filter(t => !!t.stepId);
  }

  // ===== NAVIGATION =====
  openStreamsTab(): void {
    this.tabsService.openTab({
      id: 'my-streams',
      title: 'My Streams',
      type: 'create-project'
    });
  }

  // ===== TASK CLICK =====
  onTaskClick(task: any): void {
    if (task.status === 2) return;
    if (task.stepId) {
      // Tâche depuis step → ouvrir l'outil
      this.openTool(task);
      return;
    }
    // Tâche workflow → définir les steps
    const tabId = `define-steps-${task.id}`;
    if (!this.openTabs[tabId]) {
      this.openTabs[tabId] = {
        task,
        steps: [{ stepName: '', toolName: '', order: 1, dependsOnStepId: null }]
      };
    }
    this.tabsService.openTab({
      id: tabId,
      title: task.title,
      type: 'define-steps',
      data: task
    });
  }

  openTool(task: any): void {
    const plugin = this.availablePlugins.find(p => p.id === task.toolName);
    if (plugin?.accessUrl) {
      window.open(plugin.accessUrl, '_blank');
    }
  }

  // ===== STEPS =====
  getTabData(tabId: string) { return this.openTabs[tabId] || null; }
  getOpenTabIds(): string[] { return Object.keys(this.openTabs); }

  addStep(tabId: string): void {
    const tab = this.openTabs[tabId];
    if (!tab) return;
    tab.steps.push({ stepName: '', toolName: '', order: tab.steps.length + 1, dependsOnStepId: null });
  }

  removeStep(tabId: string, index: number): void {
    const tab = this.openTabs[tabId];
    if (!tab) return;
    tab.steps.splice(index, 1);
    tab.steps.forEach((s, i) => s.order = i + 1);
  }

  saveSteps(tabId: string): void {
    const tab = this.openTabs[tabId];
    if (!tab) return;

    const stream = this.myStreams.find(s => s.projectId === tab.task.projectId);
    if (!stream) {
      this.toastService.show('Stream not found', 'error');
      return;
    }

    this.loading = true;
    const payload = {
      projectId: tab.task.projectId,
      steps: tab.steps.map(s => ({
        stepName: s.stepName,
        toolName: s.toolName,
        order: s.order,
        canBeParallel: false,
        dependsOnStepId: null,
        streamId: stream.id
      }))
    };

    this.http.post(`${this.api}/steps`, payload).subscribe({
      next: () => {
        this.toastService.show('Steps saved!', 'success');
        this.loading = false;
        this.endTask(tabId);
      },
      error: () => {
        this.toastService.show('Error saving steps', 'error');
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
    return this.projects.find(p => p.id === projectId)?.name || '—';
  }

  getStreamName(streamId: string): string {
    return this.myStreams.find(s => s.id === streamId)?.name || '—';
  }
  selectStream(stream: any): void {
  this.selectedStream = stream;
  this.tabsService.openTab({
    id: `stream-detail-${stream.id}`,
    title: stream.name,
    type: 'create-project'
  });
}
}