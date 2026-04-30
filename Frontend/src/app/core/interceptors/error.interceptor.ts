import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { environment } from '../../../environments/environment';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const router      = inject(Router);
  const authService = inject(AuthService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      let errorMessage = 'An error occurred';

      const isAuthRoute = req.url.includes('/auth/login') ||
                          req.url.includes('/auth/register') ||
                          req.url.includes('/auth/refresh');

      if (error.error instanceof ErrorEvent) {
        errorMessage = `Error: ${error.error.message}`;
      } else {
        switch (error.status) {
          case 401:
            if (isAuthRoute) {
              errorMessage = error.error?.fail_Messages || 'Email ou mot de passe invalide.';
            } else {
              // Fix : appeler handleUnauthorized() pour invalider le BehaviorSubject
              // et notifier tous les composants abonnés à currentUser$
              authService.handleUnauthorized();
              router.navigate(['/auth/login']);
              errorMessage = 'Session expirée. Veuillez vous reconnecter.';
            }
            break;
          case 403:
            errorMessage = error.error?.fail_Messages || 'Access denied.';
            break;
          case 404:
            errorMessage = 'Resource not found.';
            break;
          case 500:
            errorMessage = 'Server error. Please try again later.';
            break;
          default:
            if (error.error?.fail_Messages) {
              errorMessage = error.error.fail_Messages;
            } else {
              errorMessage = `Error: ${error.message}`;
            }
        }
      }

      // Fix : console.error uniquement hors production
      if (!environment.production) {
        console.error('HTTP Error:', errorMessage, error);
      }

      return throwError(() => error);
    })
  );
};