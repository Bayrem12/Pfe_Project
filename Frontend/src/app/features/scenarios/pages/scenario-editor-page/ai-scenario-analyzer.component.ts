import { Component, Input, Output, EventEmitter, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NlpService, ScenarioQualityResult, QualityIssue } from '../../../nlp/services/nlp.service';
import { finalize } from 'rxjs/operators';

@Component({
  selector: 'app-ai-scenario-analyzer',
  standalone: true,
  imports: [CommonModule],
  template: `
    <!-- ── AI Scenario Analyzer ────────────────────────────────── -->
    <div class="bg-gradient-to-br from-violet-50 to-indigo-50 border border-primary/15 rounded-2xl overflow-hidden shadow-[0_8px_30px_rgba(99,102,241,0.08)]">

      <!-- Header row -->
      <div class="flex items-center justify-between px-6 py-4 bg-white/60 backdrop-blur-sm border-b border-primary/10">
        <div class="flex items-center gap-3">
          <div class="w-9 h-9 rounded-lg bg-gradient-to-br from-violet-500 to-indigo-600 flex items-center justify-center shadow-md shadow-violet-200">
            <span class="material-symbols-outlined text-white text-[18px]">psychology</span>
          </div>
          <div>
            <p class="text-sm font-bold text-on-surface leading-none">AI Scenario Analyzer</p>
            <p class="text-[10px] text-outline mt-0.5">Quality check before execution</p>
          </div>
        </div>

        <button
          (click)="runAnalysis()"
          [disabled]="loading"
          class="flex items-center gap-2 px-5 py-2 bg-gradient-to-r from-violet-500 to-indigo-600
                 text-white text-xs font-bold rounded-full shadow-lg shadow-violet-200
                 hover:scale-[1.03] active:scale-95 transition-all disabled:opacity-60 disabled:cursor-not-allowed disabled:hover:scale-100">
          <ng-container *ngIf="!loading">
            <span class="material-symbols-outlined text-[15px]">auto_awesome</span>
            Analyze
          </ng-container>
          <ng-container *ngIf="loading">
            <span class="w-3.5 h-3.5 border-2 border-white border-t-transparent rounded-full animate-spin"></span>
            Analyzing…
          </ng-container>
        </button>
      </div>

      <!-- Body (only shown after analysis) -->
      <div *ngIf="result" class="px-6 py-5 space-y-5">

        <!-- ── Quality Badge ──────────────────────────────────── -->
        <div class="flex items-center gap-4">
          <!-- Circular score ring -->
          <div class="relative w-16 h-16 shrink-0">
            <svg class="w-16 h-16 -rotate-90" viewBox="0 0 56 56">
              <circle cx="28" cy="28" r="22" fill="none" stroke="#e5e7eb" stroke-width="5"/>
              <circle cx="28" cy="28" r="22" fill="none"
                [attr.stroke]="scoreFillColor"
                stroke-width="5"
                stroke-linecap="round"
                [attr.stroke-dasharray]="138.2"
                [attr.stroke-dashoffset]="138.2 - (138.2 * result.quality_score / 100)"/>
            </svg>
            <span class="absolute inset-0 flex items-center justify-center text-sm font-extrabold text-on-surface">
              {{ result.quality_score }}
            </span>
          </div>

          <div>
            <div class="flex items-center gap-2 mb-1">
              <span [class]="qualityBadgeClass" class="px-3 py-0.5 rounded-full text-xs font-extrabold uppercase tracking-widest">
                {{ qualityEmoji }} {{ result.quality_label | titlecase }}
              </span>
            </div>
            <p class="text-xs text-outline leading-relaxed">
              <span *ngIf="result.quality_label === 'good'">This scenario is well-formed and testable.</span>
              <span *ngIf="result.quality_label === 'medium'">This scenario needs minor improvements before execution.</span>
              <span *ngIf="result.quality_label === 'poor'">Critical issues found — fix them to avoid false positives.</span>
            </p>
          </div>
        </div>

        <!-- ── Issues List ────────────────────────────────────── -->
        <div *ngIf="result.issues.length > 0">
          <p class="text-[10px] font-extrabold text-outline uppercase tracking-widest mb-2">
            Detected Issues ({{ result.issues.length }})
          </p>
          <div class="space-y-2">
            <div
              *ngFor="let issue of result.issues"
              [class]="issueRowClass(issue)"
              class="flex gap-3 p-3 rounded-xl text-xs leading-snug border">
              <span class="material-symbols-outlined text-[15px] shrink-0 mt-0.5" [class]="issueIconClass(issue)">
                {{ issueIcon(issue) }}
              </span>
              <div>
                <p class="font-semibold">{{ issue.message }}</p>
                <p class="opacity-75 mt-0.5">{{ issue.why }}</p>
                <p *ngIf="issue.step_text" class="mt-1 font-mono text-[10px] opacity-60 italic">
                  "{{ issue.step_text }}"
                </p>
              </div>
            </div>
          </div>
        </div>

        <!-- ── Suggestions ────────────────────────────────────── -->
        <div *ngIf="result.suggestions.length > 0">
          <p class="text-[10px] font-extrabold text-outline uppercase tracking-widest mb-2">Suggestions</p>
          <ul class="space-y-1.5">
            <li *ngFor="let s of result.suggestions" class="flex gap-2 text-xs text-on-surface-variant leading-snug">
              <span class="material-symbols-outlined text-secondary text-[14px] shrink-0 mt-0.5">lightbulb</span>
              {{ s }}
            </li>
          </ul>
        </div>

        <!-- ── Improved Scenario ──────────────────────────────── -->
        <div *ngIf="result.improved_steps.length > 0">
          <div class="flex items-center justify-between mb-2">
            <p class="text-[10px] font-extrabold text-outline uppercase tracking-widest">Improved Scenario</p>
            <button
              (click)="applyImproved()"
              class="flex items-center gap-1 text-[10px] font-extrabold text-secondary hover:text-primary transition-colors">
              <span class="material-symbols-outlined text-[13px]">auto_fix_high</span>
              Apply to editor
            </button>
          </div>
          <div class="bg-slate-900 rounded-xl p-4 font-mono text-xs text-slate-300 space-y-1 overflow-auto max-h-48">
            <div *ngFor="let step of result.improved_steps">
              <span [class]="previewKeywordClass(step.keyword)">{{ step.keyword }}</span>
              <span class="text-slate-300"> {{ step.text }}</span>
            </div>
          </div>
        </div>

        <!-- ── Best Practices ─────────────────────────────────── -->
        <details class="group">
          <summary class="text-[10px] font-extrabold text-outline uppercase tracking-widest cursor-pointer select-none flex items-center gap-1 hover:text-primary transition-colors">
            <span class="material-symbols-outlined text-[13px] transition-transform group-open:rotate-90">chevron_right</span>
            Best Practices
          </summary>
          <ul class="mt-2 space-y-1.5 pl-4">
            <li *ngFor="let bp of result.best_practices" class="flex gap-2 text-xs text-on-surface-variant leading-snug">
              <span class="text-primary/60">•</span>
              {{ bp }}
            </li>
          </ul>
        </details>
      </div>

      <!-- Idle state (no analysis yet) -->
      <div *ngIf="!result && !loading" class="px-6 py-5 text-center">
        <span class="material-symbols-outlined text-outline-variant text-4xl mb-2 block">search</span>
        <p class="text-xs text-outline-variant">Click <strong>Analyze</strong> to check your scenario for quality issues and get improvement suggestions.</p>
      </div>
    </div>
  `,
})
export class AiScenarioAnalyzerComponent {
  @Input() steps: { keyword: string; text: string }[] = [];
  @Input() scenarioName = '';
  @Input() language = 'en';

