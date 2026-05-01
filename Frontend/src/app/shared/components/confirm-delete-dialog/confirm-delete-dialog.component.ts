import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslatePipe } from '../../pipes/translate.pipe';

@Component({
  selector: 'app-confirm-delete-dialog',
  standalone: true,
  imports: [CommonModule, TranslatePipe],
  template: `
    <div
      *ngIf="visible"
      class="fixed inset-0 z-[90] flex items-center justify-center bg-slate-950/40 p-4"
      style="animation: confirmDeleteFadeIn 180ms ease-out"
      (click)="onCancel()"
    >
      <div
        class="w-full max-w-sm rounded-2xl border border-slate-200 bg-white shadow-xl"
        style="animation: confirmDeleteSlideUp 180ms ease-out"
        (click)="$event.stopPropagation()"
      >
        <div class="px-5 py-5 sm:px-6 sm:py-6">
          <!-- Icon + Title -->
          <div class="flex items-start gap-3 mb-4">
            <div class="flex-shrink-0 flex items-center justify-center w-10 h-10 rounded-full bg-red-50 border border-red-100">
              <span class="material-symbols-outlined text-red-500 text-xl">delete</span>
            </div>
            <div>
              <h3 class="text-base font-bold tracking-tight text-slate-900">{{ title | translate }}</h3>
              <p *ngIf="description" class="mt-1 text-sm text-slate-500">{{ description | translate }}</p>
            </div>
          </div>

          <!-- Slot for extra content (e.g. colored tag chip) -->
          <ng-content></ng-content>

          <div class="mt-6 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
            <button
              type="button"
              class="rounded-xl border border-slate-200 px-4 py-2.5 text-sm font-semibold text-slate-600 transition-colors hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50"
              [disabled]="loading"
              (click)="onCancel()"
            >
              {{ cancelLabel | translate }}
            </button>

            <button
              type="button"
              class="inline-flex items-center justify-center gap-2 rounded-xl bg-primary px-4 py-2.5 text-sm font-semibold text-white transition-colors hover:bg-primary/90 disabled:cursor-not-allowed disabled:opacity-60"
              [disabled]="loading"
              (click)="onConfirm()"
            >
              <span *ngIf="!loading" class="material-symbols-outlined text-lg">delete</span>
              <span *ngIf="loading" class="inline-block h-4 w-4 rounded-full border-2 border-white/30 border-t-white animate-spin"></span>
              {{ loading ? ('delete.dialog.deleting' | translate) : (confirmLabel | translate) }}
            </button>
          </div>
        </div>
      </div>
    </div>

    <style>
      @keyframes confirmDeleteFadeIn {
        from { opacity: 0; }
        to   { opacity: 1; }
      }
      @keyframes confirmDeleteSlideUp {
        from { opacity: 0; transform: translateY(12px) scale(0.97); }
        to   { opacity: 1; transform: translateY(0)    scale(1); }
      }
    </style>
  `
})
export class ConfirmDeleteDialogComponent {
  @Input() visible = false;
  @Input() title = 'delete.dialog.defaultTitle';
  @Input() description = 'delete.dialog.defaultDesc';
  @Input() confirmLabel = 'delete.dialog.confirm';
  @Input() cancelLabel = 'delete.dialog.cancel';
  @Input() loading = false;

  @Output() confirmed = new EventEmitter<void>();
  @Output() cancelled = new EventEmitter<void>();

  onConfirm(): void {
    this.confirmed.emit();
  }

  onCancel(): void {
    this.cancelled.emit();
  }
}
