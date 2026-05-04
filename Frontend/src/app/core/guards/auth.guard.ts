import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  const token = authService.getAccessToken();
  const expired = authService.isTokenExpired();

  // Si l'utilisateur est connecté et le token valide
  if (token && !expired) {
    return true;
  }

  // Sinon, rediriger vers la page login
  return router.parseUrl('/auth/login');
};