  /** Emitted when the user clicks "Apply to editor" with the improved steps. */
  @Output() improvedStepsApplied = new EventEmitter<{ keyword: string; text: string }[]>();

  private nlpService = inject(NlpService);

  loading = false;
  result: ScenarioQualityResult | null = null;

  runAnalysis(): void {
    const nonEmpty = this.steps.filter(s => s.text.trim());
    this.loading = true;
    this.nlpService
      .analyzeQuality(this.scenarioName, nonEmpty, this.language)
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: r => (this.result = r),
        error: () => (this.result = null),
      });
  }

  applyImproved(): void {
    if (this.result?.improved_steps?.length) {
      this.improvedStepsApplied.emit(this.result.improved_steps);
    }
  }

  get scoreFillColor(): string {
    if (!this.result) return '#6366f1';
    if (this.result.quality_score >= 75) return '#22c55e';
    if (this.result.quality_score >= 45) return '#f59e0b';
    return '#ef4444';
  }

  get qualityEmoji(): string {
    const l = this.result?.quality_label;
    if (l === 'good') return '✅';
    if (l === 'medium') return '⚠️';
    return '❌';
  }

  get qualityBadgeClass(): string {
    const l = this.result?.quality_label;
    if (l === 'good') return 'bg-emerald-100 text-emerald-700';
    if (l === 'medium') return 'bg-amber-100 text-amber-700';
    return 'bg-red-100 text-red-700';
  }

  issueRowClass(issue: QualityIssue): string {
    if (issue.severity === 'error') return 'bg-red-50 border-red-200 text-red-800';
    if (issue.severity === 'warning') return 'bg-amber-50 border-amber-200 text-amber-800';
    return 'bg-blue-50 border-blue-200 text-blue-800';
  }

  issueIconClass(issue: QualityIssue): string {
    if (issue.severity === 'error') return 'text-red-500';
    if (issue.severity === 'warning') return 'text-amber-500';
    return 'text-blue-400';
  }

  issueIcon(issue: QualityIssue): string {
    if (issue.severity === 'error') return 'error';
    if (issue.severity === 'warning') return 'warning';
    return 'info';
  }

  previewKeywordClass(kw: string): string {
    const k = kw.toUpperCase();
    if (k === 'GIVEN') return 'text-blue-400 font-bold';
    if (k === 'WHEN') return 'text-purple-400 font-bold';
    if (k === 'THEN') return 'text-emerald-400 font-bold';
    return 'text-slate-400 font-bold';
  }
}
