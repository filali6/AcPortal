import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { UsersService } from '../../core/services/users.service';
import { PluginsAdminService, PluginDto } from '../../core/services/plugins-admin.service';
import { WorkflowService } from '../../core/services/Workflow.service';
import { ToastService } from '../../core/services/toast.service';
import { UtilsService } from '../../core/services/utils.service';
import { TabsService } from '../../core/services/tabs.service';
import { ChartService } from '../../core/services/chart.service';
import { ModalComponent } from '../../core/components/modal/modal.component';
import { LucideAngularModule, Users, Wrench, GitBranch, LayoutDashboard, Plus, Trash2, Edit, ChevronRight, Clock, RefreshCw } from 'lucide-angular';
import { Subscription } from 'rxjs';
import { ProjectsService } from '../../core/services/projects.service';
import { TasksService } from '../../core/services/tasks.service';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { Chart } from 'chart.js';
import { SlaService, SlaRule } from '../../core/services/sla.service';

@Component({
  selector: 'app-super-admin',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideAngularModule, ModalComponent, TranslateModule],
  templateUrl: './super-admin.component.html',
  styleUrl: './super-admin.component.scss'
})
export class SuperAdminComponent implements OnInit, OnDestroy {

  activeTabId: string = 'tasks';
  private subs: Subscription[] = [];

  // Users
  users: any[] = [];
  showUserModal = false;
  editingUser: any = null;
  userForm = { fullName: '', email: '', password: '', role: '',consultantType:'' };
  availableRoles = [
    'HeadOfCDS', 'PortfolioDirector', 'ProjectManager',
    'BusinessTeamLead', 'TechnicalTeamLead', 'Consultant', 'DAF', 'SuperAdmin'
  ];

  // Tools
  tools: PluginDto[] = [];
  showToolModal = false;
  editingTool: PluginDto | null = null;
  toolForm: PluginDto = {
    id: '', name: '', description: '',
    category: '', url: '', icon: '', ssoEnabled: false, isActive: true, allowedRoles: [],functionalDomain: ''
  };

  // Workflow
  workflowRules: any[] = [];
  actionTypes: string[] = [];
  targetTypes: string[] = [];
  showRuleModal = false;
  editingRule: any = null;
  ruleForm: any = {
    eventCode: '', actionType: '', taskTitle: '',
    taskDescription: '', targetType: '', targetValues: []
  };
  newTargetValue = '';
  showAddRuleModal = false;
  newRuleForm: any = {
    eventCode: '', actionType: 'CREATE_TASK', taskTitle: '',
    taskDescription: '', targetType: 'ROLE', targetValues: []
  };

  // SLA Rules (US59 / US60 / US61)
  slaRules: SlaRule[] = [];
  showSlaRuleModal = false;
  editingSlaRule: SlaRule | null = null;
  slaRuleForm: { name: string; description: string; type: number; slaDays: number } = {
    name: '', description: '', type: 1, slaDays: 5
  };
  applyingSlaRules = false;


  // Stats
  stats = {
    totalUsers: 0,
    totalTools: 0,
    totalRules: 0,
    totalProjects: 0,
    totalPortfolios: 0,
    tasksPending: 0,
    tasksBlocked: 0,
    tasksDone: 0,
    totalSlaRules: 0,
    usersByRole: [] as { role: string; count: number }[]
  };
  showConfirmModal = false;
  confirmMessage = '';
  confirmAction: (() => void) | null = null;

  loading = false;

  // Instances Chart.js — gardées en mémoire pour pouvoir les détruire/recréer
  private tasksChart: Chart | null = null;
  private systemChart: Chart | null = null;
  private rolesChart: Chart | null = null;
  private chartsRenderScheduled = false;

  readonly Users = Users;
  readonly Wrench = Wrench;
  readonly GitBranch = GitBranch;
  readonly LayoutDashboard = LayoutDashboard;
  readonly Plus = Plus;
  readonly Trash2 = Trash2;
  readonly Edit = Edit;
  readonly ChevronRight = ChevronRight;
  readonly Clock = Clock;
  readonly RefreshCw = RefreshCw;

