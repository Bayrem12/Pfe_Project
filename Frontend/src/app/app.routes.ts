import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { auditLogsGuard } from './core/guards/audit-logs.guard';
import { OauthCallbackComponent } from './features/auth/oauth-callback/oauth-callback.component';

export const routes: Routes = [
  {
    path: '',
    redirectTo: '/landing',
    pathMatch: 'full'
  },
  {
    path: 'auth',
    loadChildren: () =>
      import('./features/auth/auth.routes').then(m => m.authRoutes),
  },

  // ✅ OAuth Callbacks
  {
    path: 'auth/callback/google',
    component: OauthCallbackComponent
  },
  {
    path: 'auth/callback/github',
    component: OauthCallbackComponent
  },

  {
    path: 'landing',
    loadComponent: () =>
      import('./features/landing/landing-page.component').then(m => m.LandingPageComponent),
  },
  {
    path: 'about',
    data: { breadcrumb: 'About' },
    loadComponent: () =>
      import('./features/about/about-page.component').then(m => m.AboutPageComponent),
  },
  {
    path: 'help',
    data: { breadcrumb: 'Help Center' },
    loadComponent: () =>
      import('./features/help/help-page.component').then(m => m.HelpPageComponent),
  },
  {
    path: 'users',
    canActivate: [authGuard],
    loadChildren: () =>
      import('./features/admin/admin.routes').then(m => m.adminRoutes),
  },
  {
    path: 'profile',
    loadComponent: () =>
      import('./features/profile/profile.component').then(m => m.ProfileComponent),
    title: 'Mon profil',
  },
  {
    path: 'dashboard',
    canActivate: [authGuard],
    data: { breadcrumb: 'Dashboard' },
    loadChildren: () =>
      import('./features/dashboard/dashboard.routes').then(m => m.dashboardRoutes)
  },
  {
    path: 'projects',
    canActivate: [authGuard],
    data: { breadcrumb: 'Projects' },
    loadChildren: () =>
      import('./features/projects/projects.routes').then(m => m.projectsRoutes)
  },
  {
    path: 'features',
    canActivate: [authGuard],
    loadChildren: () =>
      import('./features/features-management/features.routes').then(m => m.FEATURES_ROUTES)
  },
  {
    path: 'scenarios',
    canActivate: [authGuard],
    loadChildren: () =>
      import('./features/scenarios/scenarios.routes').then(m => m.SCENARIO_ROUTES)
  },
  {
    path: 'test-suites',
    canActivate: [authGuard],
    data: { breadcrumb: 'Test Suites' },
    loadChildren: () =>
      import('./features/test-suites/test-suites.routes').then(m => m.TEST_SUITE_ROUTES)
  },
  {
    path: 'test-runs',
    canActivate: [authGuard],
    data: { breadcrumb: 'Test Runs' },
    loadChildren: () =>
      import('./features/test-runs/test-runs.routes').then(m => m.TEST_RUN_ROUTES)
  },
  {
    path: 'nlp',
    canActivate: [authGuard],
    data: { breadcrumb: 'NLP' },
    loadChildren: () =>
      import('./features/nlp/nlp.routes').then(m => m.NLP_ROUTES)
  },
  {
    path: 'audit-logs',
    canActivate: [authGuard, auditLogsGuard],
    data: { breadcrumb: 'Audit Logs' },
    loadChildren: () =>
      import('./features/audit-logs/audit-logs.routes').then(m => m.auditLogsRoutes)
  },
  {
    path: '**',
    redirectTo: ''
  }
];