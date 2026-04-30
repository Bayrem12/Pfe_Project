import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export interface DynamicBreadcrumb {
  label: string;
  url?: string;
  queryParams?: { [key: string]: string };
}

@Injectable({ providedIn: 'root' })
export class BreadcrumbService {
  private dynamicCrumb$ = new BehaviorSubject<DynamicBreadcrumb | null>(null);
  private dynamicCrumbs$ = new BehaviorSubject<DynamicBreadcrumb[]>([]);

  readonly dynamicCrumb = this.dynamicCrumb$.asObservable();
  readonly dynamicCrumbs = this.dynamicCrumbs$.asObservable();

  set(label: string, url?: string): void {
    this.dynamicCrumb$.next({ label, url });
    this.dynamicCrumbs$.next([]);
  }

  setMultiple(crumbs: DynamicBreadcrumb[]): void {
    this.dynamicCrumb$.next(null);
    this.dynamicCrumbs$.next(crumbs);
  }

  clear(): void {
    this.dynamicCrumb$.next(null);
    this.dynamicCrumbs$.next([]);
  }
}
