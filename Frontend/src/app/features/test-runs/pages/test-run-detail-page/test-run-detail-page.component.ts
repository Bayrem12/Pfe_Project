import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { Subject } from 'rxjs';
import { switchMap, takeUntil } from 'rxjs/operators';
import { TranslatePipe } from '../../../../shared/pipes/translate.pipe';

import { BreadcrumbService } from '../../../../core/services/breadcrumb.service';
import { TestRunService } from '../../../../core/services/test-run.service';
import { TestRunDetail, TestRunScenarioResult, TestRunStepResult } from '../../../../core/models/test-run.model';
import { environment } from '../../../../../environments/environment';

@Component({
  selector: 'app-test-run-detail-page',
  standalone: true,
  imports: [CommonModule, RouterModule, TranslatePipe],
  templateUrl: './test-run-detail-page.component.html'
})
export class TestRunDetailPageComponent implements OnInit, OnDestroy {
  run: TestRunDetail | null = null;
  loading = false;
  errorMessage = '';

  expandedResults = new Set<string>();
  activeTab: 'overview' | 'scenarios' = 'overview';

  private destroy$ = new Subject<void>();

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private testRunService: TestRunService,
    private breadcrumbService: BreadcrumbService
  ) {}


  ngOnInit(): void {
    this.route.paramMap
      .pipe(
        takeUntil(this.destroy$),
        switchMap(params => {
          const runId = params.get('id') || '';
          this.loading = true;
          this.errorMessage = '';
          this.run = null;
          this.expandedResults.clear();
          return this.testRunService.getTestRunDetail(runId);
        })
      )
      .subscribe({
        next: (response) => {
          this.run = response?.resultat ?? null;
          this.loading = false;

          if (this.run) {
            this.breadcrumbService.setMultiple([
              { label: 'Test Runs', url: '/test-runs' },
              { label: this.run.runLabel }
            ]);
          }
        },
        error: (error) => {
          this.errorMessage =
            error?.error?.fail_Messages ||
            error?.error?.Fail_Messages ||
            'Unable to load test run details.';
          this.loading = false;
        }
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.breadcrumbService.clear();
  }

  goBack(): void {
    this.router.navigate(['/test-runs']);
  }

  toggleResult(resultId: string): void {
    if (this.expandedResults.has(resultId)) {
      this.expandedResults.delete(resultId);
      return;
    }

    this.expandedResults.add(resultId);
  }

  isExpanded(resultId: string): boolean {
    return this.expandedResults.has(resultId);
  }

  getStatusClass(status: string): string {
    const normalized = (status || '').toLowerCase();

    if (normalized === 'completed' || normalized === 'passed') {
      return 'inline-flex items-center rounded-full bg-emerald-100 px-3 py-1 text-[10px] font-black uppercase tracking-widest text-emerald-700';
    }

    if (normalized === 'running') {
      return 'inline-flex items-center rounded-full bg-blue-100 px-3 py-1 text-[10px] font-black uppercase tracking-widest text-blue-700';
    }

    if (normalized === 'failed' || normalized === 'error') {
      return 'inline-flex items-center rounded-full bg-red-100 px-3 py-1 text-[10px] font-black uppercase tracking-widest text-red-700';
    }

    if (normalized === 'skipped' || normalized === 'cancelled') {
      return 'inline-flex items-center rounded-full bg-slate-200 px-3 py-1 text-[10px] font-black uppercase tracking-widest text-slate-700';
    }

    return 'inline-flex items-center rounded-full bg-amber-100 px-3 py-1 text-[10px] font-black uppercase tracking-widest text-amber-700';
  }

  formatDuration(seconds: number | null): string {
    if (seconds === null || Number.isNaN(seconds)) {
      return '—';
    }

    if (seconds < 60) {
      return `${Math.round(seconds)}s`;
    }

    const mins = Math.floor(seconds / 60);
    const secs = Math.round(seconds % 60);
    return `${mins}m ${secs}s`;
  }

  getPassRateClass(passRate: number): string {
    if (passRate >= 90) return 'text-emerald-700';
    if (passRate >= 70) return 'text-amber-700';
    return 'text-red-700';
  }

  trackByResultId(_: number, result: TestRunScenarioResult): string {
    return result.id;
  }

  isAiRun(run: TestRunDetail): boolean {
    return run.logs?.some(l => l.message?.startsWith('AI Agent')) ?? false;
  }

  /** Color helper for the hero header background based on run outcome. */
  heroAccentClass(status: string): string {
    const s = (status || '').toLowerCase();
    if (s === 'completed' || s === 'passed') return 'from-emerald-500/10 to-emerald-500/0 border-emerald-200';
    if (s === 'failed' || s === 'error') return 'from-rose-500/10 to-rose-500/0 border-rose-200';
    if (s === 'running') return 'from-indigo-500/10 to-indigo-500/0 border-indigo-200';
    return 'from-slate-500/10 to-slate-500/0 border-slate-200';
  }

  heroIcon(status: string): string {
    const s = (status || '').toLowerCase();
    if (s === 'completed' || s === 'passed') return 'verified';
    if (s === 'failed' || s === 'error') return 'error';
    if (s === 'running') return 'progress_activity';
    return 'help';
  }

  heroIconColor(status: string): string {
    const s = (status || '').toLowerCase();
    if (s === 'completed' || s === 'passed') return 'text-emerald-600';
    if (s === 'failed' || s === 'error') return 'text-rose-600';
    if (s === 'running') return 'text-indigo-600';
    return 'text-slate-500';
  }

  // ── Overview analytics helpers ────────────────────────────────────────

  get maxScenarioDuration(): number {
    const durations = (this.run?.testResults ?? []).map(r => r.durationSeconds ?? 0);
    return Math.max(1, ...durations);
  }

  get failedStepsFlat(): Array<TestRunStepResult & { scenarioName: string }> {
    return (this.run?.testResults ?? []).flatMap(r =>
      (r.stepResults ?? [])
        .filter(s => (s.status || '').toLowerCase() === 'failed')
        .map(s => ({ ...s, scenarioName: r.scenarioName }))
    );
  }

  getPassedStepCount(result: TestRunScenarioResult): number {
    return result.stepResults.filter(s => (s.status || '').toLowerCase() === 'passed').length;
  }

  getFailedStepCount(result: TestRunScenarioResult): number {
    return result.stepResults.filter(s => (s.status || '').toLowerCase() === 'failed').length;
  }

  /**
   * Converts a screenshot filePath (which may be a full URL or a legacy local
   * path like "reports/screenshots/xxx.png") to a publicly-accessible URL so
   * an <img> tag can load it from the IA agent's static /reports mount.
   */
  screenshotUrl(filePath: string | null | undefined): string {
    if (!filePath) return '';
    if (filePath.startsWith('http://') || filePath.startsWith('https://')) {
      return filePath;
    }
    // Legacy local path — convert using the configured agent base URL.
    const base = environment.iaAgentUrl.replace(/\/$/, '');
    const rel = filePath.replace(/\\/g, '/').replace(/^\//, '');
    return rel.startsWith('reports/') ? `${base}/${rel}` : `${base}/reports/${rel}`;
  }

  /** URL of the rich vision-aware "Technical Trace" report (second Report row). */
  get technicalTraceUrl(): string | null {
    const reports = this.run?.reports ?? [];
    const tt = reports.find(r => (r.url || '').includes('technical_trace_'));
    return tt?.url ?? null;
  }

  /** Primary AI report URL excluding the technical trace (so cards don't duplicate). */
  get primaryReportUrl(): string | null {
    if (!this.run) return null;
    if (this.run.reportUrl && !this.run.reportUrl.includes('technical_trace_')) {
      return this.run.reportUrl;
    }
    const reports = this.run.reports ?? [];
    const r = reports.find(x => !((x.url || '').includes('technical_trace_')));
    return r?.url ?? null;
  }
}
