import { Routes } from '@angular/router';

export const TEST_SUITE_ROUTES: Routes = [
  {
    path: '',
    data: { breadcrumb: 'List' },
    loadComponent: () => import('./pages/test-suites-list-page.component').then(m => m.TestSuitesListPageComponent)
  },
  {
    path: 'builder/:suiteId',
    data: { breadcrumb: 'Builder' },
    loadComponent: () => import('./pages/test-suite-builder-page/test-suite-builder-page.component').then(m => m.TestSuiteBuilderPageComponent)
  }
];
