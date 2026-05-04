import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const auditLogsGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.canViewAuditLogs()) {
    return true;
  }

  return router.parseUrl('/dashboard');
};
