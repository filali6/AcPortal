import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { KeycloakService } from 'keycloak-angular';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { ToastService } from '../services/toast.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const keycloak = inject(KeycloakService);
  const toastService = inject(ToastService);


  return from(keycloak.updateToken(30)).pipe(
    switchMap(() => {
      const token = keycloak.getKeycloakInstance().token;

      const authReq = token ? req.clone({
        setHeaders: { Authorization: `Bearer ${token}` }
      }) : req;

      return next(authReq);
    }),
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401) {
        toastService.show('Session expired — please login again', 'error');
        keycloak.logout();
      } else if (error.status === 403) {
        toastService.show('Access denied', 'error');
      } else if (error.status === 500) {
        toastService.show('Server error — please try again', 'error');
      }
      return throwError(() => error);
    })
  );
};