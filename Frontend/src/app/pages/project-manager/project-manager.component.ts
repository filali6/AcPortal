import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth.service';
import { TasksService, Task } from '../../core/services/tasks.service';
import { NotificationService } from '../../core/services/notification.service';
import { ProjectsService } from '../../core/services/projects.service';
import { UsersService } from '../../core/services/users.service';
import { StreamsService } from '../../core/services/streams.service';
import { TabsService } from '../../core/services/tabs.service';
import { ToastService } from '../../core/services/toast.service';
import { UtilsService } from '../../core/services/utils.service';
import { ChartService } from '../../core/services/chart.service';
import { LucideAngularModule, ChevronRight, Layers } from 'lucide-angular';
import { Subscription } from 'rxjs';
import { Chart, registerables } from 'chart.js';
Chart.register(...registerables);
import { TeamFilterPipe } from '../../core/pipes/team-filter.pipe';
import { ModalComponent } from '../../core/components/modal/modal.component';
@Component({
  selector: 'app-project-manager',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideAngularModule, ModalComponent, TeamFilterPipe],
  templateUrl: './project-manager.component.html',
  styleUrl: './project-manager.component.scss'
})
export class ProjectManagerComponent implements OnInit, OnDestroy {

  activeTabId: string = 'tasks';
  myTasks: Task[] = [];
  projects: any[] = [];
  bizLeads: any[] = [];
  techLeads: any[] = [];
  consultants: any[] = [];
  selectedIds: Set<string> = new Set();
  searchQuery = '';
  filterStatus = 'all';

  openTabs: { [tabId: string]: {
    task: any,
    streamName: string,
    selectedBizLeadId: string,
    selectedTechLeadId: string,
    businessTeamConsultants: string[],
    technicalTeamConsultants: string[]
  }} = {};

  loading = false;
  currentUserId = '';
  selectedProjectDetail: any = null;
projectStreams: any[] = [];
showAddConsultantModal = false;
addConsultantStreamId = '';
addConsultantTeam: 'business' | 'technical' = 'business';
selectedAddConsultantId = '';
editingLeadsStreamId = '';
editingBizLeadId = '';
editingTechLeadId = '';
showEditLeadsModal = false;

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
  readonly Layers = Layers;

