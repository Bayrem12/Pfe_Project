import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subject, takeUntil } from 'rxjs';
import { ConfirmService, ConfirmState } from '../../../core/services/confirm.service';
import { TranslatePipe } from '../../pipes/translate.pipe';

/**
 * Mounted once at the application root. Renders the confirmation dialog
 * driven by `ConfirmService`. Any page can trigger it via `confirmService.open(...)`.
 */
@Component({
  selector: 'app-confirm-host',
  standalone: true,
  imports: [CommonModule, TranslatePipe],
  template: `
    <div
      *ngIf="state.visible"
      class="fixed inset-0 z-[95] flex items-center justify-center bg-slate-950/40 p-4"
      style="animation: confirmHostFadeIn 180ms ease-out"
      (click)="onCancel()"
    >
      <div
        class="w-full max-w-sm rounded-2xl border border-slate-200 bg-white shadow-xl"
        style="animation: confirmHostSlideUp 180ms ease-out"
        (click)="$event.stopPropagation()"
      >
        <div class="px-5 py-5 sm:px-6 sm:py-6">
          <div class="flex items-start gap-3 mb-3">
            <div
              class="flex-shrink-0 flex items-center justify-center w-10 h-10 rounded-full border"
              [class.bg-red-50]="state.variant === 'danger'"
              [class.border-red-100]="state.variant === 'danger'"
              [class.bg-primary-container]="state.variant !== 'danger'"
              [class.border-primary]="state.variant !== 'danger'"
            >
              <span
                class="material-symbols-outlined text-xl"
                [class.text-red-500]="state.variant === 'danger'"
                [class.text-primary]="state.variant !== 'danger'"
              >
                {{ state.variant === 'danger' ? 'delete' : 'help' }}
              </span>
            </div>
            <div class="flex-1 min-w-0">
              <h3 class="text-base font-bold tracking-tight text-slate-900 break-words">{{ state.title }}</h3>
              <p *ngIf="state.description" class="mt-1 text-sm text-slate-500 break-words">{{ state.description }}</p>
            </div>
          </div>

          <div *ngIf="state.accentText" class="mt-2">
            <span
              class="inline-flex items-center gap-2 rounded-full border px-3 py-1.5 text-sm font-semibold"
              [style.borderColor]="(state.accentColor || '#6366F1') + '33'"
              [style.backgroundColor]="(state.accentColor || '#6366F1') + '14'"
              [style.color]="state.accentColor || '#6366F1'"
            >
              <span class="h-2.5 w-2.5 rounded-full shadow-sm" [style.backgroundColor]="state.accentColor || '#6366F1'"></span>
              <span class="truncate max-w-[200px]">{{ state.accentText }}</span>
            </span>
          </div>

          <div class="mt-6 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
            <button
              type="button"
              class="rounded-xl border border-slate-200 px-4 py-2.5 text-sm font-semibold text-slate-600 transition-colors hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50"
              [disabled]="state.loading"
              (click)="onCancel()"
            >
              {{ state.cancelLabel || ('delete.dialog.cancel' | translate) }}
            </button>

            <button
              type="button"
              class="inline-flex items-center justify-center gap-2 rounded-xl px-4 py-2.5 text-sm font-semibold text-white transition-colors disabled:cursor-not-allowed disabled:opacity-60"
              [class.bg-primary]="state.variant !== 'danger'"
              [class.hover:bg-primary]="state.variant !== 'danger'"
              [class.bg-red-600]="state.variant === 'danger'"
              [class.hover:bg-red-700]="state.variant === 'danger'"
              [disabled]="state.loading"
              (click)="onConfirm()"
            >
              <span *ngIf="!state.loading" class="material-symbols-outlined text-lg">
                {{ state.variant === 'danger' ? 'delete' : 'check' }}
              </span>
              <span *ngIf="state.loading" class="inline-block h-4 w-4 rounded-full border-2 border-white/30 border-t-white animate-spin"></span>
              {{ state.loading ? ('delete.dialog.deleting' | translate) : (state.confirmLabel || ('delete.dialog.confirm' | translate)) }}
            </button>
          </div>
        </div>
      </div>
    </div>

    <style>
      @keyframes confirmHostFadeIn {
        from { opacity: 0; }
        to   { opacity: 1; }
      }
      @keyframes confirmHostSlideUp {
        from { opacity: 0; transform: translateY(12px) scale(0.97); }
        to   { opacity: 1; transform: translateY(0)    scale(1); }
      }
    </style>
  `,
})
export class ConfirmHostComponent implements OnInit, OnDestroy {
  state: ConfirmState = {
    visible: false,
    loading: false,
    title: '',
    description: '',
    confirmLabel: '',
    cancelLabel: '',
    variant: 'danger',
  };

  private readonly destroy$ = new Subject<void>();

  constructor(private confirmService: ConfirmService) {}

  ngOnInit(): void {
    this.confirmService.state$
      .pipe(takeUntil(this.destroy$))
      .subscribe((s) => (this.state = s));
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onConfirm(): void {
    this.confirmService.accept();
  }

  onCancel(): void {
    this.confirmService.dismiss();
  }
}
