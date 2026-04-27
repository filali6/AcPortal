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

  // HeadOfCDS
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
    path: 'tools-page',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./pages/tools-page/tools-page.component')
        .then(m => m.ToolsPageComponent)
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
  {
    path: 'project-manager',
    canActivate: [authGuard],
    loadComponent: () =>
        import('./pages/project-manager/project-manager.component')
            .then(m => m.ProjectManagerComponent)
},
{
    path: 'team-lead',
    canActivate: [authGuard],
    loadComponent: () =>
        import('./pages/team-lead/team-lead.component')
            .then(m => m.TeamLeadComponent)
},

  // Route inconnue
  {
    path: '**',
    redirectTo: 'login'
  }
];