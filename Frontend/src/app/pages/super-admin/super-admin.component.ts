import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { UsersService } from '../../core/services/users.service';
import { PluginsAdminService, PluginDto } from '../../core/services/plugins-admin.service';
import { WorkflowService } from '../../core/services/Workflow.service';
import { ToastService } from '../../core/services/toast.service';
import { UtilsService } from '../../core/services/utils.service';
import { TabsService } from '../../core/services/tabs.service';
import { ModalComponent } from '../../core/components/modal/modal.component';
import { LucideAngularModule, Users, Wrench, GitBranch, LayoutDashboard, Plus, Trash2, Edit, ChevronRight } from 'lucide-angular';
import { Subscription } from 'rxjs';
import { ProjectsService } from '../../core/services/projects.service';
import { TasksService } from '../../core/services/tasks.service';
@Component({
  selector: 'app-super-admin',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideAngularModule, ModalComponent],
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
  userForm = { fullName: '', email: '', password: '', role: 'Consultant' };
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
    category: '', accessUrl: '', icon: '', ssoEnabled: false,isActive: true, allowedRoles: []
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
  usersByRole: [] as { role: string; count: number }[]
};
  showConfirmModal = false;
confirmMessage = '';
confirmAction: (() => void) | null = null;

  loading = false;

  readonly Users = Users;
  readonly Wrench = Wrench;
  readonly GitBranch = GitBranch;
  readonly LayoutDashboard = LayoutDashboard;
  readonly Plus = Plus;
  readonly Trash2 = Trash2;
  readonly Edit = Edit;
  readonly ChevronRight = ChevronRight;

  constructor(
    private usersService: UsersService,
    private pluginsAdminService: PluginsAdminService,
    private workflowService: WorkflowService,
    private toastService: ToastService,
    public utils: UtilsService,
    public tabsService: TabsService,
    private projectsService: ProjectsService,
    private tasksService: TasksService,
  ) {}

  ngOnInit(): void {
    this.loadAll();
    this.subs.push(
      this.tabsService.activeTabId.subscribe(id => this.activeTabId = id)
    );
     
  }

  ngOnDestroy(): void {
    this.subs.forEach(s => s.unsubscribe());
  }

 loadAll(): void {
  this.loadDashboard();
  this.loadUsers();
  this.loadTools();
  this.loadWorkflow();
}

  // ===== NAVIGATION =====
  openTab(type: 'users' | 'tools' | 'workflow'): void {
    const titles = { users: 'Users', tools: 'Tools', workflow: 'Workflow' };
    this.tabsService.openTab({
      id: type,
      title: titles[type],
      type: 'create-project'
    });
  }

  // ===== STATS =====
  computeStats(): void {
    this.stats.totalUsers = this.users.length;
    this.stats.totalTools = this.tools.length;
    this.stats.totalRules = this.workflowRules.length;
    const roleCount: { [key: string]: number } = {};
    this.users.forEach(u => {
      roleCount[u.role] = (roleCount[u.role] || 0) + 1;
    });
    this.stats.usersByRole = Object.keys(roleCount).map(role => ({
      role, count: roleCount[role]
    }));
  }

  // ===== USERS =====
  loadUsers(): void {
    this.usersService.getAllForAdmin().subscribe({
      next: (users) => { this.users = users; this.computeStats(); }
    });
  }

  openCreateUserModal(): void {
    this.editingUser = null;
    this.userForm = { fullName: '', email: '', password: '', role: 'Consultant' };
    this.showUserModal = true;
  }

  openEditUserModal(user: any): void {
    this.editingUser = user;
    this.userForm = { fullName: user.fullName, email: user.email, password: '', role: user.role };
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
        role: this.userForm.role
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
        role: this.userForm.role
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

  saveTool(): void {
  if (!this.toolForm.name) {
    this.toastService.show('Name is required', 'error');
    return;
  }
  this.loading = true;
  this.pluginsAdminService.update(this.editingTool!.id, this.toolForm).subscribe({
    next: () => {
      this.toastService.show('Tool updated!', 'success');
      this.loading = false;
      this.showToolModal = false;
      this.loadTools();
    },
    error: () => { this.toastService.show('Error updating tool', 'error'); this.loading = false; }
  });
}

 

  // ===== WORKFLOW =====
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
    this.ruleForm = { ...rule, targetValues: [...rule.targetValues] };
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
      this.stats.tasksDone   = tasks.filter(t => t.status === 2).length;
    }
  });
}
toggleRole(role: string): void {
  const idx = this.toolForm.allowedRoles.indexOf(role);
  if (idx > -1) this.toolForm.allowedRoles.splice(idx, 1);
  else this.toolForm.allowedRoles.push(role);
}
}