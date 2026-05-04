import { Component, Input, inject, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { finalize } from 'rxjs/operators';
import {
  NlpService,
  FailureAnalysisResult,
  AnalyzeFailureRequest,
} from '../../nlp/services/nlp.service';

/**
 * AI Failure Analyzer — visually identical to AiScenarioAnalyzerComponent, but
 * for the post-execution side: takes a failed step and shows a precise verdict.
 *
 * Drop in wherever a failed step is shown:
 *   <app-ai-failure-analyzer
 *     [stepText]="step.stepText"
 *     [errorMessage]="step.errorMessage"
 *     [selector]="step.selectorUsed"
 *     [keyword]="step.stepType"
 *     [autoResult]="step.aiAnalysis">
 *   </app-ai-failure-analyzer>
 */
@Component({
  selector: 'app-ai-failure-analyzer',
  standalone: true,
  imports: [CommonModule],
  template: `
    <!-- ══════════════════════════════════════════════════════════
         AI FAILURE ANALYZER · International / Enterprise Grade
         ══════════════════════════════════════════════════════════ -->
    <div class="relative overflow-hidden rounded-2xl bg-white ring-1 ring-slate-200/80
                shadow-[0_20px_60px_-20px_rgba(15,23,42,0.20)]">

      <!-- Decorative gradient backdrop -->
      <div class="pointer-events-none absolute inset-0 opacity-60">
        <div class="absolute -top-24 -right-24 h-64 w-64 rounded-full bg-gradient-to-br from-rose-300/30 to-purple-400/20 blur-3xl"></div>
        <div class="absolute -bottom-24 -left-24 h-64 w-64 rounded-full bg-gradient-to-tr from-indigo-300/20 to-cyan-300/10 blur-3xl"></div>
      </div>

      <!-- ── HEADER ──────────────────────────────────────────── -->
      <header class="relative flex items-center justify-between gap-4 border-b border-slate-200/70
                     bg-gradient-to-r from-slate-900 via-slate-800 to-slate-900 px-6 py-4">
        <div class="flex items-center gap-3">
          <div class="relative flex h-10 w-10 items-center justify-center rounded-xl
                      bg-gradient-to-br from-rose-500 via-fuchsia-500 to-violet-600
                      shadow-lg shadow-rose-500/30">
            <span class="material-symbols-outlined text-[20px] text-white">neurology</span>
            <span class="absolute -right-1 -top-1 h-3 w-3 rounded-full bg-emerald-400 ring-2 ring-slate-900">
              <span class="absolute inset-0 animate-ping rounded-full bg-emerald-400 opacity-75"></span>
            </span>
          </div>
          <div>
            <p class="flex items-center gap-2 text-[13px] font-bold tracking-tight text-white">
              AI Failure Analyzer
              <span class="rounded-md bg-white/10 px-1.5 py-0.5 text-[9px] font-bold uppercase tracking-widest text-white/70 ring-1 ring-white/15">
                v2 · Pro
              </span>
            </p>
            <p class="mt-0.5 text-[10px] font-medium tracking-wide text-slate-400">
              Deterministic root-cause engine · 0-latency verdict
            </p>
          </div>
        </div>

        <button
          (click)="runAnalysis()"
          [disabled]="loading || !errorMessage"
          class="group relative flex items-center gap-2 overflow-hidden rounded-full
                 bg-gradient-to-r from-rose-500 via-fuchsia-500 to-violet-600 px-5 py-2.5
                 text-[11px] font-bold uppercase tracking-wider text-white
                 shadow-lg shadow-rose-500/40 transition-all
                 hover:scale-[1.04] hover:shadow-rose-500/60 active:scale-95
                 disabled:cursor-not-allowed disabled:opacity-50 disabled:hover:scale-100">
          <span class="absolute inset-0 -translate-x-full bg-gradient-to-r from-transparent via-white/30 to-transparent transition-transform duration-700 group-hover:translate-x-full"></span>
          <ng-container *ngIf="!loading">
            <span class="material-symbols-outlined text-[16px]">{{ result ? 'refresh' : 'auto_awesome' }}</span>
            {{ result ? 'Re-analyze' : 'Analyze Failure' }}
          </ng-container>
          <ng-container *ngIf="loading">
            <span class="h-3.5 w-3.5 animate-spin rounded-full border-2 border-white border-t-transparent"></span>
            Analyzing
          </ng-container>
        </button>
      </header>

      <!-- ── BODY ────────────────────────────────────────────── -->
      <div *ngIf="result" class="relative">

        <!-- ── VERDICT HERO ─────────────────────────────────── -->
        <section class="grid grid-cols-1 gap-6 border-b border-slate-200/70 px-6 py-6 md:grid-cols-[auto_1fr_auto]">

          <!-- Confidence Ring -->
          <div class="relative flex h-20 w-20 shrink-0 items-center justify-center">
            <svg class="h-20 w-20 -rotate-90" viewBox="0 0 56 56">
              <defs>
                <linearGradient [attr.id]="'gradFail-' + uid" x1="0%" y1="0%" x2="100%" y2="100%">
                  <stop offset="0%" [attr.stop-color]="confidenceFillColor"/>
                  <stop offset="100%" [attr.stop-color]="confidenceFillColorEnd"/>
                </linearGradient>
              </defs>
              <circle cx="28" cy="28" r="22" fill="none" stroke="#e2e8f0" stroke-width="5"/>
              <circle cx="28" cy="28" r="22" fill="none"
                [attr.stroke]="'url(#gradFail-' + uid + ')'"
                stroke-width="5"
                stroke-linecap="round"
                [attr.stroke-dasharray]="138.2"
                [attr.stroke-dashoffset]="138.2 - (138.2 * confidencePct / 100)"
                class="transition-all duration-700 ease-out"/>
            </svg>
            <div class="absolute inset-0 flex flex-col items-center justify-center">
              <span class="text-lg font-extrabold leading-none text-slate-900">{{ confidencePct }}<span class="text-xs">%</span></span>
              <span class="mt-0.5 text-[8px] font-bold uppercase tracking-widest text-slate-400">conf.</span>
            </div>
          </div>

          <!-- Verdict text -->
          <div class="min-w-0">
            <div class="mb-2 flex flex-wrap items-center gap-2">
              <span [class]="categoryBadgeClass"
                    class="inline-flex items-center gap-1.5 rounded-lg px-2.5 py-1 text-[11px] font-bold uppercase tracking-wider ring-1">
                <span class="text-sm">{{ categoryEmoji }}</span>
                {{ result.category | titlecase }}
              </span>
              <span class="inline-flex items-center gap-1 rounded-lg bg-slate-100 px-2 py-0.5 text-[10px] font-mono font-bold uppercase tracking-wide text-slate-700 ring-1 ring-slate-200">
                <span class="material-symbols-outlined text-[12px]">tag</span>
                {{ result.root_cause }}
              </span>
              <span [class]="ownershipChipClass"
                    class="inline-flex items-center gap-1 rounded-lg px-2 py-0.5 text-[10px] font-bold uppercase tracking-wider ring-1">
                <span class="material-symbols-outlined text-[12px]">{{ result.is_test_issue ? 'description' : 'apps' }}</span>
                {{ result.is_test_issue ? 'Test Issue' : 'App Issue' }}
              </span>
            </div>

            <h3 class="text-base font-bold leading-snug tracking-tight text-slate-900">
              {{ result.title }}
            </h3>

            <p *ngIf="result.where" class="mt-1.5 flex items-center gap-1.5 text-[11px] text-slate-500">
              <span class="material-symbols-outlined text-[13px]">my_location</span>
              <span class="font-medium">{{ result.where }}</span>
            </p>
          </div>

          <!-- Severity strip -->
          <div class="flex flex-col items-end gap-1.5 self-start">
            <span class="text-[9px] font-bold uppercase tracking-widest text-slate-400">Severity</span>
            <div class="flex gap-1">
              <span *ngFor="let _ of [0,1,2,3,4]; let i = index"
                    class="h-1.5 w-3 rounded-sm transition-colors"
                    [class.bg-slate-200]="i >= severityLevel"
                    [class.bg-emerald-400]="i < severityLevel && severityLevel <= 1"
                    [class.bg-amber-400]="i < severityLevel && severityLevel === 2"
                    [class.bg-orange-500]="i < severityLevel && severityLevel === 3"
                    [class.bg-rose-500]="i < severityLevel && severityLevel >= 4"></span>
            </div>
            <span class="text-[10px] font-semibold text-slate-600">{{ severityLabel }}</span>
          </div>
        </section>

        <!-- ── METRICS ROW ─────────────────────────────────── -->
        <section class="grid grid-cols-2 gap-px bg-slate-200/70 sm:grid-cols-4">
          <div class="bg-white px-4 py-3">
            <p class="text-[9px] font-bold uppercase tracking-widest text-slate-400">Category</p>
            <p class="mt-1 text-sm font-bold text-slate-900">{{ result.category | titlecase }}</p>
          </div>
          <div class="bg-white px-4 py-3">
            <p class="text-[9px] font-bold uppercase tracking-widest text-slate-400">Confidence</p>
            <p class="mt-1 text-sm font-bold" [style.color]="confidenceFillColor">{{ confidencePct }}%</p>
          </div>
          <div class="bg-white px-4 py-3">
            <p class="text-[9px] font-bold uppercase tracking-widest text-slate-400">Owner</p>
            <p class="mt-1 text-sm font-bold text-slate-900">{{ result.is_test_issue ? 'QA / Test' : 'Dev / App' }}</p>
          </div>
          <div class="bg-white px-4 py-3">
            <p class="text-[9px] font-bold uppercase tracking-widest text-slate-400">Issues</p>
            <p class="mt-1 text-sm font-bold text-slate-900">{{ issuesList.length || 1 }}</p>
          </div>
        </section>

        <!-- ── SECTIONS ────────────────────────────────────── -->
        <div class="space-y-6 px-6 py-6">

          <!-- ❌ Failure Summary -->
          <section>
            <header class="mb-3 flex items-center gap-2">
              <span class="flex h-6 w-6 items-center justify-center rounded-md bg-rose-100 text-rose-600">
                <span class="material-symbols-outlined text-[15px]">error</span>
              </span>
              <h4 class="text-[11px] font-extrabold uppercase tracking-[0.18em] text-slate-700">Failure Summary</h4>
            </header>
            <div class="rounded-xl border border-rose-200 bg-gradient-to-r from-rose-50 to-rose-50/30 p-4">
              <p class="text-sm font-semibold leading-snug text-rose-900">{{ result.title }}</p>
              <p *ngIf="errorMessage" class="mt-2 break-all rounded-md border border-rose-200/70 bg-white/70 px-3 py-2 font-mono text-[11px] leading-relaxed text-rose-700">
                {{ errorMessage }}
              </p>
            </div>
          </section>

          <!-- 🔍 Root Cause Analysis -->
          <section *ngIf="result.explanation">
            <header class="mb-3 flex items-center gap-2">
              <span class="flex h-6 w-6 items-center justify-center rounded-md bg-indigo-100 text-indigo-600">
                <span class="material-symbols-outlined text-[15px]">search</span>
              </span>
              <h4 class="text-[11px] font-extrabold uppercase tracking-[0.18em] text-slate-700">Root Cause Analysis</h4>
            </header>
            <div class="rounded-xl border border-slate-200 bg-slate-50/60 p-4">
              <p class="whitespace-pre-wrap text-sm leading-relaxed text-slate-700">{{ result.explanation }}</p>
            </div>
          </section>

          <!-- ⚠️ Issues Detected -->
          <section *ngIf="issuesList.length > 0">
            <header class="mb-3 flex items-center gap-2">
              <span class="flex h-6 w-6 items-center justify-center rounded-md bg-amber-100 text-amber-600">
                <span class="material-symbols-outlined text-[15px]">warning</span>
              </span>
              <h4 class="text-[11px] font-extrabold uppercase tracking-[0.18em] text-slate-700">
                Issues Detected
                <span class="ml-1 rounded-full bg-amber-100 px-1.5 py-0.5 text-[9px] font-bold text-amber-700">{{ issuesList.length }}</span>
              </h4>
            </header>
            <ol class="space-y-2">
              <li *ngFor="let issue of issuesList; let i = index"
                  class="flex gap-3 rounded-xl border border-amber-200/70 bg-gradient-to-r from-amber-50 to-amber-50/30 p-3">
                <span class="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-amber-500 text-[11px] font-bold text-white">
                  {{ i + 1 }}
                </span>
                <p class="text-sm leading-snug text-amber-900">{{ issue }}</p>
              </li>
            </ol>
          </section>

          <!-- 💡 Suggested Fix -->
          <section *ngIf="suggestionList.length > 0">
            <header class="mb-3 flex items-center gap-2">
              <span class="flex h-6 w-6 items-center justify-center rounded-md bg-blue-100 text-blue-600">
                <span class="material-symbols-outlined text-[15px]">lightbulb</span>
              </span>
              <h4 class="text-[11px] font-extrabold uppercase tracking-[0.18em] text-slate-700">Suggested Fix</h4>
            </header>
            <div class="rounded-xl border border-blue-200 bg-gradient-to-br from-blue-50 to-cyan-50/40 p-4">
              <ul class="space-y-2.5">
                <li *ngFor="let s of suggestionList" class="flex gap-3 text-sm leading-relaxed text-blue-900">
                  <span class="mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-blue-500 text-white">
                    <span class="material-symbols-outlined text-[13px]">check</span>
                  </span>
                  <span>{{ s }}</span>
                </li>
              </ul>
            </div>
          </section>

          <!-- 📊 Debug Info -->
          <section>
            <header class="mb-3 flex items-center justify-between">
              <div class="flex items-center gap-2">
                <span class="flex h-6 w-6 items-center justify-center rounded-md bg-emerald-100 text-emerald-600">
                  <span class="material-symbols-outlined text-[15px]">terminal</span>
                </span>
                <h4 class="text-[11px] font-extrabold uppercase tracking-[0.18em] text-slate-700">Debug Info</h4>
              </div>
              <span class="rounded-md bg-slate-100 px-2 py-0.5 font-mono text-[10px] font-bold text-slate-500">trace.log</span>
            </header>
            <div class="overflow-hidden rounded-xl border border-slate-800 bg-slate-950 shadow-inner">
              <!-- terminal chrome -->
              <div class="flex items-center gap-1.5 border-b border-slate-800 bg-slate-900 px-3 py-2">
                <span class="h-2.5 w-2.5 rounded-full bg-rose-500"></span>
                <span class="h-2.5 w-2.5 rounded-full bg-amber-400"></span>
                <span class="h-2.5 w-2.5 rounded-full bg-emerald-500"></span>
                <span class="ml-2 font-mono text-[10px] text-slate-500">failure-analysis · {{ result.root_cause }}</span>
              </div>
              <div class="max-h-72 space-y-1 overflow-auto p-4 font-mono text-[11px] leading-relaxed">
                <div *ngIf="keyword || stepText" class="flex gap-2">
                  <span class="w-16 shrink-0 text-fuchsia-400 font-bold">{{ keyword || 'STEP' }}</span>
                  <span class="text-slate-300 break-all">{{ stepText }}</span>
                </div>
                <div *ngIf="selector" class="flex gap-2">
                  <span class="w-16 shrink-0 text-cyan-400 font-bold">SELECTOR</span>
                  <span class="text-slate-300 break-all">{{ selector }}</span>
                </div>
                <div *ngIf="result.where" class="flex gap-2">
                  <span class="w-16 shrink-0 text-emerald-400 font-bold">WHERE</span>
                  <span class="text-slate-300">{{ result.where }}</span>
                </div>
                <div *ngIf="errorMessage" class="flex gap-2">
                  <span class="w-16 shrink-0 text-rose-400 font-bold">ERROR</span>
                  <span class="text-slate-300 break-all">{{ errorMessage }}</span>
                </div>
                <div class="flex gap-2 border-t border-slate-800 pt-2 mt-2">
                  <span class="w-16 shrink-0 text-amber-400 font-bold">VERDICT</span>
                  <span class="text-slate-300">
                    {{ result.root_cause }}
                    <span class="text-slate-500">·</span>
                    <span [style.color]="confidenceFillColor">{{ confidencePct }}% confidence</span>
                  </span>
                </div>
              </div>
            </div>
          </section>

          <!-- Best Practices -->
          <details class="group rounded-xl border border-slate-200 bg-slate-50/50 px-4 py-3 open:bg-slate-50">
            <summary class="flex cursor-pointer select-none items-center justify-between text-[11px] font-extrabold uppercase tracking-[0.18em] text-slate-600 transition-colors hover:text-slate-900">
              <span class="flex items-center gap-2">
                <span class="flex h-5 w-5 items-center justify-center rounded-md bg-violet-100 text-violet-600">
                  <span class="material-symbols-outlined text-[13px]">school</span>
                </span>
                Best Practices
              </span>
              <span class="material-symbols-outlined text-[16px] text-slate-400 transition-transform group-open:rotate-180">expand_more</span>
            </summary>
            <ul class="mt-3 space-y-1.5 pl-7">
              <li *ngFor="let bp of bestPractices" class="flex gap-2 text-xs leading-relaxed text-slate-600">
                <span class="text-violet-400">▸</span>
                {{ bp }}
              </li>
            </ul>
          </details>
        </div>

        <!-- ── FOOTER ─────────────────────────────────────── -->
        <footer class="flex flex-wrap items-center justify-between gap-2 border-t border-slate-200/70 bg-slate-50/70 px-6 py-3 text-[10px] text-slate-500">
          <span class="flex items-center gap-1.5">
            <span class="h-1.5 w-1.5 rounded-full bg-emerald-500"></span>
            <span class="font-medium">Analysis complete · {{ result.category | titlecase }} layer</span>
          </span>
          <span class="font-mono">Powered by AutoTestify AI · Deterministic Engine</span>
        </footer>
      </div>

      <!-- ── IDLE ───────────────────────────────────────────── -->
      <div *ngIf="!result && !loading" class="relative px-6 py-10 text-center">
        <div class="mx-auto mb-3 flex h-14 w-14 items-center justify-center rounded-full bg-gradient-to-br from-rose-100 to-violet-100">
          <span class="material-symbols-outlined text-3xl text-rose-500">troubleshoot</span>
        </div>
        <p class="text-sm font-bold text-slate-700">Ready to investigate</p>
        <p class="mx-auto mt-1 max-w-md text-xs leading-relaxed text-slate-500">
          Click <strong class="text-slate-700">Analyze Failure</strong> to receive a precise verdict — root cause,
          severity, suggested fix and full debug trace.
        </p>
      </div>

      <!-- ── ERROR STATE ────────────────────────────────────── -->
      <div *ngIf="errorState" class="relative flex items-start gap-2 border-t border-rose-200 bg-rose-50 px-6 py-3 text-xs text-rose-700">
        <span class="material-symbols-outlined text-[15px]">error</span>
        <span class="font-medium">{{ errorState }}</span>
      </div>
    </div>
  `,
})
export class AiFailureAnalyzerComponent implements OnChanges {
  @Input() stepText = '';
  @Input() errorMessage = '';
  @Input() selector = '';
  @Input() keyword = '';
  @Input() visualFallbackUsed = false;
  @Input() retryCount = 0;
  /** If the backend already attached an analysis, show it without a click. */
  @Input() autoResult: FailureAnalysisResult | null = null;

  private nlpService = inject(NlpService);

  loading = false;
  result: FailureAnalysisResult | null = null;
  errorState = '';

  bestPractices: string[] = [
    'Always wait for the application to finish loading before asserting visible text.',
    'Prefer stable selectors (data-testid, role, label) over fragile CSS/XPath.',
    'Match assertion values exactly — case and spacing both count.',
    'When a test fails, reproduce manually first to decide if it is an app bug or a test bug.',
  ];

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['autoResult'] && this.autoResult) {
      this.result = this.autoResult;
    }
  }

  runAnalysis(): void {
    if (!this.errorMessage) return;
    const req: AnalyzeFailureRequest = {
      stepText: this.stepText,
      errorMessage: this.errorMessage,
      selector: this.selector,
      keyword: this.keyword,
      visualFallbackUsed: this.visualFallbackUsed,
      retryCount: this.retryCount,
    };
    this.loading = true;
    this.errorState = '';
    this.nlpService
      .analyzeFailure(req)
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: r => (this.result = r),
        error: () => (this.errorState = 'Could not reach the AI agent. Please try again.'),
      });
  }

  // ── Derived helpers ──────────────────────────────────────────────────

  get confidencePct(): number {
    if (!this.result?.confidence) return 0;
    return Math.round(Math.max(0, Math.min(1, this.result.confidence)) * 100);
  }

  /** Unique id for SVG gradient defs (avoid collisions when multiple instances render). */
  readonly uid: string = Math.random().toString(36).slice(2, 9);

  get confidenceFillColor(): string {
    const pct = this.confidencePct;
    if (pct >= 75) return '#10b981';
    if (pct >= 45) return '#f59e0b';
    return '#ef4444';
  }

  get confidenceFillColorEnd(): string {
    const pct = this.confidencePct;
    if (pct >= 75) return '#34d399';
    if (pct >= 45) return '#fbbf24';
    return '#f87171';
  }

  /** Severity 1..5 derived from confidence + category. */
  get severityLevel(): number {
    if (!this.result) return 0;
    const c = (this.result.category || '').toLowerCase();
    const pct = this.confidencePct;
    if (c === 'application') return pct >= 75 ? 5 : 4;
    if (c === 'detection')   return 3;
    if (c === 'timing')      return 2;
    if (c === 'test')        return 2;
    if (c === 'environment') return 3;
    return 2;
  }

  get severityLabel(): string {
    const lvl = this.severityLevel;
    return ['', 'Low', 'Low', 'Medium', 'High', 'Critical'][lvl] || 'Unknown';
  }

  get ownershipChipClass(): string {
    return this.result?.is_test_issue
      ? 'bg-amber-50 text-amber-700 ring-amber-200'
      : 'bg-rose-50 text-rose-700 ring-rose-200';
  }

  get categoryEmoji(): string {
    const c = (this.result?.category || '').toLowerCase();
    if (c === 'application') return '🐞';
    if (c === 'test') return '📝';
    if (c === 'detection') return '🎯';
    if (c === 'timing') return '⏱️';
    if (c === 'environment') return '🌐';
    return '❓';
  }

  get categoryBadgeClass(): string {
    const c = (this.result?.category || '').toLowerCase();
    if (c === 'application') return 'bg-rose-50 text-rose-700 ring-rose-200';
    if (c === 'test')        return 'bg-amber-50 text-amber-700 ring-amber-200';
    if (c === 'detection')   return 'bg-blue-50 text-blue-700 ring-blue-200';
    if (c === 'timing')      return 'bg-indigo-50 text-indigo-700 ring-indigo-200';
    if (c === 'environment') return 'bg-slate-100 text-slate-700 ring-slate-200';
    return 'bg-slate-100 text-slate-700 ring-slate-200';
  }

  /** Split the explanation into bullet-style issues by sentence/line. */
  get issuesList(): string[] {
    const ex = (this.result?.explanation || '').trim();
    if (!ex) return [];
    // Drop the technical "Engine error: …" / "Diff: …" trailing line — already in Debug Info.
    const cleaned = ex
      .split('\n')
      .filter(l => !/^(engine error|diff|current url):/i.test(l.trim()))
      .join(' ')
      .trim();
    if (!cleaned) return [];
    // Split on sentence boundaries; keep meaningful ones only.
    return cleaned
      .split(/(?<=[.!?])\s+(?=[A-Z“"'])/)
      .map(s => s.trim())
      .filter(s => s.length > 12)
      .slice(0, 4);
  }

  /** Split the suggested_fix into actionable bullet items. */
  get suggestionList(): string[] {
    const fix = (this.result?.suggested_fix || '').trim();
    if (!fix) return [];
    // The agent emits bullet-style fixes separated by "•" or newlines.
    return fix
      .split(/\n|•/)
      .map(s => s.replace(/^[\s•\-]+/, '').trim())
      .filter(s => s.length > 0);
  }
}
