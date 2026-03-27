import { ApplicationConfig } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    // Router → gère la navigation entre les pages
    provideRouter(routes),

    // HttpClient → permet les appels HTTP vers le backend
    // withInterceptors → ajoute notre interceptor JWT automatiquement
    provideHttpClient(withInterceptors([authInterceptor])),

    // Animations → effets visuels Angular Material
    provideAnimations()
  ]
};