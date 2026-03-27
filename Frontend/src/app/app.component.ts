import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterOutlet, NavigationEnd } from '@angular/router';
import { AuthService } from './core/services/auth.service';
import { HttpClient } from '@angular/common/http';
import { environment } from '../environments/environment';
import { filter } from 'rxjs/operators';
import { NotificationService } from './core/services/notification.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet],
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

  private apiUrl = environment.apiUrl;

  constructor(
    private router: Router,
    private authService: AuthService,
    private http: HttpClient,
    private notificationService:NotificationService,
    
  ) {}

  ngOnInit(): void {
    const token = localStorage.getItem('token');
    if (!token) {
        this.showLayout = false;
        this.userRole = '';
        this.userInfo = null;
    } else {
        this.userInfo = this.authService.getUserInfo();
        this.userRole = this.userInfo?.role || '';
        if (this.userInfo?.id){
          this.notificationService.startConnection(this.userInfo.id);
    }
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
      this.showLayout = !e.url.includes('login') && !e.url.includes('plugins');
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
    this.showLayout = !currentUrl.includes('login') && !currentUrl.includes('plugins');
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

  navigate(path: string, queryParams?: any): void {
    this.router.navigate([path], queryParams ? { queryParams } : {});
}

  isActive(path: string): boolean {
    return this.currentRoute.includes(path);
  }

  isAdmin(): boolean {
    return this.userRole === 'SuperAdmin';
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

  isChefEquipe(): boolean {
      return this.isChefInAnyProject;
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
}