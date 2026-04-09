import { ApplicationConfig ,APP_INITIALIZER} from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { KeycloakService } from 'keycloak-angular';

 
function initializeKeycloak(keycloak: KeycloakService) {
  return () =>
    keycloak.init({
      config: {
        url: 'http://localhost:8181',
        realm: 'acp-portal',
        clientId: 'acp-frontend'
      },
      initOptions: {
  onLoad: 'check-sso',
  checkLoginIframe: false,  // ← ajoute juste cette ligne
  silentCheckSsoRedirectUri:
    window.location.origin + '/assets/silent-check-sso.html'
},
      enableBearerInterceptor: false  // on garde notre interceptor existant
    });
}

export const appConfig: ApplicationConfig = {
  providers: [
    // Router → gère la navigation entre les pages
    provideRouter(routes),

    // HttpClient → permet les appels HTTP vers le backend
    // withInterceptors → ajoute notre interceptor JWT automatiquement
    provideHttpClient(withInterceptors([authInterceptor])),

    // Animations → effets visuels Angular Material
    provideAnimations(),

    KeycloakService,
    {
      provide: APP_INITIALIZER,
      useFactory: initializeKeycloak,
      multi: true,
      deps: [KeycloakService]
    }
  ]
};