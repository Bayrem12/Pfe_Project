import { Component, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { AuthService } from '../../../core/services/auth.service';
import { environment } from '../../../../environments/environment';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, TranslatePipe],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent implements OnDestroy {

  loginForm: FormGroup;
  isLoading        = false;
  isGoogleLoading  = false;
  isGithubLoading  = false;
  errorMessage     = '';

  private destroy$ = new Subject<void>();

  constructor(
    private fb:          FormBuilder,
    private authService: AuthService,
    private router:      Router
  ) {
    this.loginForm = this.fb.group({
      email:    ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(6)]]
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  get email()    { return this.loginForm.get('email');    }
  get password() { return this.loginForm.get('password'); }

  onSubmit(): void {
    this.loginForm.markAllAsTouched();
    if (this.loginForm.invalid) return;

    this.isLoading    = true;
    this.errorMessage = '';

    this.authService.login(this.loginForm.value)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          if (response.status === 200) {
            this.router.navigate(['/dashboard']);
          } else {
            this.errorMessage = response.fail_Messages || 'Login failed. Please verify your credentials and try again.';
            this.isLoading    = false;
          }
        },
        error: (error) => {
          this.errorMessage = this.authService.extractApiErrorMessage(
            error,
            'Login failed. Please verify your credentials and try again.',
            {
              400: 'Please check your email and password format.',
              401: 'Invalid email or password.',
              429: 'Too many login attempts. Please wait a minute and try again.'
            }
          );
          this.isLoading    = false;
        }
      });
  }

  loginWithGoogle(): void {
    this.isGoogleLoading = true;
    this.errorMessage    = '';

    const REDIRECT_URI = `${window.location.origin}/auth/callback/google`;

    const authUrl = `https://accounts.google.com/o/oauth2/v2/auth?${new URLSearchParams({
      client_id:     environment.googleClientId,
      redirect_uri:  REDIRECT_URI,
      response_type: 'code',
      scope:         'openid email profile',
      access_type:   'offline',
      prompt:        'select_account'
    }).toString()}`;

    window.location.href = authUrl;
  }

  loginWithGithub(): void {
    this.isGithubLoading = true;
    this.errorMessage    = '';

    const REDIRECT_URI = `${window.location.origin}/auth/callback/github`;

    const authUrl = `https://github.com/login/oauth/authorize?${new URLSearchParams({
      client_id:    environment.githubClientId,
      redirect_uri: REDIRECT_URI,
      scope:        'user:email'
    }).toString()}`;

    window.location.href = authUrl;
  }
}