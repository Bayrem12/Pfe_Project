import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterOutlet, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs/operators';
import { FormsModule } from '@angular/forms';
import { SidebarNavComponent } from './shared/components/sidebar-nav/sidebar-nav.component';
import { TopbarComponent } from './shared/components/topbar/topbar.component';
import { SidebarService } from './core/services/sidebar.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet, SidebarNavComponent, TopbarComponent,FormsModule],
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent {
  title = 'AutoTestify';
  showLayout = true;

  sidebarService = inject(SidebarService);
  router = inject(Router);

  private shouldDisplayLayout(url: string): boolean {
    const normalizedUrl = (url || '').split('?')[0].split('#')[0];
    const isLanding = normalizedUrl === '' || normalizedUrl === '/' || normalizedUrl === '/landing';
    const isAuthArea = normalizedUrl.startsWith('/auth');

    return !isLanding && !isAuthArea;
  }

  constructor() {
    // 🔥 au chargement (refresh)
    const currentUrl = this.router.url;
    this.showLayout = this.shouldDisplayLayout(currentUrl);

    // 🔥 lors du changement de route
    this.router.events
      .pipe(filter(event => event instanceof NavigationEnd))
      .subscribe((event: NavigationEnd) => {
        this.showLayout = this.shouldDisplayLayout(event.urlAfterRedirects || event.url);
      });
  }
}