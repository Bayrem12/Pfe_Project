import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-verify-email',
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
      @keyframes bounce    { 0%,100%{ transform:translateY(0); } 50%{ transform:translateY(-8px); } }

      .su-1 { animation: slideUp .65s cubic-bezier(.22,1,.36,1) .05s both; }
      .su-2 { animation: slideUp .65s cubic-bezier(.22,1,.36,1) .18s both; }
      .su-3 { animation: slideUp .65s cubic-bezier(.22,1,.36,1) .32s both; }
      .orb-1 { animation: orbMove1 22s ease-in-out infinite; }
      .orb-2 { animation: orbMove2 28s ease-in-out infinite 6s; }
      .orb-3 { animation: orbMove3 19s ease-in-out infinite 11s; }
      .gpulse { animation: gridPulse 4s ease-in-out infinite; }
      .scan-anim { animation: scanLine 3.5s linear infinite; }
      .mail-bounce { animation: bounce 2.5s ease-in-out infinite; }

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

      <!-- Grid background -->
      <div class="grid-bg gpulse fixed inset-0 pointer-events-none z-0">
        <div class="scan-anim absolute left-0 right-0"
             style="height:2px;background:linear-gradient(90deg,transparent,rgba(99,102,241,.25),transparent);"></div>
      </div>

      <!-- Ambient orbs -->
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

          <!-- Mail icon -->
          <div class="self-center flex items-center justify-center w-16 h-16 rounded-2xl mail-bounce"
               style="background:rgba(99,102,241,.08);border:1px solid rgba(99,102,241,.15);">
            <svg width="30" height="30" viewBox="0 0 24 24" fill="none"
                 stroke="#6366f1" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
              <path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z"/>
              <polyline points="22,6 12,13 2,6"/>
            </svg>
          </div>

          <!-- Title -->
          <div>
            <h1 class="font-extrabold text-slate-900 mb-1"
                style="font-family:'Syne',sans-serif;font-size:1.6rem;letter-spacing:-.03em;">
              Check your inbox
            </h1>
            <p class="text-sm text-slate-500">We've sent a verification link to your email address.</p>
          </div>

          <!-- Info box -->
          <div class="rounded-2xl px-4 py-4 text-sm text-left"
               style="background:rgba(99,102,241,.05);border:1px solid rgba(99,102,241,.15);">
            <p class="text-slate-600 leading-relaxed">
              Click the link in the email to <strong class="text-indigo-600">activate your account</strong>.
              The link expires in <strong class="text-indigo-600">24 hours</strong>.
            </p>
            <p class="text-slate-400 text-xs mt-2">
              Didn't receive it? Check your <strong>spam folder</strong>.
            </p>
          </div>

          <!-- Back to Login -->
          <a routerLink="/auth/login"
             class="btn-gradient w-full py-4 rounded-2xl text-white font-bold text-base flex items-center justify-center gap-2 no-underline">
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none"
                 stroke="white" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
              <line x1="19" y1="12" x2="5" y2="12"/>
              <polyline points="12 19 5 12 12 5"/>
            </svg>
            Back to Login
          </a>
        </div>

        <p class="su-3 text-xs text-center leading-relaxed text-slate-400">
          Need help? Contact our
          <a href="#" class="text-indigo-500 underline underline-offset-2">Support Team</a>.
        </p>

      </main>
    </div>
  `,
})
export class VerifyEmailComponent {}

