import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import { Router } from '@angular/router';
import { environment } from '../../../environments/environment';
import {
  LoginRequest,
  LoginResult,
  ChangePasswordResponse,
  ForgotPasswordResponse,
  ResetPasswordResponse
} from '../models/auth.model';
import { ResponseHttp } from '../models/response-http.model';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private http   = inject(HttpClient);
  private router = inject(Router);

  private isAuthenticatedSubject = new BehaviorSubject<boolean>(this.hasToken());
  public isAuthenticated$ = this.isAuthenticatedSubject.asObservable();

  private currentUserSubject = new BehaviorSubject<LoginResult['user'] | null>(
    this.getUserData() || this.getUserFromToken()
  );
  public currentUser$ = this.currentUserSubject.asObservable();

  login(credentials: LoginRequest): Observable<ResponseHttp<LoginResult>> {
    return this.http.post<ResponseHttp<LoginResult>>(
      `${environment.apiUrl}/auth/login`,
      credentials
    ).pipe(
      tap(response => {
        if (response.status === 200 && response.resultat) {
          this.setToken(response.resultat.token);
          const tokenUser = this.getUserFromToken();
          const userFromApi = response.resultat.user;
          const roles = userFromApi?.roles?.length ? userFromApi.roles : (tokenUser?.roles ?? []);
          const normalizedUser = {
            ...userFromApi,
            roles
          };
          this.setUserData(normalizedUser);
          this.currentUserSubject.next(normalizedUser);
          this.isAuthenticatedSubject.next(true);
        }
      })
    );
  }

  // ✅ any → ChangePasswordResponse
  changePassword(
    email: string,
    currentPassword: string,
    newPassword: string
  ): Observable<ResponseHttp<ChangePasswordResponse>> {
    return this.http.post<ResponseHttp<ChangePasswordResponse>>(
      `${environment.apiUrl}/auth/change-password`,
      { email, currentPassword, newPassword }
    );
  }

  // ✅ any → ForgotPasswordResponse
  forgotPassword(email: string): Observable<ResponseHttp<ForgotPasswordResponse>> {
    return this.http.post<ResponseHttp<ForgotPasswordResponse>>(
      `${environment.apiUrl}/auth/forgot-password`,
      { email }
    );
  }

  // ✅ any → ResetPasswordResponse
  resetPassword(
    token: string,
    newPassword: string,
    confirmPassword: string
  ): Observable<ResponseHttp<ResetPasswordResponse>> {
    return this.http.post<ResponseHttp<ResetPasswordResponse>>(
      `${environment.apiUrl}/auth/reset-password`,
      { token, newPassword, confirmPassword }
    );
  }

  logout(): void {
    this.clearTokens();
    localStorage.removeItem('userId');
    this.currentUserSubject.next(null);
    this.isAuthenticatedSubject.next(false);
    this.router.navigate(['/auth/login']);
  }

  handleUnauthorized(): void {
    this.clearTokens();
    localStorage.removeItem('userId');
    this.currentUserSubject.next(null);
    this.isAuthenticatedSubject.next(false);
  }

  getAccessToken(): string | null {
    return localStorage.getItem('access_token');
  }

  isTokenExpired(): boolean {
    const token = this.getAccessToken();
    if (!token) return true;
    try {
      const payload = this.decodeTokenPayload(token);
      return payload ? Date.now() > payload['exp'] * 1000 : true;
    } catch {
      return true;
    }
  }

  decodeTokenPayload(token: string): Record<string, any> | null {
    try {
      return JSON.parse(atob(token.split('.')[1]));
    } catch {
      return null;
    }
  }

  private setToken(token: string): void {
    localStorage.setItem('access_token', token);
  }

  private setUserData(user: LoginResult['user']): void {
    localStorage.setItem('user_data', JSON.stringify(user));
  }

  private getUserData(): LoginResult['user'] | null {
    const userData = localStorage.getItem('user_data');
    if (userData) {
      try {
        return JSON.parse(userData);
      } catch {
        return null;
      }
    }
    return null;
  }

  private clearTokens(): void {
    localStorage.removeItem('access_token');
    localStorage.removeItem('user_data');
  }

  private hasToken(): boolean {
    return !!this.getAccessToken() && !this.isTokenExpired();
  }

  private getUserFromToken(): LoginResult['user'] | null {
    const token = this.getAccessToken();
    if (!token || this.isTokenExpired()) return null;

    const p = this.decodeTokenPayload(token);
    if (!p) return null;

    const id =
      p['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier']
      ?? p['sub'] ?? p['nameid'] ?? '';

    const email =
      p['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name']
      ?? p['email'] ?? p['unique_name'] ?? '';

    const firstName = p['given_name']  ?? p['firstName']  ?? '';
    const lastName  = p['family_name'] ?? p['lastName']   ?? '';

    const rawRoles =
      p['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']
      ?? p['role'] ?? [];
    const roles: string[] = Array.isArray(rawRoles) ? rawRoles : [rawRoles];

    return {
      id, email, firstName, lastName,
      fullName: `${firstName} ${lastName}`.trim() || email,
      isActive: true, roles,
      createdDate: '', modifiedDate: null
    };
  }

  getCurrentUserId(): string | null {
    const user = this.currentUserSubject.getValue();
    return user?.id || null;
  }

  getCurrentUser(): LoginResult['user'] | null {
    return this.currentUserSubject.getValue();
  }

  getCurrentRole(): string {
    const role = this.getCurrentUser()?.roles?.[0] || 'Viewer';
    return String(role).toLowerCase();
  }

  hasRole(role: string): boolean {
    const targetRole = role.toLowerCase();
    return !!this.getCurrentUser()?.roles?.some(userRole => userRole.toLowerCase() === targetRole);
  }

  hasAnyRole(roles: string[]): boolean {
    if (!roles?.length) return false;
    return roles.some(role => this.hasRole(role));
  }

  canManageUsers(): boolean {
    return this.hasRole('Owner');
  }

  canCreateScenarios(): boolean {
    return this.hasAnyRole(['Owner', 'Tester']);
  }

  canRunTests(): boolean {
    return this.hasAnyRole(['Owner', 'Tester']);
  }

  isProjectReadOnly(): boolean {
    return this.hasRole('Viewer');
  }

  canManageNlpMappings(): boolean {
    const user = this.getCurrentUser();
    if (!user) return false;
    return !this.hasRole('Viewer');
  }

  canParseGherkinText(): boolean {
    return this.hasAnyRole(['Owner', 'Manager', 'Tester', 'Viewer']);
  }

  canViewAuditLogs(): boolean {
    return this.hasAnyRole(['Owner', 'Manager', 'Tester']);
  }

  extractApiErrorMessage(
    error: unknown,
    fallbackMessage: string,
    statusMessages: Record<number, string> = {}
  ): string {
    const httpError = error as HttpErrorResponse;
    const status = httpError?.status;
    const payload = httpError?.error as Record<string, unknown> | string | null | undefined;

    const payloadMessage =
      this.normalizeErrorMessage((payload as Record<string, unknown>)?.['fail_Messages']) ||
      this.normalizeErrorMessage((payload as Record<string, unknown>)?.['Fail_Messages']) ||
      this.normalizeErrorMessage((payload as Record<string, unknown>)?.['message']) ||
      (typeof payload === 'string' ? payload : '');

    if (payloadMessage && !payloadMessage.toLowerCase().includes('http failure response for')) {
      return payloadMessage;
    }

    if (status && statusMessages[status]) {
      return statusMessages[status];
    }

    if (status === 0) {
      return 'Network error. Please check your connection and try again.';
    }

    if (status === 500) {
      return 'Server error. Please try again in a moment.';
    }

    return fallbackMessage;
  }

  private normalizeErrorMessage(message: unknown): string {
    if (!message) return '';
    if (Array.isArray(message)) {
      return message.map(item => String(item)).join(' | ');
    }
    return String(message);
  }

  setSessionFromOAuth(token: string, user: LoginResult['user']): void {
  this.setToken(token);
  this.setUserData(user);
  this.currentUserSubject.next(user);
  this.isAuthenticatedSubject.next(true);
}
}