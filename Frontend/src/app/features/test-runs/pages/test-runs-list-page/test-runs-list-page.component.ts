import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { Subject, interval } from 'rxjs';
import { takeUntil, distinctUntilChanged, switchMap, map, debounceTime } from 'rxjs/operators';

import { BreadcrumbService } from '../../../../core/services/breadcrumb.service';
import { TestRunService } from '../../../../core/services/test-run.service';
import { RunNotificationsService } from '../../../../core/services/run-notifications.service';
import { TestRunListItem } from '../../../../core/models/test-run.model';
import { TranslatePipe } from '../../../../shared/pipes/translate.pipe';
import { TranslationService } from '../../../../core/services/translation.service';

@Component({
  selector: 'app-test-runs-list-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, TranslatePipe],
  templateUrl: './test-runs-list-page.component.html'
})
export class TestRunsListPageComponent implements OnInit, OnDestroy {
  runs: TestRunListItem[] = [];
  loading = false;
  errorMessage = '';

  searchTerm = '';
  selectedStatus = '';

  currentPage = 1;
  pageSize = 20;
  total = 0;
  stoppingRunIds = new Set<string>();

  get statusOptions() {
    return [
      { value: '', label: this.t('testRun.list.status.all') },
      { value: 'Pending', label: this.t('testRun.list.status.pending') },
      { value: 'Running', label: this.t('testRun.list.status.running') },
      { value: 'Completed', label: this.t('testRun.list.status.completed') },
      { value: 'Failed', label: this.t('testRun.list.status.failed') },
      { value: 'Cancelled', label: this.t('testRun.list.status.cancelled') }
    ];
  }

  getStatusLabel(status: string): string {
    const normalized = (status || '').toLowerCase();
    const map: Record<string, string> = {
      pending: 'testRun.list.status.pending',
      running: 'testRun.list.status.running',
      completed: 'testRun.list.status.completed',
      failed: 'testRun.list.status.failed',
      cancelled: 'testRun.list.status.cancelled'
    };
    return map[normalized] ? this.t(map[normalized]) : (status || '');
  }

  private destroy$ = new Subject<void>();
  private runNotifications = inject(RunNotificationsService);

  constructor(
    private testRunService: TestRunService,
    private breadcrumbService: BreadcrumbService,
    private translationService: TranslationService,
    private router: Router
  ) {}

  private t(key: string, ...args: string[]): string {
    return this.translationService.t(key, ...args);
  }

  ngOnInit(): void {
    this.breadcrumbService.setMultiple([{ label: 'Test Runs' }]);
    this.loadRuns();

    // Refresh silently whenever a notification is added or changes state
    // (e.g. a new run starts → immediately fetch so it appears as "Running").
    this.runNotifications.runs$.pipe(
      map(runs => runs.map(r => `${r.id}:${r.state}`).join(',')),
      distinctUntilChanged(),
      debounceTime(400),
      takeUntil(this.destroy$),
    ).subscribe(() => this.silentRefresh());

    // While at least one run is executing, poll every 5 s so the row
    // switches from "Running" → "Completed" / "Failed" automatically.
    this.runNotifications.runs$.pipe(
      map(runs => runs.some(r => r.state === 'running')),
      distinctUntilChanged(),
      switchMap(hasRunning => hasRunning ? interval(5000) : []),
      takeUntil(this.destroy$),
    ).subscribe(() => this.silentRefresh());
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.breadcrumbService.clear();
  }

