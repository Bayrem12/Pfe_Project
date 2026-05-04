import { Routes } from '@angular/router';

export const TEST_RUN_ROUTES: Routes = [
  {
    path: '',
    data: { breadcrumb: 'Test Runs' },
    loadComponent: () => import('./pages/test-runs-list-page/test-runs-list-page.component').then(m => m.TestRunsListPageComponent)
  },
  {
    path: ':id',
    data: { breadcrumb: 'Run Detail' },
    loadComponent: () => import('./pages/test-run-detail-page/test-run-detail-page.component').then(m => m.TestRunDetailPageComponent)
  }
];
