import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { BehaviorSubject, catchError, filter, switchMap, take, throwError } from 'rxjs';
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

      // --- 401: attempt token refresh before giving up ---
      if (error.status === 401 && !isAuthRoute) {
        if (!authService.isRefreshing) {
          authService.isRefreshing = true;
          authService.refreshTokenSubject.next(null); // block concurrent requests

          return authService.refreshTokens().pipe(
            switchMap(res => {
              authService.isRefreshing = false;
              const newToken = res.resultat!.token;
              authService.refreshTokenSubject.next(newToken);
              // Retry the original request with the new token
              return next(req.clone({ setHeaders: { Authorization: `Bearer ${newToken}` } }));
            }),
            catchError(refreshError => {
              authService.isRefreshing = false;
              authService.refreshTokenSubject.next(null);
              authService.handleUnauthorized();
              router.navigate(['/auth/login']);
              return throwError(() => refreshError);
            })
          );
        } else {
          // Another request is already refreshing — wait for the new token
          return authService.refreshTokenSubject.pipe(
            filter(token => token !== null),
            take(1),
            switchMap(token =>
              next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }))
            )
          );
        }
      }
      // --------------------------------------------------

      if (error.error instanceof ErrorEvent) {
        errorMessage = `Error: ${error.error.message}`;
      } else {
        switch (error.status) {
          case 401:
            // isAuthRoute 401 (bad credentials)
            errorMessage = error.error?.fail_Messages || 'Email ou mot de passe invalide.';
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