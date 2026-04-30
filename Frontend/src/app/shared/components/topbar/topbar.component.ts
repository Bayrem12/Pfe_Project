import { Component, OnInit, OnDestroy, inject, HostListener, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, NavigationEnd, ActivatedRoute, RouterModule } from '@angular/router';
import { filter, map, takeUntil } from 'rxjs/operators';
import { SidebarService } from '../../../core/services/sidebar.service';
import { BreadcrumbService, DynamicBreadcrumb } from '../../../core/services/breadcrumb.service';
import { RunNotificationsService, RunNotification } from '../../../core/services/run-notifications.service';
import { combineLatest, Subject } from 'rxjs';

interface Breadcrumb {
  label: string;
  url: string;
}

@Component({
  selector: 'app-topbar',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './topbar.component.html',
  styleUrls: ['./topbar.component.scss']
})
export class TopbarComponent implements OnInit, OnDestroy {
  private router = inject(Router);
  private activatedRoute = inject(ActivatedRoute);
  private sidebarService = inject(SidebarService);
  private breadcrumbService = inject(BreadcrumbService);
  readonly runNotifications = inject(RunNotificationsService);
  private host = inject(ElementRef<HTMLElement>);
  private destroy$ = new Subject<void>();

  notifications: RunNotification[] = [];
  notificationsOpen = false;

  breadcrumbs: Breadcrumb[] = [];
  dynamicCrumb: DynamicBreadcrumb | null = null;
  dynamicCrumbs: DynamicBreadcrumb[] = [];
  searchQuery = '';
  userAvatar = 'https://lh3.googleusercontent.com/aida-public/AB6AXuCkHPYrbvTwArRSsvW26Y_QypuMcjsvFywXIogZrfrJPtianHfKaAxXANggcjgvTyMlh_79vPyQ6ug_fpuTv3ccnkoAqHSJezH4_Zehb19gWapuKWPIIjh_rlyLv2oK65EAXlilx_pA0zkmpDeD28HwUkRiXwKQ6OpIp77D6LF1Txmqy_NF_AVR_9CQETxGWhp7b8VSDbQQ0TXsODhwSuS4SrKWy_2FAvKVFLosSwM4wZGlrSgGkgdYyZ777Come_6qII8_YTSAIbyL';

  ngOnInit(): void {
    const routeChange$ = this.router.events.pipe(
      filter(event => event instanceof NavigationEnd),
      map(() => this.buildBreadcrumbs(this.activatedRoute.root))
    );

    combineLatest([
      routeChange$,
      this.breadcrumbService.dynamicCrumb,
      this.breadcrumbService.dynamicCrumbs
    ]).pipe(
      takeUntil(this.destroy$)
    ).subscribe(([crumbs, dynamic, dynamicMulti]) => {
      this.breadcrumbs = crumbs;
      this.dynamicCrumb = dynamic;
      this.dynamicCrumbs = dynamicMulti;
    });

    // Build initial breadcrumbs
    this.breadcrumbs = this.buildBreadcrumbs(this.activatedRoute.root);

    // Subscribe to live run notifications.
    this.runNotifications.runs$
      .pipe(takeUntil(this.destroy$))
      .subscribe(runs => (this.notifications = runs));
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private buildBreadcrumbs(route: ActivatedRoute, url: string = '', breadcrumbs: Breadcrumb[] = []): Breadcrumb[] {
    const children: ActivatedRoute[] = route.children;

    if (children.length === 0) {
      return breadcrumbs;
    }

    for (const child of children) {
      const routeURL: string = child.snapshot.url.map(segment => segment.path).join('/');
      if (routeURL !== '') {
        url += `/${routeURL}`;
      }

      // Get breadcrumb label from route data
      const label = child.snapshot.data['breadcrumb'];
      if (label) {
        // Avoid duplicates - check if this label already exists
        const exists = breadcrumbs.some(b => b.label === label);
        if (!exists) {
          breadcrumbs.push({ label, url });
        }
      }

      // Recursive call for nested routes
      return this.buildBreadcrumbs(child, url, breadcrumbs);
    }

    return breadcrumbs;
  }

  onSearch(): void {
    console.log('Search:', this.searchQuery);
    // TODO: Implement search functionality
  }

  openNotifications(): void {
    this.notificationsOpen = !this.notificationsOpen;
    if (this.notificationsOpen) {
      // Mark as read shortly after open so the badge clears but the user
      // still notices the freshly-arrived items.
      setTimeout(() => this.runNotifications.markAllAsRead(), 600);
    }
  }

  closeNotifications(): void {
    this.notificationsOpen = false;
  }

  onNotificationClick(n: RunNotification): void {
    this.runNotifications.markAsRead(n.id);
    if (n.executionId) {
      this.router.navigate(['/test-runs', n.executionId]);
      this.notificationsOpen = false;
    }
  }

  dismissNotification(event: Event, id: string): void {
    event.stopPropagation();
    this.runNotifications.dismiss(id);
  }

  clearCompleted(event: Event): void {
    event.stopPropagation();
    this.runNotifications.clearCompleted();
  }

  notifIcon(n: RunNotification): string {
    if (n.state === 'running') return 'progress_activity';
    if (n.state === 'passed') return 'check_circle';
    return 'cancel';
  }

  notifColorClass(n: RunNotification): string {
    if (n.state === 'running') return 'text-primary';
    if (n.state === 'passed') return 'text-emerald-600';
    return 'text-rose-600';
  }

  trackByNotifId(_: number, n: RunNotification): string {
    return n.id;
  }

  formatRelative(d: Date): string {
    const diff = Math.max(0, Date.now() - new Date(d).getTime());
    const sec = Math.floor(diff / 1000);
    if (sec < 60) return `${sec}s ago`;
    const min = Math.floor(sec / 60);
    if (min < 60) return `${min}m ago`;
    const hr = Math.floor(min / 60);
    if (hr < 24) return `${hr}h ago`;
    return new Date(d).toLocaleDateString();
  }

  formatDuration(ms?: number): string {
    if (!ms) return '';
    if (ms < 1000) return `${ms}ms`;
    return `${(ms / 1000).toFixed(1)}s`;
  }

  @HostListener('document:click', ['$event'])
  onDocClick(event: MouseEvent): void {
    if (!this.notificationsOpen) return;
    if (!this.host.nativeElement.contains(event.target as Node)) {
      this.notificationsOpen = false;
    }
  }

  openHistory(): void {
    console.log('Open history');
  }

  toggleSidebar(): void {
    this.sidebarService.toggle();
  }

  navigateCrumb(crumb: DynamicBreadcrumb): void {
    if (crumb.url) {
      this.router.navigate([crumb.url], { queryParams: crumb.queryParams || {} });
    }
  }

  goToProfile(): void {
    this.router.navigate(['/profile']);  // Remplacez '/profile' par le chemin réel vers votre composant profile
  }
}