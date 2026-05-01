import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable, map } from 'rxjs';
import { en } from '../i18n/en';
import { fr } from '../i18n/fr';

export type Language = 'en' | 'fr';

const STORAGE_KEY = 'app_language';
const TRANSLATIONS: Record<Language, Record<string, string>> = { en, fr };

@Injectable({ providedIn: 'root' })
export class TranslationService {
  private readonly _lang$ = new BehaviorSubject<Language>(this._initial());

  /** Observable of the current language code */
  readonly lang$: Observable<Language> = this._lang$.asObservable();

  /** Observable that emits a translate function — use with the `translate` pipe or `async` pipe */
  readonly t$: Observable<(key: string, ...args: string[]) => string> = this._lang$.pipe(
    map(lang => (key: string, ...args: string[]) => this._resolve(lang, key, args))
  );

  get current(): Language { return this._lang$.value; }

  setLanguage(lang: Language): void {
    localStorage.setItem(STORAGE_KEY, lang);
    this._lang$.next(lang);
  }

  /** Instant translate — useful in component .ts files */
  t(key: string, ...args: string[]): string {
    return this._resolve(this.current, key, args);
  }

  private _initial(): Language {
    const stored = localStorage.getItem(STORAGE_KEY) as Language | null;
    return stored === 'fr' ? 'fr' : 'en';
  }

  private _resolve(lang: Language, key: string, args: string[]): string {
    let value = TRANSLATIONS[lang][key] ?? TRANSLATIONS['en'][key] ?? key;
    args.forEach((arg, i) => { value = value.replace(`{${i}}`, arg); });
    return value;
  }
}
