import { Pipe, PipeTransform } from '@angular/core';
import { TranslationService } from '../../core/services/translation.service';

/**
 * Usage in templates: {{ 'some.key' | translate }}
 * The pipe is NOT pure so it reacts to language changes automatically.
 */
@Pipe({
  name: 'translate',
  standalone: true,
  pure: false,
})
export class TranslatePipe implements PipeTransform {
  constructor(private readonly ts: TranslationService) {}

  transform(key: string, ...args: string[]): string {
    return this.ts.t(key, ...args);
  }
}
