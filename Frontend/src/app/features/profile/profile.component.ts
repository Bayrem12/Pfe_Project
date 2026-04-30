import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  FormBuilder,
  FormGroup,
  Validators,
  AbstractControl,
  ValidationErrors,
  ReactiveFormsModule
} from '@angular/forms';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';

import { AuthService } from '../../core/services/auth.service';
import { ResponseHttp } from '../../core/models/response-http.model';
import { ChangePasswordResponse } from '../../core/models/auth.model';

export interface UserProfile {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  isActive: boolean;
  fullName: string;
  roles: string[];
  createdDate: string | null;
  modifiedDate: string | null;
}

export interface ActivityItem {
  title: string;
  description: string;
  timeAgo: string;
  color: string;
}

function newPasswordDifferentValidator(group: AbstractControl): ValidationErrors | null {
  const current = group.get('currentPassword')?.value;
  const next    = group.get('newPassword')?.value;
  return current && next && current === next ? { sameAsCurrent: true } : null;
}

function confirmMatchValidator(group: AbstractControl): ValidationErrors | null {
  const np = group.get('newPassword')?.value;
  const cp = group.get('confirmPassword')?.value;
  return np && cp && np !== cp ? { mismatch: true } : null;
}

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './profile.component.html'
})
export class ProfileComponent implements OnInit, OnDestroy {

  private authService = inject(AuthService);
  private fb          = inject(FormBuilder);
  private destroy$    = new Subject<void>();

  user: UserProfile | null = null;
  loadingUser  = true;
  userError: string | null = null;

  changePasswordForm!: FormGroup;
  submitting      = false;
  successMessage: string | null = null;
  errorMessage:   string | null = null;

  showCurrent      = false;
  showNew          = false;
  showConfirm      = false;
  showPasswordForm = false;

  activities: ActivityItem[] = [];
  loadingActivities = false;

  autoHealingEnabled = true;
  emailNotificationsEnabled = true;
  preferredTheme: 'light' | 'dark' = 'light';
  twoFactorEnabled = true;

  private readonly prefKey = 'profile_preferences';

