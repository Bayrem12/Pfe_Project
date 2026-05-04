import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const scenarioCreateGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.canCreateScenarios()) {
    return true;
  }

  return router.parseUrl('/scenarios');
};

export const scenarioEditGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.isProjectReadOnly()) {
    return true;
  }

  return router.parseUrl('/scenarios');
};
