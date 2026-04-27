import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { KeycloakService } from 'keycloak-angular';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent implements OnInit {

  constructor(
    private authService: AuthService,
    private keycloak: KeycloakService,
    private router: Router
  ) {}

  ngOnInit(): void {
    
    // Si déjà connecté → redirige directement
    if (this.authService.isLoggedIn()) {
      this.redirectByRole();
    } else {
      // Sinon → redirige vers la page login Keycloak
      this.keycloak.login({
        redirectUri: window.location.origin + '/login'
      });
    }
  }

  private redirectByRole(): void {
    const userInfo = this.authService.getUserInfo();
    const role = userInfo?.role;

    if (role === 'HeadOfCDS') {
      this.router.navigate(['/admin']);
    } else if (role === 'PortfolioDirector') {
      this.router.navigate(['/director']);
    } else if (role === 'ProjectManager') {
      this.router.navigate(['/project-manager']);
    } else if (role === 'BusinessTeamLead' || role === 'TechnicalTeamLead') {
      this.router.navigate(['/team-lead']);
    } else if (role === 'Consultant') {
      this.router.navigate(['/consultant']);
    } else if (role === 'DAF') {
      this.router.navigate(['/daf']);
    } else {
      this.keycloak.login();
    }
  }
}