  constructor(
    private usersService: UsersService,
    private pluginsAdminService: PluginsAdminService,
    private workflowService: WorkflowService,
    private toastService: ToastService,
    public utils: UtilsService,
    public tabsService: TabsService,
    private projectsService: ProjectsService,
    private tasksService: TasksService,
    private chartService: ChartService,
    private translate: TranslateService,
    private slaService: SlaService,
  ) {}

  ngOnInit(): void {
    this.loadAll();
    this.subs.push(
      this.tabsService.activeTabId.subscribe(id => {
        this.activeTabId = id;
        if (id === 'tasks') {
          this.renderCharts();
        }
      })
    );
  }

  ngOnDestroy(): void {
    this.subs.forEach(s => s.unsubscribe());
    this.tasksChart?.destroy();
    this.systemChart?.destroy();
    this.rolesChart?.destroy();
  }

  loadAll(): void {
    this.loadDashboard();
    this.loadUsers();
    this.loadTools();
    this.loadWorkflow();
    this.loadSlaRules();
  }

  openTab(type: 'users' | 'tools' | 'workflow' | 'sla'): void {
    const titles = { users: 'Users', tools: 'Tools', workflow: 'Workflow', sla: 'SLA Rules' };
    this.tabsService.openTab({
      id: type,
      title: titles[type],
      type: 'create-project'
    });
  }

  computeStats(): void {
    this.stats.totalUsers = this.users.length;
    this.stats.totalTools = this.tools.length;
    this.stats.totalRules = this.workflowRules.length;
    this.stats.totalSlaRules = this.slaRules.length;
    const roleCount: { [key: string]: number } = {};
    this.users.forEach(u => {
      roleCount[u.role] = (roleCount[u.role] || 0) + 1;
    });
    this.stats.usersByRole = Object.keys(roleCount).map(role => ({
      role, count: roleCount[role]
    }));
    this.renderCharts();
  }

  private renderCharts(): void {
    if (this.activeTabId !== 'tasks') return;
    if (this.chartsRenderScheduled) return;
    this.chartsRenderScheduled = true;

    setTimeout(() => {
      this.chartsRenderScheduled = false;

      try {
        this.tasksChart = this.chartService.createDoughnut(
          'chartTasksOverview',
          [
            this.translate.instant('TASKS.PENDING'),
            this.translate.instant('TASKS.BLOCKED'),
            this.translate.instant('TASKS.DONE')
          ],
          [this.stats.tasksPending, this.stats.tasksBlocked, this.stats.tasksDone],
          ['#f59e0b', '#ef4444', '#10b981'],
          this.tasksChart
        );
      } catch (e) {
        console.error('Erreur création chart Tasks Overview', e);
      }

      try {
        this.systemChart = this.chartService.createBar(
          'chartSystemStatus',
          [
            this.translate.instant('TOOLS.TITLE'),
            this.translate.instant('DASHBOARD.WORKFLOW_RULES'),
            this.translate.instant('SA.ROLES')
          ],
          [this.stats.totalTools, this.stats.totalRules, this.stats.usersByRole.length],
          '#0080ff',
          this.systemChart
        );
      } catch (e) {
        console.error('Erreur création chart System Status', e);
      }

      try {
        this.rolesChart = this.chartService.createDoughnut(
          'chartUsersByRole',
          this.stats.usersByRole.map(r => r.role),
          this.stats.usersByRole.map(r => r.count),
          this.stats.usersByRole.map(r => this.getRoleColor(r.role)),
          this.rolesChart
        );
      } catch (e) {
        console.error('Erreur création chart Users by Role', e);
      }
    });
  }

  loadUsers(): void {
    this.usersService.getAllForAdmin().subscribe({
      next: (users) => { this.users = users; this.computeStats(); }
    });
  }

