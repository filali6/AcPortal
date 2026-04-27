import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent {

  email = '';
  password = '';
  errorMessage = '';
  loading = false;

  constructor(
    private authService: AuthService,
    private router: Router
  ) {}

  login() {
    this.loading = true;
    this.errorMessage = '';

    this.authService.login(this.email, this.password).subscribe({
      next: () => {
        // Récupère les infos du token JWT
        const userInfo = this.authService.getUserInfo();

        // Redirige selon le rôle
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
              this.router.navigate(['/login']);
          }
      },
      error: () => {
        this.errorMessage = 'Email ou mot de passe incorrect';
        this.loading = false;
      }
    });
  }
}