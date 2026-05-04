import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { map } from 'rxjs/operators';

export const adminGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  return authService.isAuthenticated$.pipe(
    map(isAuthenticated => {
      if (!isAuthenticated || authService.isTokenExpired()) {
        router.navigate(['/login']);
        return false;
      }
      // TODO: Add role check when user info is available
      // For now, just check if authenticated
      return true;
    })
  );
};