  openCreateUserModal(): void {
    this.editingUser = null;
    this.userForm = { fullName: '', email: '', password: '', role: '',consultantType:'' };
    this.showUserModal = true;
  }

  openEditUserModal(user: any): void {
    this.editingUser = user;
    this.userForm = { fullName: user.fullName, email: user.email, password: '', role: user.role,consultantType:user.consultantType };
    this.showUserModal = true;
  }

  saveUser(): void {
    if (!this.userForm.fullName || !this.userForm.email) {
      this.toastService.show('Full name and email are required', 'error');
      return;
    }
    this.loading = true;

    if (this.editingUser) {
      this.usersService.updateUser(this.editingUser.id, {
        fullName: this.userForm.fullName,
        role: this.userForm.role,
        consultantType: this.userForm.consultantType || undefined
      }).subscribe({
        next: () => {
          this.toastService.show('User updated!', 'success');
          this.loading = false;
          this.showUserModal = false;
          this.loadUsers();
        },
        error: () => { this.toastService.show('Error updating user', 'error'); this.loading = false; }
      });
    } else {
      if (!this.userForm.password) {
        this.toastService.show('Password is required', 'error');
        this.loading = false;
        return;
      }
      this.usersService.createUser({
        fullName: this.userForm.fullName,
        email: this.userForm.email,
        password: this.userForm.password,
        role: this.userForm.role,
        consultantType: this.userForm.consultantType || undefined   
      }).subscribe({
        next: () => {
          this.toastService.show('User created!', 'success');
          this.loading = false;
          this.showUserModal = false;
          this.loadUsers();
        },
        error: () => { this.toastService.show('Error creating user', 'error'); this.loading = false; }
      });
    }
  }

  deleteUser(user: any): void {
    this.openConfirm(`Delete ${user.fullName}?`, () => {
      this.usersService.deleteUser(user.id).subscribe({
        next: () => { this.toastService.show('User deleted!', 'success'); this.loadUsers(); },
        error: () => this.toastService.show('Error deleting user', 'error')
      });
    });
  }


  loadTools(): void {
    this.pluginsAdminService.getAll().subscribe({
      next: (tools) => { this.tools = tools; this.computeStats(); }
    });
  }



  openEditToolModal(tool: PluginDto): void {
    this.editingTool = tool;
    this.toolForm = { ...tool };
    this.showToolModal = true;
  }
  openCreateToolModal(): void {
    this.editingTool = null;
    this.toolForm = {
      id: '', name: '', description: '',
      category: '', url: '', icon: '',
      ssoEnabled: false, isActive: true, allowedRoles: [],functionalDomain: ''
    };
    this.showToolModal = true;
  }
  deleteTool(tool: PluginDto): void {
    this.openConfirm(`Delete ${tool.name}?`, () => {
      this.pluginsAdminService.delete(tool.id).subscribe({
        next: () => { this.toastService.show('Tool deleted!', 'success'); this.loadTools(); },
        error: () => this.toastService.show('Error deleting tool', 'error')
      });
    });
  }

  saveTool(): void {
    if (!this.toolForm.name || (!this.editingTool && !this.toolForm.id)) {
      this.toastService.show('ID and Name are required', 'error');
      return;
    }
    this.loading = true;

    const payload = {
      ...this.toolForm,
      allowedRoles: JSON.stringify(this.toolForm.allowedRoles)
    };

    if (this.editingTool) {
      this.pluginsAdminService.update(this.editingTool.id, payload as any).subscribe({
        next: () => {
          this.toastService.show('Tool updated!', 'success');
          this.loading = false;
          this.showToolModal = false;
          this.loadTools();
        },
        error: () => { this.toastService.show('Error updating tool', 'error'); this.loading = false; }
      });
    } else {
      this.pluginsAdminService.create(payload as any).subscribe({
        next: () => {
          this.toastService.show('Tool created!', 'success');
          this.loading = false;
          this.showToolModal = false;
          this.loadTools();
        },
        error: () => { this.toastService.show('Error creating tool', 'error'); this.loading = false; }
      });
    }
  }

