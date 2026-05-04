import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export interface ConfirmOptions {
  /** Title shown in the dialog. Plain text (already translated). */
  title: string;
  /** Optional description below the title. Plain text (already translated). */
  description?: string;
  /** Label for the confirm button. Defaults to 'Delete' (translated). */
  confirmLabel?: string;
  /** Label for the cancel button. Defaults to 'Cancel' (translated). */
  cancelLabel?: string;
  /** Optional accent chip text shown above the action buttons. */
  accentText?: string;
  /** Optional accent chip color (any CSS color). */
  accentColor?: string;
  /** Variant: 'danger' (red) | 'primary' (blue). Defaults to 'danger'. */
  variant?: 'danger' | 'primary';
}

export interface ConfirmState extends Required<Omit<ConfirmOptions, 'accentText' | 'accentColor'>> {
  visible: boolean;
  loading: boolean;
  accentText?: string;
  accentColor?: string;
}

const INITIAL: ConfirmState = {
  visible: false,
  loading: false,
  title: '',
  description: '',
  confirmLabel: '',
  cancelLabel: '',
  variant: 'danger',
};

/**
 * Global confirmation dialog service.
 *
 * Usage:
 *   const ok = await this.confirmService.open({ title: 'Delete project?', description: '...' });
 *   if (!ok) return;
 *
 * The host component (`<app-confirm-host>`) is mounted once in the root layout
 * so any feature page can call `open()` without wiring its own modal.
 */
@Injectable({ providedIn: 'root' })
export class ConfirmService {
  private readonly _state = new BehaviorSubject<ConfirmState>(INITIAL);
  readonly state$ = this._state.asObservable();

  private resolver: ((value: boolean) => void) | null = null;

  open(opts: ConfirmOptions): Promise<boolean> {
    // If a previous dialog is still open, resolve it as cancelled first.
    if (this.resolver) {
      this.resolver(false);
      this.resolver = null;
    }

    this._state.next({
      visible: true,
      loading: false,
      title: opts.title || 'Are you sure?',
      description: opts.description || '',
      confirmLabel: opts.confirmLabel || '',
      cancelLabel: opts.cancelLabel || '',
      accentText: opts.accentText,
      accentColor: opts.accentColor,
      variant: opts.variant || 'danger',
    });

    return new Promise<boolean>((resolve) => {
      this.resolver = resolve;
    });
  }

  /** Called by the host component when the user clicks the confirm button. */
  accept(): void {
    if (this.resolver) {
      this.resolver(true);
      this.resolver = null;
    }
    this._state.next({ ...this._state.value, visible: false, loading: false });
  }

  /** Called by the host component when the user dismisses the dialog. */
  dismiss(): void {
    if (this.resolver) {
      this.resolver(false);
      this.resolver = null;
    }
    this._state.next({ ...this._state.value, visible: false, loading: false });
  }
}
