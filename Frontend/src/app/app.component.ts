import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterOutlet, NavigationEnd } from '@angular/router';
import { AuthService } from './core/services/auth.service';
import { filter } from 'rxjs/operators';

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

  constructor(
    private router: Router,
    private authService: AuthService
  ) {}

  ngOnInit(): void {
    this.router.events.pipe(
      filter(e => e instanceof NavigationEnd)
    ).subscribe((e: any) => {
      // Cache sidebar/header sur la page login
      //this.showLayout = !e.url.includes('login');
      this.showLayout = !e.url.includes('login') && !e.url.includes('plugins');
      this.currentRoute = e.url;
      if (this.showLayout) {
        this.userInfo = this.authService.getUserInfo();
      }
    });
  }

  navigate(path: string): void {
    this.router.navigate([path]);
  }

  isActive(path: string): boolean {
    return this.currentRoute.includes(path);
  }

  logout(): void {
    this.authService.logout();
  }
}