  // Aperçu de l'icône dans le modal / la table : si l'URL/chemin est invalide, on masque
  // l'image plutôt que de laisser le carré "image cassée" du navigateur.
  onIconPreviewError(event: Event): void {
    const img = event.target as HTMLImageElement;
    img.style.visibility = 'hidden';
  }

  loadWorkflow(): void {
    this.workflowService.getRules().subscribe({
      next: (rules) => { this.workflowRules = rules; this.computeStats(); }
    });
    this.workflowService.getActionTypes().subscribe({
      next: (types) => this.actionTypes = types
    });
    this.workflowService.getTargetTypes().subscribe({
      next: (types) => this.targetTypes = types
    });
  }

  openEditRuleModal(rule: any): void {
    this.editingRule = rule;
    // On copie aussi `config` (pas juste une référence) pour ne pas muter la règle
    // d'origine avant la sauvegarde, et pour garantir que `config.prompt` existe
    // même si la règle n'en avait pas encore (cas SUMMARIZE_CONTRACT sans prompt configuré).
    this.ruleForm = {
      ...rule,
      targetValues: [...rule.targetValues],
      config: rule.config ? { ...rule.config } : {}
    };
    this.showRuleModal = true;
  }

  openAddRuleModal(): void {
    this.newRuleForm = {
      eventCode: '', actionType: 'CREATE_TASK', taskTitle: '',
      taskDescription: '', targetType: 'ROLE', targetValues: []
    };
    this.showAddRuleModal = true;
  }

  addTargetValue(form: any): void {
    if (this.newTargetValue.trim())
      form.targetValues.push(this.newTargetValue.trim());
    this.newTargetValue = '';
  }

  removeTargetValue(form: any, index: number): void {
    form.targetValues.splice(index, 1);
  }

  saveRule(): void {
    const index = this.workflowRules.findIndex(r => r.eventCode === this.editingRule.eventCode);
    if (index > -1) this.workflowRules[index] = { ...this.ruleForm };
    this.workflowService.updateRules({ workflowRules: this.workflowRules }).subscribe({
      next: () => {
        this.toastService.show('Rule updated!', 'success');
        this.showRuleModal = false;
        this.loadWorkflow();
      },
      error: () => this.toastService.show('Error updating rule', 'error')
    });
  }

  addRule(): void {
    if (!this.newRuleForm.eventCode || !this.newRuleForm.actionType) {
      this.toastService.show('Event code and action type are required', 'error');
      return;
    }
    this.workflowRules.push({ ...this.newRuleForm });
    this.workflowService.updateRules({ workflowRules: this.workflowRules }).subscribe({
      next: () => {
        this.toastService.show('Rule added!', 'success');
        this.showAddRuleModal = false;
        this.loadWorkflow();
      },
      error: () => this.toastService.show('Error adding rule', 'error')
    });
  }

  // ─────────────────────────────────────────────────────────────
  // SLA Rules — US59 (créer) / US60 (modifier) / US61 (supprimer)
  // ─────────────────────────────────────────────────────────────

  loadSlaRules(): void {
    this.slaService.getAllRules().subscribe({
      next: (rules) => { this.slaRules = rules; this.computeStats(); },
      error: () => this.toastService.show('Error loading SLA rules', 'error')
    });
  }

  openCreateSlaRuleModal(): void {
    this.editingSlaRule = null;
    this.slaRuleForm = { name: '', description: '', type: 1, slaDays: 5 };
    this.showSlaRuleModal = true;
  }

  openEditSlaRuleModal(rule: SlaRule): void {
    this.editingSlaRule = rule;
    this.slaRuleForm = {
      name: rule.name,
      description: rule.description || '',
      type: rule.type,
      slaDays: rule.slaDays
    };
    this.showSlaRuleModal = true;
  }