  constructor(
    private authService: AuthService,
    private tasksService: TasksService,
    private notificationService: NotificationService,
    private projectsService: ProjectsService,
    private usersService: UsersService,
    private streamsService: StreamsService,
    public tabsService: TabsService,
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
    this.refreshTasks();
    this.projectsService.getManagedProjects().subscribe({ next: (p) => {
      this.projects = p;
      this.computeStats();
    }});
    this.usersService.getLeads().subscribe({
      next: (users) => {
        this.bizLeads = users.filter((u: any) => u.role === 'BusinessTeamLead');
        this.techLeads = users.filter((u: any) => u.role === 'TechnicalTeamLead');
      }
    });
    this.usersService.getAll().subscribe({
      next: (users) => this.consultants = users.filter((u: any) => u.role === 'Consultant')
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
    this.statsBottom.activeProjects = this.projects.length;

    const upcoming = this.projects
      .filter((p: any) => p.targetDate)
      .sort((a: any, b: any) => new Date(a.targetDate).getTime() - new Date(b.targetDate).getTime());

    this.statsBottom.nextDelivery = upcoming[0]
      ? { name: upcoming[0].name, date: new Date(upcoming[0].targetDate).toLocaleDateString('en-GB') }
      : { name: '—', date: '—' };

    setTimeout(() => this.renderCharts(), 100);
  }

  renderCharts(): void {
    this.donutChart = this.chartService.createDoughnut(
      'pmDonutChart',
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
      'pmBarChart',
      this.chartService.getLast6MonthsLabels(),
      this.chartService.getLast6MonthsData(this.projects),
      '#3b82f6',
      this.barChart
    );
  }

  get filteredTasks(): Task[] {
    return this.myTasks.filter(t => {
      const matchSearch = t.title.toLowerCase().includes(this.searchQuery.toLowerCase());
      const matchStatus = this.filterStatus === 'all' || t.status === +this.filterStatus;
      return matchSearch && matchStatus;
    });
  }

  onTaskClick(task: any): void {
    if (task.status === 2) return;
    const tabId = `create-stream-${task.id}`;
    if (!this.openTabs[tabId]) {
      this.openTabs[tabId] = {
        task,
        streamName: '',
        selectedBizLeadId: '',
        selectedTechLeadId: '',
        businessTeamConsultants: [],
        technicalTeamConsultants: []
      };
    }
    this.tabsService.openTab({
      id: tabId,
      title: task.title,
      type: 'create-stream',
      data: task
    });
  }

  getTabData(tabId: string) { return this.openTabs[tabId] || null; }
  getOpenTabIds(): string[] { return Object.keys(this.openTabs); }

  isConsultantSelected(tabId: string, consultantId: string, team: 'business' | 'technical'): boolean {
    const tab = this.openTabs[tabId];
    if (!tab) return false;
    return team === 'business'
      ? tab.businessTeamConsultants.includes(consultantId)
      : tab.technicalTeamConsultants.includes(consultantId);
  }

  toggleConsultant(tabId: string, consultantId: string, team: 'business' | 'technical'): void {
    const tab = this.openTabs[tabId];
    if (!tab) return;
    const list = team === 'business' ? tab.businessTeamConsultants : tab.technicalTeamConsultants;
    const index = list.indexOf(consultantId);
    if (index > -1) list.splice(index, 1);
    else list.push(consultantId);
  }

  createStream(tabId: string): void {
    const tab = this.openTabs[tabId];
    if (!tab || !tab.streamName) {
      this.toastService.show('Stream name is required', 'error');
      return;
    }
    this.loading = true;

    this.streamsService.create(
      tab.streamName,
      tab.task.projectId,
      tab.selectedBizLeadId || null,
      tab.selectedTechLeadId || null,
      tab.businessTeamConsultants,
      tab.technicalTeamConsultants
    ).subscribe({
      next: () => {
        this.toastService.show('Stream created successfully!', 'success');
        this.loading = false;
        this.endTask(tabId);
      },
      error: () => {
        this.toastService.show('Error creating stream', 'error');
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

  openProjectsTab(): void {
    this.tabsService.openTab({
      id: 'my-projects',
      title: 'My Projects',
      type: 'create-project'
    });
  }

  getProjectName(projectId: string): string {
    return this.projects.find(p => p.id === projectId)?.name || '—';
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
  selectProjectDetail(project: any): void {
  this.projectsService.getDetails(project.id).subscribe({
    next: (d) => {
      this.selectedProjectDetail = d;
      this.tabsService.openTab({
        id: `pm-project-${project.id}`,
        title: project.name,
        type: 'create-project'
      });
    }
  });
}

openAddConsultantModal(streamId: string, team: 'business' | 'technical'): void {
  this.addConsultantStreamId = streamId;
  this.addConsultantTeam = team;
  this.selectedAddConsultantId = '';
  this.showAddConsultantModal = true;
}

closeAddConsultantModal(): void {
  this.showAddConsultantModal = false;
  this.addConsultantStreamId = '';
  this.selectedAddConsultantId = '';
}

addConsultant(): void {
  if (!this.selectedAddConsultantId || !this.addConsultantStreamId) return;
  this.loading = true;
  this.streamsService.addMember(
    this.addConsultantStreamId,
    this.selectedAddConsultantId,
    this.addConsultantTeam === 'business' ? 0 : 1
  ).subscribe({
    next: () => {
      this.toastService.show('Consultant added!', 'success');
      this.loading = false;
      this.closeAddConsultantModal();
      this.refreshProjectDetail();
    },
    error: () => {
      this.toastService.show('Error adding consultant', 'error');
      this.loading = false;
    }
  });
}

removeConsultant(streamId: string, consultantId: string): void {
  this.streamsService.removeMember(streamId, consultantId).subscribe({
    next: () => {
      this.toastService.show('Consultant removed!', 'success');
      this.refreshProjectDetail();
    },
    error: () => this.toastService.show('Error removing consultant', 'error')
  });
}

openEditLeadsModal(stream: any): void {
  this.editingLeadsStreamId = stream.id;
  this.editingBizLeadId = stream.businessTeamLead?.id || '';
  this.editingTechLeadId = stream.technicalTeamLead?.id || '';
  this.showEditLeadsModal = true;
}

closeEditLeadsModal(): void {
  this.showEditLeadsModal = false;
  this.editingLeadsStreamId = '';
}

saveLeads(): void {
  if (!this.editingLeadsStreamId) return;
  this.loading = true;
  this.streamsService.updateLeads(
    this.editingLeadsStreamId,
    this.editingBizLeadId || undefined,
    this.editingTechLeadId || undefined
  ).subscribe({
    next: () => {
      this.toastService.show('Leads updated!', 'success');
      this.loading = false;
      this.closeEditLeadsModal();
      this.refreshProjectDetail();
    },
    error: () => {
      this.toastService.show('Error updating leads', 'error');
      this.loading = false;
    }
  });
}

refreshProjectDetail(): void {
  if (!this.selectedProjectDetail) return;
  this.projectsService.getDetails(this.selectedProjectDetail.id).subscribe({
    next: (d) => this.selectedProjectDetail = d
  });
}

getAvailableConsultants(streamId: string): any[] {
  if (!this.selectedProjectDetail) return this.consultants;
  const stream = this.selectedProjectDetail.streams?.find((s: any) => s.id === streamId);
  if (!stream) return this.consultants;
  const assignedIds = stream.members?.map((m: any) => m.consultantId) || [];
  return this.consultants.filter(c => !assignedIds.includes(c.id));
}
}