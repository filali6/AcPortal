import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterOutlet, NavigationEnd } from '@angular/router';
import { AuthService } from './core/services/auth.service';
import { HttpClient } from '@angular/common/http';
import { environment } from '../environments/environment';
import { filter } from 'rxjs/operators';
import { NotificationService } from './core/services/notification.service';
import { TabsBarComponent } from './core/components/tabs-bar/tabs-bar.component';
import { ToastComponent } from './core/components/toast/toast.component';
import { NotificationsDropdownComponent } from './core/components/notifications-dropdown/notifications-dropdown.component';
import { LucideAngularModule, LayoutDashboard, FolderOpen, FileText, Wrench, Bell, MessageSquare, LogOut, User, ChevronRight, Briefcase,Users,GitBranch,Settings } from 'lucide-angular';
import { ChatService } from './core/services/chat.service';
import { KeycloakService } from 'keycloak-angular';
import { DiscussionsPanelComponent } from './core/components/discussions-panel/discussions-panel.component';
@Component({
  selector: 'app-root',
  standalone: true,
imports: [CommonModule, RouterOutlet, TabsBarComponent, ToastComponent, NotificationsDropdownComponent, LucideAngularModule,DiscussionsPanelComponent],
templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit {

  showLayout = false;
  currentRoute = '';
  userInfo: any;
  userRole = '';
  toastMessage = '';
toastVisible = false;
private toastTimeout: any;


  // Outils accessibles au consultant
  myTools: { toolId: string; toolName: string; roles: string[] }[] = [];
  isChefInAnyProject = false;
  notifications: { message: string, projectId: string }[] = [];
  discussionsOpen=false;
  private apiUrl = environment.apiUrl;
  readonly LayoutDashboard = LayoutDashboard;
readonly FolderOpen = FolderOpen;
readonly FileText = FileText;
readonly Wrench = Wrench;
readonly Bell = Bell;
readonly MessageSquare = MessageSquare;
readonly LogOut = LogOut;
readonly User = User;
readonly ChevronRight = ChevronRight;
readonly Briefcase = Briefcase;
readonly Users=Users;
readonly GitBranch=GitBranch;
readonly Settings=Settings;

  constructor(
    private router: Router,
    private authService: AuthService,
    private http: HttpClient,
    private notificationService:NotificationService,
    private keycloak:KeycloakService,
    private chatService:ChatService
    
  ) {}

  ngOnInit(): void {
   
   
  if (this.keycloak.isLoggedIn()) {
    this.userInfo = this.authService.getUserInfo();
    console.log('userInfo:', this.userInfo);
    console.log('userRole:', this.userInfo?.role);
    this.userRole = this.userInfo?.role || '';
    this.showLayout=true;
    
    if (this.userInfo?.id) {
      this.notificationService.startConnection(this.userInfo.id);
      this.initChatConnection();
    }
  } else {
    this.showLayout = false;
    this.userRole = '';
    this.userInfo = null;
  }
    
    this.notificationService.toast$.subscribe(message => {
  this.toastMessage = message;
  this.toastVisible = true;
  clearTimeout(this.toastTimeout);
  this.toastTimeout = setTimeout(() => this.toastVisible = false, 4000);
});
   
 

    this.router.events.pipe(
      filter(e => e instanceof NavigationEnd)
    ).subscribe((e: any) => {
      
      console.log('navigation url ', e.url);
      this.showLayout = !e.url.includes('login') 
    && !e.url.includes('plugins/axe-iam')
    && !e.url.includes('plugins/axe-bpm')
    && !e.url.includes('plugins/axe-gui');
      this.currentRoute = e.url;

      if (this.showLayout) {
        this.userInfo = this.authService.getUserInfo();
        this.userRole = this.userInfo?.role || '';

        // Charge les outils si consultant
        if (this.isConsultant() && this.myTools.length === 0) {
          this.loadMyTools();
        }
        if (this.isConsultant()){
          this.http.get<any>(`${this.apiUrl}/teams/is-chef-equipe`).subscribe({
        next: (res) => this.isChefInAnyProject = res.isChefEquipe
            });
        }
      }
    });

    const currentUrl = this.router.url;
    this.showLayout = !currentUrl.includes('login') 
    && !currentUrl.includes('plugins/axe-iam')
    && !currentUrl.includes('plugins/axe-bpm')
    && !currentUrl.includes('plugins/axe-gui');
    this.currentRoute = currentUrl;
  }

  loadMyTools(): void {
    this.http.get<any[]>(`${this.apiUrl}/tools/my-roles`).subscribe({
      next: (tools) => {
        
        this.myTools = tools.filter(t => t.roles.length > 0);
      }
    });
  }


  getToolRoute(toolName: string): string {
    const routes: { [key: string]: string } = {
      'axeIAM': '/plugins/axe-iam',
      'axeBPM': '/plugins/axe-bpm',
      'axeGUI': '/plugins/axe-gui'
    };
    return routes[toolName] || '/tools';
  }
  private initChatConnection(): void {
  // pas async — on utilise .then()
  const token = this.keycloak.getKeycloakInstance().token || '';
  this.chatService.startConnection(token);
}

  navigate(path: string, queryParams?: any): void {
    this.router.navigate([path], queryParams ? { queryParams } : {});
}

  isActive(path: string): boolean {
    return this.currentRoute.includes(path);
  }

  isAdmin(): boolean {
    return this.userRole === 'HeadOfCDS';
  }

  isDirector(): boolean {
    return this.userRole === 'PortfolioDirector';
  }

  isConsultant(): boolean {
    return this.userRole === 'Consultant';
  }
  isDAF(): boolean {
    return this.userRole === 'DAF';
}

  isProjectManager(): boolean {
  return this.userRole === 'ProjectManager';
}
  isTeamLead(): boolean {
    return this.userRole === 'BusinessTeamLead' || this.userRole === 'TechnicalTeamLead';
}
isSuperAdmin(): boolean {
  return this.userRole === 'SuperAdmin';
}
  closeToast(): void {
  this.toastVisible = false;
  clearTimeout(this.toastTimeout);
}

  logout(): void {
    this.notificationService.stopConnection();
    this.authService.logout();
    this.userRole = '';
    this.userInfo = null;
    this.showLayout = false;
  }
  getUserInitials(): string {
  const name = this.userInfo?.name || '';
  return name.split(' ').map((w: string) => w[0]).join('').toUpperCase().slice(0, 2) || 'U';
}

getRoleLabel(): string {
  const labels: { [key: string]: string } = {
    'HeadOfCDS': 'Head of CDS',
    'PortfolioDirector': 'Portfolio Director',
    'ProjectManager': 'Project Manager',
    'BusinessTeamLead': 'Business Team Lead',
    'TechnicalTeamLead': 'Technical Team Lead',
    'Consultant': 'Consultant',
    'DAF': 'DAF',
    'SuperAdmin': 'Super Admin'
  };
  return labels[this.userRole] || this.userRole;
}
toggleDiscussions():void{
  this.discussionsOpen=!this.discussionsOpen;
}
}