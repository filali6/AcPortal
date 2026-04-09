import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { KeycloakService } from 'keycloak-angular';
import { NotificationService } from './notification.service';

@Injectable({
  providedIn: 'root'
})
export class AuthService {

  constructor(
    private keycloak: KeycloakService,
    private router: Router,
    private notificationService: NotificationService
  ) {}

  
  getToken(): string | null {
  const instance = this.keycloak.getKeycloakInstance();
  
  // Force refresh si token expiré
  if (instance.isTokenExpired(30)) {
    this.keycloak.updateToken(30);
  }
  
  return instance.token ?? null;
}

   
  isLoggedIn(): boolean {
    return this.keycloak.isLoggedIn();
  }

   
  logout(): void {
    this.keycloak.logout('http://localhost:4200/login');
  }

   
  getUserInfo(): any {
    const instance = this.keycloak.getKeycloakInstance();
    const token = instance.token;
    if (!token) return null;

    const payload = token.split('.')[1];
    const decoded = JSON.parse(atob(payload));
    console.log('Token décodé :', decoded);

    return {
      id: decoded.sub,                          // l'ID Keycloak
      name: decoded.name ?? decoded.preferred_username,
      email: decoded.email,
      role: decoded.realm_access?.roles?.find((r: string) =>
        ['HeadOfCDS', 'PortfolioDirector', 'ProjectManager',
         'BusinessTeamLead', 'TechnicalTeamLead', 'Consultant', 'DAF']
        .includes(r)
      )
    };
  }

   
  startNotifications(): void {
    const userInfo = this.getUserInfo();
    if (userInfo?.id) {
      this.notificationService.startConnection(userInfo.id);
    }
  }
}