  saveSlaRule(): void {
    if (!this.slaRuleForm.name || !this.slaRuleForm.slaDays || this.slaRuleForm.slaDays < 1) {
      this.toastService.show('Name and a valid number of SLA days are required', 'error');
      return;
    }
    this.loading = true;

    if (this.editingSlaRule) {
      this.slaService.updateRule(this.editingSlaRule.id, {
        name: this.slaRuleForm.name,
        description: this.slaRuleForm.description || undefined,
        type: this.slaRuleForm.type,
        slaDays: this.slaRuleForm.slaDays
      }).subscribe({
        next: () => {
          this.toastService.show('SLA rule updated!', 'success');
          this.loading = false;
          this.showSlaRuleModal = false;
          this.loadSlaRules();
        },
        error: () => { this.toastService.show('Error updating SLA rule', 'error'); this.loading = false; }
      });
    } else {
      this.slaService.createRule({
        name: this.slaRuleForm.name,
        description: this.slaRuleForm.description || undefined,
        type: this.slaRuleForm.type,
        slaDays: this.slaRuleForm.slaDays
      }).subscribe({
        next: () => {
          this.toastService.show('SLA rule created!', 'success');
          this.loading = false;
          this.showSlaRuleModal = false;
          this.loadSlaRules();
        },
        error: () => { this.toastService.show('Error creating SLA rule', 'error'); this.loading = false; }
      });
    }
  }

  deleteSlaRule(rule: SlaRule): void {
    this.openConfirm(`Delete SLA rule "${rule.name}"?`, () => {
      this.slaService.deleteRule(rule.id).subscribe({
        next: () => { this.toastService.show('SLA rule deleted!', 'success'); this.loadSlaRules(); },
        error: () => this.toastService.show('Error deleting SLA rule', 'error')
      });
    });
  }

  applySlaRules(): void {
    this.applyingSlaRules = true;
    this.slaService.applyRules().subscribe({
      next: () => {
        this.toastService.show('SLA rules applied to all tasks!', 'success');
        this.applyingSlaRules = false;
      },
      error: () => {
        this.toastService.show('Error applying SLA rules', 'error');
        this.applyingSlaRules = false;
      }
    });
  }

  getSlaTypeLabel(type: number): string {
    return type === 0 ? 'Stream' : 'Task';
  }

  getRoleColor(role: string): string {
    const colors: { [key: string]: string } = {
      'HeadOfCDS': '#6366f1', 'PortfolioDirector': '#8b5cf6',
      'ProjectManager': '#3b82f6', 'BusinessTeamLead': '#10b981',
      'TechnicalTeamLead': '#14b8a6', 'Consultant': '#f59e0b',
      'DAF': '#ef4444', 'SuperAdmin': '#1e293b'
    };
    return colors[role] || '#888';
  }
  openConfirm(message: string, action: () => void): void {
    this.confirmMessage = message;
    this.confirmAction = action;
    this.showConfirmModal = true;
  }

  executeConfirm(): void {
    if (this.confirmAction) this.confirmAction();
    this.showConfirmModal = false;
  }
  loadDashboard(): void {
    this.projectsService.getAll().subscribe({
      next: (projects) => { this.stats.totalProjects = projects.length; }
    });
    this.projectsService.getAllPortfolios().subscribe({
      next: (portfolios) => { this.stats.totalPortfolios = portfolios.length; }
    });
    this.tasksService.getAll().subscribe({
      next: (tasks) => {
        this.stats.tasksPending = tasks.filter(t => t.status === 0).length;
        this.stats.tasksBlocked = tasks.filter(t => t.status === 1).length;
        this.stats.tasksDone = tasks.filter(t => t.status === 2).length;
        this.renderCharts();
      }
    });
  }
  toggleRole(role: string): void {
    const idx = this.toolForm.allowedRoles.indexOf(role);
    if (idx > -1) this.toolForm.allowedRoles.splice(idx, 1);
    else this.toolForm.allowedRoles.push(role);
  }
}