  ngOnInit(): void {
    this.buildForm();
    this.loadPreferences();
    this.loadCurrentUser();
    this.loadActivities();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private buildForm(): void {
    this.changePasswordForm = this.fb.group(
      {
        currentPassword: ['', [Validators.required, Validators.minLength(6)]],
        newPassword: [
          '',
          [
            Validators.required,
            Validators.minLength(8),
            Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)/)
          ]
        ],
        confirmPassword: ['', Validators.required]
      },
      { validators: [newPasswordDifferentValidator, confirmMatchValidator] }
    );
  }

  private loadCurrentUser(): void {
    this.authService.currentUser$
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (tokenUser) => {
          if (!tokenUser) {
            this.userError   = 'Vous n\'êtes pas connecté.';
            this.loadingUser = false;
            return;
          }
          this.user = {
            id:           tokenUser.id,
            email:        tokenUser.email,
            firstName:    tokenUser.firstName  || '',
            lastName:     tokenUser.lastName   || '',
            fullName:     tokenUser.fullName   || tokenUser.email,
            isActive:     tokenUser.isActive   ?? true,
            roles:        tokenUser.roles      ?? [],
            createdDate:  tokenUser.createdDate   || null,
            modifiedDate: tokenUser.modifiedDate  || null,
          };
          this.loadingUser = false;
        },
        error: () => {
          this.userError   = 'Impossible de charger le profil.';
          this.loadingUser = false;
        }
      });
  }

  private loadActivities(): void {
    this.loadingActivities = true;
    // TODO: remplacer par votre vrai appel API, ex:
    // this.activityService.getRecentActivities()
    //   .pipe(takeUntil(this.destroy$))
    //   .subscribe({
    //     next: (data) => { this.activities = data; this.loadingActivities = false; },
    //     error: ()   => { this.loadingActivities = false; }
    //   });
    this.activities        = [];
    this.loadingActivities = false;
  }

  private loadPreferences(): void {
    const raw = localStorage.getItem(this.prefKey);
    if (!raw) return;

    try {
      const prefs = JSON.parse(raw) as {
        autoHealingEnabled?: boolean;
        emailNotificationsEnabled?: boolean;
        preferredTheme?: 'light' | 'dark';
        twoFactorEnabled?: boolean;
      };

      this.autoHealingEnabled = prefs.autoHealingEnabled ?? true;
      this.emailNotificationsEnabled = prefs.emailNotificationsEnabled ?? true;
      this.preferredTheme = prefs.preferredTheme ?? 'light';
      this.twoFactorEnabled = prefs.twoFactorEnabled ?? true;
      this.applyTheme(this.preferredTheme);
    } catch {
      // Ignore malformed persisted preferences.
    }
  }

  private persistPreferences(): void {
    localStorage.setItem(this.prefKey, JSON.stringify({
      autoHealingEnabled: this.autoHealingEnabled,
      emailNotificationsEnabled: this.emailNotificationsEnabled,
      preferredTheme: this.preferredTheme,
      twoFactorEnabled: this.twoFactorEnabled,
    }));
  }

  toggleAutoHealing(): void {
    this.autoHealingEnabled = !this.autoHealingEnabled;
    this.persistPreferences();
  }

  toggleEmailNotifications(): void {
    this.emailNotificationsEnabled = !this.emailNotificationsEnabled;
    this.persistPreferences();
  }

  setTheme(theme: 'light' | 'dark'): void {
    this.preferredTheme = theme;
    this.applyTheme(theme);
    this.persistPreferences();
  }

  toggleTwoFactor(): void {
    this.twoFactorEnabled = !this.twoFactorEnabled;
    this.persistPreferences();
  }

  private applyTheme(theme: 'light' | 'dark'): void {
    const root = document.documentElement;
    if (theme === 'dark') {
      root.classList.add('dark');
    } else {
      root.classList.remove('dark');
    }
  }

  getPrimaryRole(): string {
    return this.user?.roles?.[0] || 'Viewer';
  }

  getRoleClass(): string {
    const role = this.getPrimaryRole().toLowerCase();
    if (role === 'owner') return 'border border-blue-200 bg-blue-50 text-blue-700';
    if (role === 'manager') return 'border border-violet-200 bg-violet-50 text-violet-700';
    if (role === 'tester') return 'border border-emerald-200 bg-emerald-50 text-emerald-700';
    return 'border border-slate-200 bg-slate-100 text-slate-700';
  }

  onSubmit(): void {
    if (this.changePasswordForm.invalid || !this.user) return;

    this.submitting     = true;
    this.successMessage = null;
    this.errorMessage   = null;

    const { currentPassword, newPassword } = this.changePasswordForm.value;

    this.authService.changePassword(this.user.email, currentPassword, newPassword)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res: ResponseHttp<ChangePasswordResponse>) => {
          this.submitting = false;
          if (res.status === 200) {
            this.successMessage  = 'Mot de passe modifié avec succès.';
            this.showPasswordForm = false;
            this.changePasswordForm.reset();
            if (res.resultat) {
              this.user = {
                ...this.user!,
                ...res.resultat,
                fullName: `${res.resultat.firstName} ${res.resultat.lastName}`.trim()
              };
            }
          } else {
            this.errorMessage = res.fail_Messages ?? 'Une erreur est survenue.';
          }
        },
        error: (err: Error) => {
          this.submitting   = false;
          this.errorMessage = err.message ?? 'Erreur serveur, veuillez réessayer.';
        }
      });
  }

  get f() { return this.changePasswordForm.controls; }

  toggleVisibility(field: 'current' | 'new' | 'confirm'): void {
    if (field === 'current') this.showCurrent = !this.showCurrent;
    if (field === 'new')     this.showNew     = !this.showNew;
    if (field === 'confirm') this.showConfirm = !this.showConfirm;
  }

  getInitials(): string {
    if (!this.user) return '?';
    const f = this.user.firstName?.[0] ?? '';
    const l = this.user.lastName?.[0]  ?? '';
    return (f + l).toUpperCase() || this.user.email?.[0]?.toUpperCase() || '?';
  }

  getPasswordStrength(pwd: string): number {
    let score = 0;
    if (pwd.length >= 8)                         score++;
    if (/[A-Z]/.test(pwd) && /[a-z]/.test(pwd)) score++;
    if (/\d/.test(pwd))                           score++;
    if (/[^A-Za-z0-9]/.test(pwd))                score++;
    return score <= 1 ? 1 : score === 2 ? 2 : 3;
  }

  getPasswordStrengthLabel(pwd: string): string {
    return ['', 'Faible', 'Moyen', 'Fort'][this.getPasswordStrength(pwd)];
  }

  getStrengthColor(pwd: string): string {
    const s = this.getPasswordStrength(pwd);
    return s === 1 ? 'bg-red-500' : s === 2 ? 'bg-amber-500' : 'bg-emerald-500';
  }

  getStrengthWidth(pwd: string): string {
    return ['', 'w-1/3', 'w-2/3', 'w-full'][this.getPasswordStrength(pwd)];
  }

  getStrengthTextColor(pwd: string): string {
    const s = this.getPasswordStrength(pwd);
    return s === 1 ? 'text-red-400' : s === 2 ? 'text-amber-400' : 'text-emerald-400';
  }
}