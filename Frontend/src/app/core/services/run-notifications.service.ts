import { Injectable, inject } from '@angular/core';
import { BehaviorSubject, Observable, finalize, switchMap, catchError, of, map } from 'rxjs';
import { IaAgentService, AiRunRequest, AiRunSuiteRequest } from './ia-agent.service';
import { TestRunService } from './test-run.service';

export type RunKind = 'scenario' | 'suite';
export type RunState = 'running' | 'passed' | 'failed';

export interface RunNotification {
  id: string;                  // local uuid
  kind: RunKind;
  title: string;               // scenario title or suite name
  subtitle?: string;           // e.g. project / feature
  state: RunState;
  startedAt: Date;
  completedAt?: Date;
  durationMs?: number;
  executionId?: string;        // populated on completion
  errorMessage?: string;
  read: boolean;               // user has seen the toast/dropdown entry
  isHeadless: boolean;
}

/**
 * Tracks AI test runs (single scenarios + suites) initiated from anywhere in
 * the app. Runs continue executing while the user navigates away. The topbar
 * notification dropdown subscribes to this service to show live status and
 * surfaces a completion toast.
 */
@Injectable({ providedIn: 'root' })
export class RunNotificationsService {
  private iaAgentService = inject(IaAgentService);
  private testRunService = inject(TestRunService);

  private readonly _runs$ = new BehaviorSubject<RunNotification[]>([]);
  readonly runs$: Observable<RunNotification[]> = this._runs$.asObservable();

  /** Current snapshot of all known runs (newest first). */
  get runs(): RunNotification[] {
    return this._runs$.value;
  }

  /** Number of currently running tests (scenarios + suites). */
  get runningCount(): number {
    return this._runs$.value.filter(r => r.state === 'running').length;
  }

  /** Number of completed runs the user has not yet acknowledged. */
  get unreadCount(): number {
    return this._runs$.value.filter(r => r.state !== 'running' && !r.read).length;
  }

  /** Combined badge count shown on the topbar bell. */
  get totalBadgeCount(): number {
    return this.runningCount + this.unreadCount;
  }

  /**
   * Trigger a scenario run, register it in the notification list and return
   * the underlying observable. The run continues even if the caller navigates
   * away or unsubscribes — the service keeps a live subscription.
   */
  runScenario(req: AiRunRequest, title: string, subtitle?: string): RunNotification {
    const notification = this._addRun({
      kind: 'scenario',
      title,
      subtitle,
      isHeadless: req.isHeadless ?? true,
    });

    this.iaAgentService.runScenario(req)
      .pipe(
        switchMap(result =>
          this.testRunService.getTestRunDetail(result.executionId).pipe(
            map(resp => ({ result, detail: resp?.resultat })),
            catchError(() => of({ result, detail: null as any })),
          )
        ),
        finalize(() => this._emit()),
      )
      .subscribe({
        next: ({ result, detail }) => {
          const success = !detail || detail.failedTests === 0;
          this._completeRun(notification.id, success, result.executionId);
        },
        error: (err) => this._completeRun(
          notification.id, false, undefined,
          err?.error?.fail_Messages || err?.message || 'Run failed.'
        ),
      });

    return notification;
  }

  /** Trigger a test-suite run with the same fire-and-forget semantics as runScenario. */
  runSuite(req: AiRunSuiteRequest, title: string, subtitle?: string): RunNotification {
    const notification = this._addRun({
      kind: 'suite',
      title,
      subtitle,
      isHeadless: req.isHeadless ?? true,
    });

    this.iaAgentService.runTestSuite(req)
      .pipe(
        switchMap(result =>
          this.testRunService.getTestRunDetail(result.executionId).pipe(
            map(resp => ({ result, detail: resp?.resultat })),
            catchError(() => of({ result, detail: null as any })),
          )
        ),
        finalize(() => this._emit()),
      )
      .subscribe({
        next: ({ result, detail }) => {
          const success = !detail || detail.failedTests === 0;
          this._completeRun(notification.id, success, result.executionId);
        },
        error: (err) => this._completeRun(
          notification.id, false, undefined,
          err?.error?.fail_Messages || err?.message || 'Run failed.'
        ),
      });

    return notification;
  }

  /** Mark a single notification as read (user opened it / clicked through). */
  markAsRead(id: string): void {
    const list = this._runs$.value.map(r => r.id === id ? { ...r, read: true } : r);
    this._runs$.next(list);
  }

  /** Mark every notification as read (e.g. user opened the dropdown). */
  markAllAsRead(): void {
    this._runs$.next(this._runs$.value.map(r => ({ ...r, read: true })));
  }

  /** Remove a notification entirely. Running notifications are not dismissable. */
  dismiss(id: string): void {
    const list = this._runs$.value.filter(r => r.id !== id || r.state === 'running');
    this._runs$.next(list);
  }

  /** Clear all completed runs. Running ones stay. */
  clearCompleted(): void {
    this._runs$.next(this._runs$.value.filter(r => r.state === 'running'));
  }

  // ── internal helpers ───────────────────────────────────────────────────────

  private _addRun(partial: Pick<RunNotification, 'kind' | 'title' | 'subtitle' | 'isHeadless'>): RunNotification {
    const notif: RunNotification = {
      id: `run_${Date.now()}_${Math.random().toString(36).slice(2, 8)}`,
      kind: partial.kind,
      title: partial.title,
      subtitle: partial.subtitle,
      state: 'running',
      startedAt: new Date(),
      read: false,
      isHeadless: partial.isHeadless,
    };
    this._runs$.next([notif, ...this._runs$.value]);
    return notif;
  }

  private _completeRun(
    id: string,
    success: boolean,
    executionId?: string,
    errorMessage?: string,
  ): void {
    const list = this._runs$.value.map(r => {
      if (r.id !== id) return r;
      const completedAt = new Date();
      return {
        ...r,
        state: (success ? 'passed' : 'failed') as RunState,
        completedAt,
        durationMs: completedAt.getTime() - r.startedAt.getTime(),
        executionId,
        errorMessage,
        read: false, // reset to unread so badge updates
      };
    });
    this._runs$.next(list);
  }

  private _emit(): void {
    // No-op finalize hook — we already update state in next/error callbacks.
    // Kept for future extensibility (e.g. analytics).
  }
}
