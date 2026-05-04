import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule, AbstractControl, ValidationErrors } from '@angular/forms';
import { RouterLink, Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';
import { ResponseHttp } from '../../../core/models/response-http.model';
import { AuthService } from '../../../core/services/auth.service';
import { TranslationService } from '../../../core/services/translation.service';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';
import { PLATFORM_ID, Inject } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

// ── Custom validators ─────────────────────────────────────────
const COMMON_PASSWORDS = new Set([
  '123456','password','123456789','12345678','12345','1234567',
  'password1','abc123','qwerty','azerty','letmein','iloveyou',
  'admin','welcome','monkey','login','sunshine','master'
]);

function noSpacesValidator(ctrl: AbstractControl): ValidationErrors | null {
  return /\s/.test(ctrl.value || '') ? { hasSpaces: true } : null;
}

function alphanumericStartValidator(ctrl: AbstractControl): ValidationErrors | null {
  const v: string = ctrl.value || '';
  if (!v) return null;
  return /^[a-zA-ZÀ-ÿ]/.test(v) ? null : { mustStartWithLetter: true };
}

function alphanumericOnlyValidator(ctrl: AbstractControl): ValidationErrors | null {
  const v: string = ctrl.value || '';
  if (!v) return null;
  return /^[a-zA-ZÀ-ÿ0-9]+$/.test(v) ? null : { invalidChars: true };
}

function noCommonPasswordValidator(ctrl: AbstractControl): ValidationErrors | null {
  return COMMON_PASSWORDS.has((ctrl.value || '').toLowerCase()) ? { commonPassword: true } : null;
}

function hasLetterValidator(ctrl: AbstractControl): ValidationErrors | null {
  return /[A-Za-z]/.test(ctrl.value || '') ? null : { noLetter: true };
}

function hasDigitValidator(ctrl: AbstractControl): ValidationErrors | null {
  return /[0-9]/.test(ctrl.value || '') ? null : { noDigit: true };
}

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, TranslatePipe],
  templateUrl: './register.component.html',
  // No styleUrl — styles are inline in the template
})
export class RegisterComponent implements OnInit {
  signupForm!: FormGroup;
  isLoading = false;
  isGoogleLoading = false;
  isGithubLoading = false;
  errorMessage = '';
  showPass = false;
  showConfirm = false;

  constructor(
    private fb: FormBuilder,
    private http: HttpClient,
    private authService: AuthService,
    private router: Router,
    private ts: TranslationService,
    @Inject(PLATFORM_ID) private platformId: object
  ) {}

  ngOnInit(): void {
    this.signupForm = this.fb.group(
      {
        firstName:       ['', [
          Validators.required,
          Validators.minLength(3),
          Validators.maxLength(20),
          noSpacesValidator,
          alphanumericStartValidator,
          alphanumericOnlyValidator
        ]],
        lastName:        ['', [
          Validators.required,
          Validators.minLength(3),
          Validators.maxLength(20),
          noSpacesValidator,
          alphanumericStartValidator,
          alphanumericOnlyValidator
        ]],
        email:           ['', [Validators.required, Validators.email]],
        password:        ['', [
          Validators.required,
          Validators.minLength(6),
          hasLetterValidator,
          hasDigitValidator,
          noCommonPasswordValidator
        ]],
        confirmPassword: ['', Validators.required],
        terms:           [false, Validators.requiredTrue],
      },
      { validators: this.passwordMatchValidator }
    );
  }

  // ── Helpers ─────────────────────────────────────────────────
  get firstName()       { return this.signupForm.get('firstName'); }
  get lastName()        { return this.signupForm.get('lastName'); }
  get email()           { return this.signupForm.get('email'); }
  get password()        { return this.signupForm.get('password'); }
  get confirmPassword() { return this.signupForm.get('confirmPassword'); }
  get terms()           { return this.signupForm.get('terms'); }

  // ── Cross-field validator ────────────────────────────────────
  passwordMatchValidator(form: FormGroup) {
    const pw  = form.get('password')?.value;
    const cpw = form.get('confirmPassword')?.value;
    return pw === cpw ? null : { passwordMismatch: true };
  }

  // ── Password strength ────────────────────────────────────────
  private scorePassword(v: string): number {
    let s = 0;
    if (v.length >= 8)          s++;
    if (/[A-Z]/.test(v))        s++;
    if (/[0-9]/.test(v))        s++;
    if (/[^A-Za-z0-9]/.test(v)) s++;
    return s;
  }

  getStrengthClass(segment: number): string {
    const v = this.password?.value || '';
    if (!v) return '';
    const score = this.scorePassword(v);
    if (segment > score) return '';
    if (score === 1) return 'str-w';
    if (score === 2) return 'str-m';
    return 'str-s';
  }

  getStrengthLabel(): string {
    const v = this.password?.value || '';
    if (!v) return '';
    return ['', 'Weak', 'Medium', 'Strong', 'Very strong'][this.scorePassword(v)] || '';
  }

  getStrengthColor(): string {
    const v = this.password?.value || '';
    if (!v) return 'rgba(148,163,184,.4)';
    return ['', 'rgba(248,113,113,.8)', 'rgba(251,146,60,.8)', 'rgba(74,222,128,.8)', 'rgba(74,222,128,.8)'][this.scorePassword(v)] || 'rgba(148,163,184,.4)';
  }

  // ── Submit ───────────────────────────────────────────────────
  signUpWithGoogle(): void {
    if (!isPlatformBrowser(this.platformId)) return;
    this.isGoogleLoading = true;
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

  signUpWithGithub(): void {
    if (!isPlatformBrowser(this.platformId)) return;
    this.isGithubLoading = true;
    const REDIRECT_URI = `${window.location.origin}/auth/callback/github`;
    const authUrl = `https://github.com/login/oauth/authorize?${new URLSearchParams({
      client_id:    environment.githubClientId,
      redirect_uri: REDIRECT_URI,
      scope:        'user:email'
    }).toString()}`;
    window.location.href = authUrl;
  }

  onSubmit(): void {
    this.signupForm.markAllAsTouched();
    if (this.signupForm.invalid) return;

    const { firstName, lastName, email, password } = this.signupForm.value;
    this.isLoading = true;
    this.errorMessage = '';

    this.http
      .post<ResponseHttp>(`${environment.apiUrl}/auth/register`, { firstName, lastName, email, password })
      .subscribe({
        next: (res) => {
          if (res.status === 200) {
            this.router.navigate(['/auth/verify-email']);
          } else {
            this.errorMessage = res.fail_Messages || 'Registration failed. Please review your details and try again.';
            this.isLoading = false;
          }
        },
        error: (err) => {
          this.errorMessage = this.authService.extractApiErrorMessage(
            err,
            'Registration failed. Please review your details and try again.',
            {
              400: 'Some registration fields are invalid. Please review and try again.',
              409: 'An account with this email already exists.'
            }
          );
          this.isLoading = false;
        },
      });
  }
}