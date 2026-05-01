import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule, NavigationEnd } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { SidebarService } from '../../../core/services/sidebar.service';
import { TranslatePipe } from '../../pipes/translate.pipe';
import { filter } from 'rxjs/operators';

interface NavItem {
  label: string;
  icon: string;
  route: string;
  filled?: boolean;
}

@Component({
  selector: 'app-sidebar-nav',
  standalone: true,
  imports: [CommonModule, RouterModule, TranslatePipe],
  templateUrl: './sidebar-nav.component.html',
  styleUrls: ['./sidebar-nav.component.scss']
})
export class SidebarNavComponent {
  private authService = inject(AuthService);
  private router = inject(Router);
  sidebarService = inject(SidebarService);

  currentRoute = '';

  private allNavItems: NavItem[] = [
    { label: 'nav.dashboard', icon: 'dashboard', route: '/dashboard' },
    { label: 'nav.projects', icon: 'folder_open', route: '/projects' },
    { label: 'nav.scenarios', icon: 'description', route: '/scenarios' },
    { label: 'nav.testSuites', icon: 'content_copy', route: '/test-suites' },
    { label: 'nav.testRuns', icon: 'play_circle', route: '/test-runs' },
    { label: 'nav.nlp', icon: 'auto_awesome', route: '/nlp/action-mappings' },
    { label: 'Audit Logs', icon: 'history', route: '/audit-logs' },
  ];

  navItems: NavItem[] = [];

  private allBottomNavItems: NavItem[] = [
    { label: 'nav.profile', icon: 'person', route: '/profile' },
    { label: 'nav.admin', icon: 'group', route: '/users' },
  ];

  bottomNavItems: NavItem[] = [];

  constructor() {
    this.currentRoute = this.router.url;

    this.authService.currentUser$.subscribe((user) => {
      const canViewAuditLogs = user ? this.authService.canViewAuditLogs() : true;
      this.navItems = this.allNavItems.filter(item => {
        if (item.route === '/audit-logs') return canViewAuditLogs;
        return true;
      });

      this.bottomNavItems = this.authService.canManageUsers()
        ? [...this.allBottomNavItems]
        : this.allBottomNavItems.filter(item => item.label === 'Profile');
    });

    this.router.events.pipe(
      filter(event => event instanceof NavigationEnd)
    ).subscribe((event: any) => {
      this.currentRoute = event.url;
    });
  }

  isActive(route: string): boolean {
    return this.currentRoute.startsWith(route);
  }

  logout(): void {
    this.authService.logout();
  }
}
