import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { environment } from '../../../../environments/environment';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-oauth-callback',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div style="display:flex;align-items:center;
                justify-content:center;min-height:100vh;">
      <div style="text-align:center;">
        <svg style="width:2rem;height:2rem;
                    animation:spin .8s linear infinite;margin:auto;"
             viewBox="0 0 24 24" fill="none"
             stroke="#6366f1" stroke-width="2.5" stroke-linecap="round">
          <path d="M12 2a10 10 0 0 1 10 10"/>
        </svg>
        <p style="margin-top:1rem;color:#64748b;font-size:.9rem;">
          {{ message }}
        </p>
      </div>
    </div>
    <style>
      @keyframes spin { to { transform: rotate(360deg); } }
    </style>
  `
})
export class OauthCallbackComponent implements OnInit {
  private route       = inject(ActivatedRoute);
  private router      = inject(Router);
  private http        = inject(HttpClient);
  private authService = inject(AuthService); // ← ajouter

  message = 'Finalizing authentication…';

  ngOnInit(): void {
    const fullUrl  = this.router.url;
    const provider = fullUrl.includes('google') ? 'google' : 'github';
    const code     = this.route.snapshot.queryParamMap.get('code');

    if (!code) {
      this.message = 'Authentication failed. Redirecting…';
      setTimeout(() => this.router.navigate(['/auth/login']), 2000);
      return;
    }

    this.http.post<any>(
      `${environment.apiUrl}/auth/${provider}/callback`,
      {
        code,
        redirectUri: `${window.location.origin}/auth/callback/${provider}`
      }
    ).subscribe({
      next: (response) => {
        if (response.status === 200 && response.resultat) {
          // ✅ Utiliser AuthService comme le login normal
          this.authService.setSessionFromOAuth(
            response.resultat.token,
            response.resultat.user
          );
          this.router.navigate(['/dashboard']);
        } else {
          this.message = response.failMessages || 'Login failed. Redirecting…';
          setTimeout(() => this.router.navigate(['/auth/login']), 2000);
        }
      },
      error: (err) => {
        const msg = err?.error?.failMessages;
        this.message = msg || 'Server error. Redirecting…';
        setTimeout(() => this.router.navigate(['/auth/login']), 2000);
      }
    });
  }
}