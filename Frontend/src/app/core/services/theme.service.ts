import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export type ThemeMode = 'light' | 'dark';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly STORAGE_KEY = 'pfe_theme';

  private _theme$ = new BehaviorSubject<ThemeMode>(this.loadSavedTheme());
  readonly theme$ = this._theme$.asObservable();

  get current(): ThemeMode {
    return this._theme$.value;
  }

  /** Call once at app startup to apply the persisted theme. */
  init(): void {
    this.apply(this._theme$.value);
  }

  set(theme: ThemeMode): void {
    if (this._theme$.value === theme) return;
    this._theme$.next(theme);
    this.apply(theme);
    try {
      localStorage.setItem(this.STORAGE_KEY, theme);
    } catch (_) {}
  }

  toggle(): void {
    this.set(this.current === 'dark' ? 'light' : 'dark');
  }

  private apply(theme: ThemeMode): void {
    const root = document.documentElement;
    if (theme === 'dark') {
      root.classList.add('dark');
    } else {
      root.classList.remove('dark');
    }
  }

  private loadSavedTheme(): ThemeMode {
    try {
      const saved = localStorage.getItem(this.STORAGE_KEY);
      if (saved === 'dark' || saved === 'light') return saved;
    } catch (_) {}
    return 'light';
  }
}
