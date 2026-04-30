import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';
import { ResponseHttp } from '../../../core/models/response-http.model';

@Component({
  selector: 'app-verify-email-confirm',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <style>
      @keyframes shimmer   { 0%{ background-position:-200% center; } 100%{ background-position:200% center; } }
      @keyframes slideUp   { from{ opacity:0; transform:translateY(26px); } to{ opacity:1; transform:translateY(0); } }
      @keyframes orbMove1  { 0%,100%{ transform:translate(0,0); } 50%{ transform:translate(60px,-45px); } }
      @keyframes orbMove2  { 0%,100%{ transform:translate(0,0); } 50%{ transform:translate(-50px,55px); } }
      @keyframes orbMove3  { 0%,100%{ transform:translate(0,0); } 50%{ transform:translate(40px,45px); } }
      @keyframes gridPulse { 0%,100%{ opacity:.05; } 50%{ opacity:.10; } }
      @keyframes scanLine  { 0%{ top:-2px; opacity:1; } 85%{ opacity:.6; } 100%{ top:100%; opacity:0; } }
      @keyframes spin      { to { transform: rotate(360deg); } }
      @keyframes fadeInUp  { from{ opacity:0; transform:translateY(8px); } to{ opacity:1; transform:translateY(0); } }

      .su-1 { animation: slideUp .65s cubic-bezier(.22,1,.36,1) .05s both; }
      .su-2 { animation: slideUp .65s cubic-bezier(.22,1,.36,1) .18s both; }
      .su-3 { animation: slideUp .65s cubic-bezier(.22,1,.36,1) .32s both; }
      .orb-1 { animation: orbMove1 22s ease-in-out infinite; }
      .orb-2 { animation: orbMove2 28s ease-in-out infinite 6s; }
      .orb-3 { animation: orbMove3 19s ease-in-out infinite 11s; }
      .gpulse { animation: gridPulse 4s ease-in-out infinite; }
      .scan-anim { animation: scanLine 3.5s linear infinite; }
      .spin { animation: spin .75s linear infinite; }
      .fade-in { animation: fadeInUp .3s ease both; }

      .shimmer-brand {
        background: linear-gradient(90deg,#3b82f6,#8b5cf6,#3b82f6);
        background-size: 200% auto;
        -webkit-background-clip: text; -webkit-text-fill-color: transparent;
        background-clip: text; animation: shimmer 3s linear infinite;
      }
      .grid-bg {
        background-image:
          linear-gradient(rgba(99,102,241,.07) 1px, transparent 1px),
          linear-gradient(90deg, rgba(99,102,241,.07) 1px, transparent 1px);
        background-size: 40px 40px;
      }
      .btn-gradient {
        background: linear-gradient(135deg, #3b82f6, #6366f1);
        position: relative; overflow: hidden;
        transition: box-shadow .3s, transform .2s;
        box-shadow: 0 4px 20px rgba(99,102,241,.25);
      }
      .btn-gradient::before {
        content: ''; position: absolute; inset: 0;
        background: linear-gradient(135deg, #60a5fa, #818cf8);
        opacity: 0; transition: opacity .3s;
      }
      .btn-gradient:hover::before { opacity: 1; }
      .btn-gradient:hover { box-shadow: 0 6px 28px rgba(99,102,241,.38); }
      .btn-gradient:active { transform: scale(.98); }
      .btn-gradient > * { position: relative; z-index: 1; }
    </style>

    <div class="relative min-h-screen flex flex-col items-center justify-center px-6 py-8 font-sans text-slate-800"
         style="background-color:#f9f9ff;
                background-image:radial-gradient(circle at 50% 0%,hsla(220,100%,98%,1) 0,transparent 60%),
                                 radial-gradient(circle at 0% 100%,hsla(230,100%,97%,1) 0,transparent 50%),
                                 radial-gradient(circle at 100% 50%,hsla(210,100%,98%,1) 0,transparent 50%);">

      <div class="grid-bg gpulse fixed inset-0 pointer-events-none z-0">
        <div class="scan-anim absolute left-0 right-0"
             style="height:2px;background:linear-gradient(90deg,transparent,rgba(99,102,241,.25),transparent);"></div>
      </div>
      <div class="orb-1 fixed rounded-full pointer-events-none z-0"
           style="top:-5%;left:-10%;width:500px;height:500px;
                  background:radial-gradient(circle,rgba(99,102,241,.08) 0%,transparent 70%);filter:blur(30px);"></div>
      <div class="orb-2 fixed rounded-full pointer-events-none z-0"
           style="bottom:-10%;right:-5%;width:400px;height:400px;
                  background:radial-gradient(circle,rgba(139,92,246,.06) 0%,transparent 70%);filter:blur(30px);"></div>
      <div class="orb-3 fixed rounded-full pointer-events-none z-0"
           style="top:50%;left:50%;width:300px;height:300px;
                  background:radial-gradient(circle,rgba(59,130,246,.06) 0%,transparent 70%);filter:blur(30px);"></div>

      <main class="relative flex flex-col items-center gap-7 w-full max-w-sm z-10">

        <!-- Logo -->
        <div class="su-1">
          <a routerLink="/landing" class="shimmer-brand"
             style="font-family:'Syne',sans-serif;font-size:1.5rem;font-weight:800;letter-spacing:-.04em;text-decoration:none;">
            AutoTestify
          </a>
        </div>

        <!-- Card -->
        <div class="su-2 w-full rounded-3xl p-10 flex flex-col gap-6 text-center"
             style="background:rgba(255,255,255,.80);border:1px solid rgba(0,0,0,.06);
                    backdrop-filter:blur(20px);-webkit-backdrop-filter:blur(20px);
                    box-shadow:0 12px 40px rgba(99,102,241,.08);">

          <!-- LOADING -->
          @if (state === 'loading') {
            <div class="self-center flex items-center justify-center w-16 h-16 rounded-2xl"
                 style="background:rgba(99,102,241,.08);border:1px solid rgba(99,102,241,.15);">
              <svg class="spin" width="28" height="28" viewBox="0 0 24 24"
                   fill="none" stroke="#6366f1" stroke-width="2.5" stroke-linecap="round">
                <path d="M12 2a10 10 0 0 1 10 10"/>
              </svg>
            </div>
            <div>
              <h1 class="font-extrabold text-slate-900 mb-1"
                  style="font-family:'Syne',sans-serif;font-size:1.6rem;letter-spacing:-.03em;">
                Verifying your email…
              </h1>
              <p class="text-sm text-slate-500">Please wait a moment.</p>
            </div>
          }

          <!-- SUCCESS -->
          @if (state === 'success') {
            <div class="self-center flex items-center justify-center w-16 h-16 rounded-2xl fade-in"
                 style="background:rgba(16,185,129,.08);border:1px solid rgba(16,185,129,.2);">
              <svg width="28" height="28" viewBox="0 0 24 24" fill="none"
                   stroke="#10b981" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
                <polyline points="20 6 9 17 4 12"/>
              </svg>
            </div>
            <div class="fade-in">
              <h1 class="font-extrabold text-slate-900 mb-1"
                  style="font-family:'Syne',sans-serif;font-size:1.6rem;letter-spacing:-.03em;">
                Email verified!
              </h1>
              <p class="text-sm text-slate-500">
                Your account is now active. Redirecting to login in
                <span class="font-bold text-indigo-600">{{ countdown }}</span> seconds…
              </p>
            </div>
            <a routerLink="/auth/login"
               class="btn-gradient w-full py-4 rounded-2xl text-white font-bold text-base flex items-center justify-center gap-2 no-underline fade-in">
              <span>Go to Login</span>
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none"
                   stroke="white" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
                <line x1="5" y1="12" x2="19" y2="12"/>
                <polyline points="12 5 19 12 12 19"/>
              </svg>
            </a>
          }

          <!-- ERROR -->
          @if (state === 'error') {
            <div class="self-center flex items-center justify-center w-16 h-16 rounded-2xl fade-in"
                 style="background:rgba(239,68,68,.08);border:1px solid rgba(239,68,68,.2);">
              <svg width="28" height="28" viewBox="0 0 24 24" fill="none"
                   stroke="#ef4444" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z"/>
                <line x1="12" y1="9" x2="12" y2="13"/>
                <line x1="12" y1="17" x2="12.01" y2="17"/>
              </svg>
            </div>
            <div class="fade-in">
              <h1 class="font-extrabold text-slate-900 mb-1"
                  style="font-family:'Syne',sans-serif;font-size:1.6rem;letter-spacing:-.03em;">
                Verification failed
              </h1>
              <p class="text-sm text-slate-500">{{ errorMessage }}</p>
            </div>
            <a routerLink="/auth/register"
               class="btn-gradient w-full py-4 rounded-2xl text-white font-bold text-base flex items-center justify-center gap-2 no-underline fade-in">
              Back to Register
            </a>
          }

        </div>

        <p class="su-3 text-xs text-center leading-relaxed text-slate-400">
          Need help? Contact our
          <a href="#" class="text-indigo-500 underline underline-offset-2">Support Team</a>.
        </p>

      </main>
    </div>
  `,
})
export class VerifyEmailConfirmComponent implements OnInit {
  state: 'loading' | 'success' | 'error' = 'loading';
  errorMessage = 'The verification link is invalid or has expired.';
  countdown = 4;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private http: HttpClient
  ) {}

  ngOnInit(): void {
    const token = this.route.snapshot.queryParamMap.get('token');

    if (!token) {
      this.state = 'error';
      this.errorMessage = 'No verification token found in the URL.';
      return;
    }

    this.http
      .get<ResponseHttp>(`${environment.apiUrl}/auth/verify-email`, { params: { token } })
      .subscribe({
        next: (res) => {
          if (res.status === 200) {
            this.state = 'success';
            this.startCountdown();
          } else {
            this.state = 'error';
            this.errorMessage = res.fail_Messages || this.errorMessage;
          }
        },
        error: (err) => {
          this.state = 'error';
          this.errorMessage =
            err?.error?.fail_Messages ||
            err?.error?.message ||
            'The verification link is invalid or has expired.';
        },
      });
  }

  private startCountdown(): void {
    const interval = setInterval(() => {
      this.countdown--;
      if (this.countdown <= 0) {
        clearInterval(interval);
        this.router.navigate(['/auth/login']);
      }
    }, 1000);
  }
}

