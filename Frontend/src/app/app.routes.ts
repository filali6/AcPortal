import { Routes } from '@angular/router';

export const routes: Routes = [
  
  {
    path: '',
    redirectTo: 'login',
    pathMatch: 'full'
  },

  // Page login
  {
    path: 'login',
    loadComponent: () =>
      import('./pages/login/login.component')
        .then(m => m.LoginComponent)
  },

  // Dashboard
  {
    path: 'tools',
    loadComponent: () =>
      import('./pages/dashboard/dashboard.component')
        .then(m => m.DashboardComponent)
  },

  
  {
    path: 'plugins/axe-iam',
    loadComponent: () =>
      import('./pages/plugins/axe-iam/axe-iam.component')
        .then(m => m.AxeIamComponent)
  },

  // Plugin axeBPM
  {
    path: 'plugins/axe-bpm',
    loadComponent: () =>
      import('./pages/plugins/axe-bpm/axe-bpm.component')
        .then(m => m.AxeBpmComponent)
  },

  // Plugin axeGUI
  {
    path: 'plugins/axe-gui',
    loadComponent: () =>
      import('./pages/plugins/axe-gui/axe-gui.component')
        .then(m => m.AxeGuiComponent)
  },

   
  {
    path: '**',
    redirectTo: 'tools'
  }
];