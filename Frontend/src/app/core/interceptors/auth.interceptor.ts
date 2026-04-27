import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';

// HttpInterceptorFn → fonction qui intercepte les requêtes
// c'est le nouveau style Angular (standalone)
export const authInterceptor: HttpInterceptorFn = (req, next) => {

  // inject() → récupère le service
  // comme l'injection dans le constructeur
  const authService = inject(AuthService);

  // Récupère le token stocké
  const token = authService.getToken();

  // Si token existe → ajoute le header Authorization
  if (token) {
    // clone la requête avec le nouveau header
    // on clone car les requêtes HTTP sont immutables
    const authReq = req.clone({
      setHeaders: {
        Authorization: `Bearer ${token}`
      }
    });
    // continue avec la requête modifiée
    return next(authReq);
  }

  // Pas de token → continue sans modification
  return next(req);
};