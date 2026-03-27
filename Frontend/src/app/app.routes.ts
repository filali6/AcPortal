import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  // Page d'accueil → login
  {
    path: '',
    redirectTo: 'login',
    pathMatch: 'full'
  },

  // Login → tout le monde
  {
    path: 'login',
    loadComponent: () =>
      import('./pages/login/login.component')
        .then(m => m.LoginComponent)
  },

  // SuperAdmin
  {
    path: 'admin',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./pages/admin/admin.component')
        .then(m => m.AdminComponent)
  },

  // PortfolioDirector
  {
    path: 'director',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./pages/director/director.component')
        .then(m => m.DirectorComponent)
  },

  // Consultant → tools + tâches
  {
    path: 'tools',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./pages/dashboard/dashboard.component')
        .then(m => m.DashboardComponent)
  },

  // Consultant → ses tâches
  {
    path: 'consultant',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./pages/consultant/consultant.component')
        .then(m => m.ConsultantComponent)
  },

  // Plugins → sans sidebar
  {
    path: 'plugins/axe-iam',
    loadComponent: () =>
      import('./pages/plugins/axe-iam/axe-iam.component')
        .then(m => m.AxeIamComponent)
  },
  {
    path: 'plugins/axe-bpm',
    loadComponent: () =>
      import('./pages/plugins/axe-bpm/axe-bpm.component')
        .then(m => m.AxeBpmComponent)
  },
  {
    path: 'plugins/axe-gui',
    loadComponent: () =>
      import('./pages/plugins/axe-gui/axe-gui.component')
        .then(m => m.AxeGuiComponent)
  },
  {
    path : 'daf',
    canActivate:[authGuard],
    loadComponent:()=>import('./pages/daf/daf.component').then(m=>m.DafComponent)
  },
  {
    path:'chef-equipe',
    canActivate:[authGuard],
    loadComponent:()=>import('./pages/chef-equipe/chef-equipe.component').then(m=>m.ChefEquipeComponent)

  },

  // Route inconnue
  {
    path: '**',
    redirectTo: 'login'
  }
];