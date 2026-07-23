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
import { LucideAngularModule, ChevronRight, Layers, Edit, Sparkles, FileUp, Check, Send ,Trash2} from 'lucide-angular';
import { Subscription } from 'rxjs';
import { Chart, registerables } from 'chart.js';
Chart.register(...registerables);
import { TeamFilterPipe } from '../../core/pipes/team-filter.pipe';
import { ModalComponent } from '../../core/components/modal/modal.component';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { BriefingCardComponent } from '../../core/components/briefing-card/briefing-card.component';
import { PlanningService } from '../../core/services/planning.service';

@Component({
  selector: 'app-project-manager',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideAngularModule, ModalComponent, TeamFilterPipe, TranslateModule, BriefingCardComponent],
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

  showPlanHistoryModal = false;
planHistory: any[] = [];
selectedHistoryPlan: any = null;

  openTabs: { [tabId: string]: {
    task: any,
    selectedMode: 'manual' | 'ai' | null,
    aiSubStep: 'upload' | 'review' | 'done',
    streamName: string,
    selectedBizLeadId: string,
    selectedTechLeadId: string,
    businessTeamConsultants: string[],
    technicalTeamConsultants: string[],
    guidelines: string,
    file: File | null,
    proposalId: string | null,
    editingStreamIndex:number|null,
    plan: any,
    refineMessage: string,
    chatHistory: { role: string, content: string }[]
    
  }} = {};

  openDrop: string | null = null;
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
  generatingPlan = false;
  refiningPlan = false;
  approvingPlan = false;

  private donutChart: Chart | null = null;
  private barChart: Chart | null = null;
  private subs: Subscription[] = [];

  statsBottom = {
    pendingTasks: 0,
    completionRate: 0,
    activeProjects: 0,
    activeStreams: 0
  };

  readonly ChevronRight = ChevronRight;
  readonly Layers = Layers;
  readonly Edit = Edit;
  readonly Sparkles = Sparkles;
  readonly FileUp = FileUp;
  readonly Check = Check;
  readonly Send = Send;
  readonly Trash2=Trash2;

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
    private chartService: ChartService,
    private translate: TranslateService,
    private planningService: PlanningService
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
    this.statsBottom.activeStreams = this.projects.reduce((sum, p) => sum + (p.streamCount || 0), 0);
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

  // ===== TABS =====

  onTaskClick(task: any): void {
    if (task.status === 2) return;
    const tabId = `create-stream-${task.id}`;
    if (!this.openTabs[tabId]) {
      this.openTabs[tabId] = {
        task,
        selectedMode: null,
        aiSubStep: 'upload',
        streamName: '',
        selectedBizLeadId: '',
        selectedTechLeadId: '',
        businessTeamConsultants: [],
        technicalTeamConsultants: [],
        guidelines: '',
        file: null,
        proposalId: null,
        plan: null,
        editingStreamIndex: null,
        refineMessage: '',
        chatHistory: []
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

  // ===== PLANNING MODE =====

  selectPlanningMode(tabId: string, mode: 'manual' | 'ai'): void {
    const tab = this.openTabs[tabId];
    if (tab) tab.selectedMode = mode;
  }

  // ===== FLUX MANUEL =====

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

  getConsultantsForTab(tabId: string, side: 'business' | 'technical') {
    const tab = this.openTabs[tabId];
    if (!tab) return [];
    const ids = side === 'business' ? tab.businessTeamConsultants : tab.technicalTeamConsultants;
    return this.consultants.filter((c: any) => ids.includes(c.id));
  }

  addConsultantFromDrop(tabId: string, c: any, side: 'business' | 'technical') {
    const alreadyUsed =
      this.isConsultantSelected(tabId, c.id, 'business') ||
      this.isConsultantSelected(tabId, c.id, 'technical');
    if (alreadyUsed) return;
    this.toggleConsultant(tabId, c.id, side);
    this.openDrop = null;
  }

  // ===== FLUX AGENT IA =====

  onFsdFileSelected(event: Event, tabId: string): void {
    const input = event.target as HTMLInputElement;
    const tab = this.openTabs[tabId];
    if (!tab || !input.files?.length) return;
    tab.file = input.files[0];
  }

  generatePlan(tabId: string): void {
    const tab = this.openTabs[tabId];
    if (!tab || !tab.file) {
      this.toastService.show('Please upload a FSD file', 'error');
      return;
    }
    this.generatingPlan = true;
    const formData = new FormData();
    formData.append('file', tab.file);
    formData.append('projectId', tab.task.projectId);
    formData.append('guidelines', tab.guidelines);

    this.planningService.generate(formData).subscribe({
      next: (res: any) => {
        tab.proposalId = res.proposalId;
        tab.plan = res.plan;
        tab.aiSubStep = 'review'; // ← correct
        this.generatingPlan = false;
      },
      error: () => {
        this.toastService.show('AI planning failed. Please try again.', 'error');
        this.generatingPlan = false;
      }
    });
  }

 sendRefineMessage(tabId: string): void {
    const tab = this.openTabs[tabId];
    if (!tab || !tab.refineMessage.trim() || !tab.proposalId) return;

    const message = tab.refineMessage.trim();
    tab.chatHistory.push({ role: 'user', content: message });
    tab.refineMessage = '';
    this.refiningPlan = true;

    this.planningService.refine(tab.proposalId, message, tab.plan).subscribe({
        next: (res: any) => {
            tab.plan = res.plan;
            const count = res.plan?.streams?.length || 0;
            tab.chatHistory.push({
                role: 'assistant',
                content: `Plan updated — ${count} stream${count > 1 ? 's' : ''}.`
            });
            this.refiningPlan = false;
        },
        error: () => {
            this.toastService.show('Refinement failed. Please try again.', 'error');
            tab.chatHistory.push({ role: 'assistant', content: 'Failed to update. Please try again.' });
            this.refiningPlan = false;
        }
    });
}

  approvePlan(tabId: string): void {
    const tab = this.openTabs[tabId];
    if (!tab || !tab.proposalId) return;

    this.approvingPlan = true;
    this.planningService.approve(tab.proposalId,tab.plan).subscribe({
      next: () => {
        this.toastService.show('Plan approved! Streams are being created.', 'success');
        this.approvingPlan = false;
        tab.aiSubStep = 'done'; // ← correct

        this.tasksService.updateStatus(tab.task.id, 2).subscribe(() => {
          this.refreshTasks();
        });

        setTimeout(() => this.endTask(tabId), 2000);
      },
      error: () => {
        this.toastService.show('Approval failed. Please try again.', 'error');
        this.approvingPlan = false;
      }
    });
  }

  getStreamConsultantCount(stream: any): number {
    return (stream.businessConsultantIds?.length || 0) + (stream.technicalConsultantIds?.length || 0);
  }

  // ===== PROJECTS TAB =====

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
    if (this.selectedProjectDetail?.id === project.id) {
      this.selectedProjectDetail = null;
      return;
    }
    this.projectsService.getDetails(project.id).subscribe({
      next: (d) => this.selectedProjectDetail = d
    });
  }

  getProgressColor(progress: number): string {
    if (progress >= 70) return '#10b981';
    if (progress >= 40) return '#f59e0b';
    return '#ef4444';
  }

  getStreamDone(stream: any): number {
    return stream.streamTasks?.filter((t: any) => t.status === 2).length || 0;
  }

  getStreamPending(stream: any): number {
    return stream.streamTasks?.filter((t: any) => t.status === 0).length || 0;
  }

  getStreamBlocked(stream: any): number {
    return stream.streamTasks?.filter((t: any) => t.status === 1).length || 0;
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

  getInitials(fullName: string): string {
    if (!fullName) return '?';
    return fullName.split(' ').map((w: string) => w[0]).join('').toUpperCase().slice(0, 2);
  }

  openExternalLink(url: string): void {
    window.open(url, '_blank');
  }

  getLeadById(id: string, type: 'biz' | 'tech') {
    if (!id) return null;
    const pool = type === 'biz' ? this.bizLeads : this.techLeads;
    return pool.find((u: any) => u.id === id) ?? null;
  }

  toggleDrop(key: string) {
    this.openDrop = this.openDrop === key ? null : key;
  }

  closeDrop(key: string) {
    if (this.openDrop === key) this.openDrop = null;
  }


  // ===== Édition inline des streams du plan IA =====

  startEditStream(tabId: string, index: number): void {
    const tab = this.openTabs[tabId];
    if (tab) tab.editingStreamIndex = index;
  }

  cancelEditStream(tabId: string): void {
    const tab = this.openTabs[tabId];
    if (tab) tab.editingStreamIndex = null;
  }

  updateStreamName(tabId: string, index: number, name: string): void {
    const tab = this.openTabs[tabId];
    if (tab?.plan?.streams?.[index])
      tab.plan.streams[index].name = name;
  }

  updateStreamLead(tabId: string, index: number, leadId: string, type: 'business' | 'technical'): void {
    const tab = this.openTabs[tabId];
    if (!tab?.plan?.streams?.[index]) return;
    if (type === 'business') tab.plan.streams[index].businessLeadId = leadId || null;
    else tab.plan.streams[index].technicalLeadId = leadId || null;
  }

  toggleStreamConsultant(tabId: string, streamIndex: number, consultantId: string, type: 'business' | 'technical'): void {
    const tab = this.openTabs[tabId];
    if (!tab?.plan?.streams?.[streamIndex]) return;
    const stream = tab.plan.streams[streamIndex];
    const list = type === 'business' ? stream.businessConsultantIds : stream.technicalConsultantIds;
    const idx = list.indexOf(consultantId);
    if (idx > -1) list.splice(idx, 1);
    else list.push(consultantId);
  }

  isStreamConsultantSelected(tabId: string, streamIndex: number, consultantId: string, type: 'business' | 'technical'): boolean {
    const tab = this.openTabs[tabId];
    if (!tab?.plan?.streams?.[streamIndex]) return false;
    const stream = tab.plan.streams[streamIndex];
    const list = type === 'business' ? stream.businessConsultantIds : stream.technicalConsultantIds;
    return list?.includes(consultantId) ?? false;
  }

  removeStream(tabId: string, index: number): void {
    const tab = this.openTabs[tabId];
    if (!tab?.plan?.streams) return;
    tab.plan.streams.splice(index, 1);
    tab.editingStreamIndex = null;
  }

  getLeadName(leadId: string): string {
    if (!leadId) return '';
    return this.bizLeads.find((u: any) => u.id === leadId)?.fullName
        || this.techLeads.find((u: any) => u.id === leadId)?.fullName
        || '—';
  }

  getConsultantName(consultantId: string): string {
    return this.consultants.find((c: any) => c.id === consultantId)?.fullName || '—';
  }
  openPlanHistoryModal(): void {
  if (!this.selectedProjectDetail) return;
  this.planningService.getByProject(this.selectedProjectDetail.id).subscribe({
    next: (proposals: any[]) => {
      this.planHistory = proposals;
      this.selectedHistoryPlan = null;
      this.showPlanHistoryModal = true;
    }
  });
}

viewHistoryPlan(proposal: any): void {
  this.selectedHistoryPlan = proposal;
}
}