import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, TranslatePipe],
  templateUrl: './forgot-password.component.html',
})
export class ForgotPasswordComponent {
  private authService = inject(AuthService);
  private fb = inject(FormBuilder);

  forgotPasswordForm: FormGroup = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
  });

  isLoading = false;
  successMessage = '';
  errorMessage = '';

  onSubmit(): void {
    this.forgotPasswordForm.markAllAsTouched();
    if (this.forgotPasswordForm.invalid) return;

    this.isLoading = true;
    this.errorMessage = '';
    this.successMessage = '';

    const { email } = this.forgotPasswordForm.value;

    this.authService.forgotPassword(email).subscribe({
      next: (res: any) => {
        this.isLoading = false;
        if (res?.status === 200) {
          this.successMessage = 'A password reset link has been sent to your email.';
          this.forgotPasswordForm.reset();
        } else {
          this.errorMessage = res?.fail_Messages || 'Something went wrong. Please try again.';
        }
      },
      error: (err: any) => {
        this.isLoading = false;
        const status = err?.status;
        if (status === 404) {
          this.errorMessage = 'No account found with this email address.';
        } else {
          this.errorMessage = err?.error?.fail_Messages || 'Something went wrong. Please try again.';
        }
      }
    });
  }
}