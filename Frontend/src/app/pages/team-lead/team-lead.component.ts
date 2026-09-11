import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth.service';
import { TasksService, Task } from '../../core/services/tasks.service';
import { StreamsService } from '../../core/services/streams.service';
import { ProjectsService } from '../../core/services/projects.service';
import { NotificationService } from '../../core/services/notification.service';
import { TabsService } from '../../core/services/tabs.service';
import { ToastService } from '../../core/services/toast.service';
import { UtilsService } from '../../core/services/utils.service';
import { ChartService } from '../../core/services/chart.service';
import { PluginBridgeService } from '../../core/services/plugin-bridge.service';
import { LucideAngularModule, ChevronRight, Layers, MessageSquare } from 'lucide-angular';
import { Subscription } from 'rxjs';
import { Chart, registerables } from 'chart.js';
Chart.register(...registerables);
import { TeamFilterPipe } from '../../core/pipes/team-filter.pipe';
import { ChatPanelComponent } from '../../core/components/chat-panel/chat-panel.component';
import { ChatService } from '../../core/services/chat.service';
import { KeycloakService } from 'keycloak-angular';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { DrawerService } from '../../core/services/drawer.service';
import { BriefingCardComponent } from '../../core/components/briefing-card/briefing-card.component';
import { GitService } from '../../core/services/git.service';

@Component({
  selector: 'app-team-lead',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideAngularModule, TeamFilterPipe, ChatPanelComponent, TranslateModule,BriefingCardComponent],
  templateUrl: './team-lead.component.html',
  styleUrl: './team-lead.component.scss'
})
export class TeamLeadComponent implements OnInit, OnDestroy {

  activeTabId = 'tasks';
  userRole = '';
  currentUserId = '';

  myTasks: Task[] = [];
  myStreams: any[] = [];
  projects: any[] = [];
  availablePlugins: any[] = [];

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

  chatOpen = false;
  chatStreamId: string | null = null;
  chatTaskId: string | null = null;
  chatTitle = '';

  streamConfigs: { [streamId: string]: any } = {};
  loadingConfigs = false;
  validatingStream = false;

  readonly ChevronRight = ChevronRight;
  readonly Layers = Layers;
  readonly MessageSquare = MessageSquare;

  constructor(
    private http: HttpClient,
    private authService: AuthService,
    private tasksService: TasksService,
    private streamsService: StreamsService,
    private projectsService: ProjectsService,
    private notificationService: NotificationService,
    public tabsService: TabsService,
    private toastService: ToastService,
    public utils: UtilsService,
    private chartService: ChartService,
    private pluginBridge: PluginBridgeService,
    private chatService: ChatService,
    private keycloak: KeycloakService,
    private translate: TranslateService,
    private drawerService:DrawerService,
    private gitService: GitService
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
    this.streamsService.getMyStreams().subscribe({
      next: (streams) => {
        this.myStreams = streams;
        const token = this.keycloak.getKeycloakInstance().token || '';
        this.chatService.startConnection(token).then(() => {
          streams.forEach((s: any) => {
            this.chatService.joinStreamChat(s.id);

            
            this.tasksService.getByStream(s.id).subscribe({
  next: (tasks: Task[]) => tasks.forEach(t => this.chatService.joinTaskChatSilent(t.id))
});
          });
        });
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
        this.projectsService.getDetails(s.projectId).subscribe({
          next: (p: any) => {
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
      [
        this.translate.instant('TASKS.PENDING'),
        this.translate.instant('TASKS.BLOCKED'),
        this.translate.instant('TASKS.DONE')
      ],
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

  get workflowTasks(): Task[] { return this.filteredTasks.filter(t => !t.stepId); }
  get stepTasks(): Task[] { return this.filteredTasks.filter(t => !!t.stepId); }

  // ===== NAVIGATION =====
  openStreamsTab(): void {
    this.tabsService.openTab({ id: 'my-streams', title: 'My Streams', type: 'create-project' });
  }

  // ===== TASK CLICK =====
  onTaskClick(task: any): void {
    if (task.status === 2) return;
    if (task.stepId) { this.openTool(task); return; }
    const tabId = `define-steps-${task.id}`;
    if (!this.openTabs[tabId]) {
      this.openTabs[tabId] = {
        task,
        steps: [{ stepName: '', toolName: '', order: 1, dependsOnStepId: null }]
      };
    }
    this.tabsService.openTab({ id: tabId, title: task.title, type: 'define-steps', data: task });
  }

  openTool(task: any): void {
    const plugin = this.availablePlugins.find(p => p.id === task.toolName);
    if (plugin?.accessUrl) window.open(plugin.accessUrl, '_blank');
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
    if (!stream) { this.toastService.show('Stream not found', 'error'); return; }

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

    // ✅ utilise http directement car pas de StepsService existant
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
    this.loadStreamConfigs(stream.id);
}

  openStreamChat(stream: any): void {
    this.chatStreamId = stream.id;
    this.chatTaskId = null;
    this.chatTitle = `${stream.name} — Team Chat`;
    this.chatOpen = true;
    this.drawerService.open();
  }

  openTaskComments(task: any, streamId: string): void {
    console.log('task:', task);
    console.log('streamId:', streamId);
    this.chatTaskId = task.id;
    this.chatStreamId = streamId;
    this.chatTitle = task.title || task.stepName;
    this.chatOpen = true;
    this.drawerService.open();
}

  closeChat(): void {
    this.chatOpen = false;
    this.drawerService.close();
  }
   loadStreamConfigs(streamId: string): void {
    this.loadingConfigs = true;
    this.gitService.getStreamConfigs(streamId).subscribe({
        next: (configs) => {
            this.streamConfigs[streamId] = configs;
            this.loadingConfigs = false;
        },
        error: () => this.loadingConfigs = false
    });
}

validateStream(streamId: string): void {
    this.validatingStream = true;
    this.gitService.validateStream(streamId).subscribe({
        next: (result) => {
            this.toastService.show(`✅ ${result.message}`, 'success');
            this.validatingStream = false;
        },
        error: () => {
            this.toastService.show('❌ Failed to validate stream', 'error');
            this.validatingStream = false;
        }
    });
}
openExternalLink(url: string): void {
    window.open(url, '_blank');
}
}