  loadRuns(): void {
    this.loading = true;
    this.errorMessage = '';

    this.testRunService.getTestRuns({
      page: this.currentPage,
      pageSize: this.pageSize,
      search: this.searchTerm.trim() || undefined,
      status: this.selectedStatus || undefined
    })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          const payload = response?.resultat;
          this.runs = payload?.items ?? [];
          this.total = payload?.total ?? 0;
          this.loading = false;
        },
        error: (error) => {
          this.errorMessage =
            error?.error?.fail_Messages ||
            error?.error?.Fail_Messages ||
            'Unable to load test runs.';
          this.runs = [];
          this.total = 0;
          this.loading = false;
        }
      });
  }

  /** Background refresh — no loading spinner, preserves current scroll/filters. */
  private silentRefresh(): void {
    this.testRunService.getTestRuns({
      page: this.currentPage,
      pageSize: this.pageSize,
      search: this.searchTerm.trim() || undefined,
      status: this.selectedStatus || undefined
    })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          const payload = response?.resultat;
          this.runs = payload?.items ?? [];
          this.total = payload?.total ?? 0;
        },
      });
  }

  applyFilters(): void {
    this.currentPage = 1;
    this.loadRuns();
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.selectedStatus = '';
    this.currentPage = 1;
    this.loadRuns();
  }

  setStatusFilter(value: string): void {
    this.selectedStatus = value;
    this.currentPage = 1;
    this.loadRuns();
  }

  openRun(run: TestRunListItem): void {
    this.router.navigate(['/test-runs', run.id]);
  }

  canStopRun(run: TestRunListItem): boolean {
    return (run.status || '').toLowerCase() === 'running';
  }

  isStoppingRun(runId: string): boolean {
    return this.stoppingRunIds.has(runId);
  }

  stopRun(run: TestRunListItem, event: Event): void {
    event.stopPropagation();

    if (!this.canStopRun(run) || this.isStoppingRun(run.id)) {
      return;
    }

    this.stoppingRunIds.add(run.id);
    this.errorMessage = '';

    this.testRunService.cancelTestRun(run.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.runs = this.runs.map(item => item.id === run.id ? { ...item, status: 'Cancelled' } : item);
          this.stoppingRunIds.delete(run.id);
          this.silentRefresh();
        },
        error: (error) => {
          this.errorMessage =
            error?.error?.fail_Messages ||
            error?.error?.Fail_Messages ||
            this.t('testRun.list.stopFailed');
          this.stoppingRunIds.delete(run.id);
        }
      });
  }

  changePage(page: number): void {
    if (page < 1 || page > this.totalPages || page === this.currentPage) {
      return;
    }

    this.currentPage = page;
    this.loadRuns();
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.total / this.pageSize));
  }

  getStartIndex(): number {
    if (!this.total) return 0;
    return (this.currentPage - 1) * this.pageSize + 1;
  }

  getEndIndex(): number {
    return Math.min(this.currentPage * this.pageSize, this.total);
  }

  getStatusClass(status: string): string {
    const normalized = (status || '').toLowerCase();

    if (normalized === 'completed') {
      return 'inline-flex items-center rounded-full bg-emerald-100 px-3 py-1 text-[10px] font-black uppercase tracking-widest text-emerald-700';
    }

    if (normalized === 'running') {
      return 'inline-flex items-center rounded-full bg-blue-100 px-3 py-1 text-[10px] font-black uppercase tracking-widest text-blue-700';
    }

    if (normalized === 'failed') {
      return 'inline-flex items-center rounded-full bg-red-100 px-3 py-1 text-[10px] font-black uppercase tracking-widest text-red-700';
    }

    if (normalized === 'cancelled') {
      return 'inline-flex items-center rounded-full bg-slate-200 px-3 py-1 text-[10px] font-black uppercase tracking-widest text-slate-700';
    }

    return 'inline-flex items-center rounded-full bg-amber-100 px-3 py-1 text-[10px] font-black uppercase tracking-widest text-amber-700';
  }

  getPassRateClass(passRate: number): string {
    if (passRate >= 90) return 'text-emerald-700';
    if (passRate >= 70) return 'text-amber-700';
    return 'text-red-700';
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

  get avgPassRate(): number {
    if (!this.runs.length) return 0;
    return this.runs.reduce((sum, r) => sum + (r.passRate ?? 0), 0) / this.runs.length;
  }

  getStatusChipActiveClass(value: string): string {
    const normalized = (value || '').toLowerCase();
    if (normalized === 'completed') return 'bg-emerald-100 text-emerald-700 border border-emerald-200';
    if (normalized === 'running') return 'bg-blue-100 text-blue-700 border border-blue-200';
    if (normalized === 'failed') return 'bg-red-100 text-red-700 border border-red-200';
    if (normalized === 'cancelled') return 'bg-slate-200 text-slate-700 border border-slate-300';
    if (normalized === 'pending') return 'bg-amber-100 text-amber-700 border border-amber-200';
    // "All Statuses" (empty value)
    return 'bg-slate-900 text-white border border-slate-900';
  }
}
