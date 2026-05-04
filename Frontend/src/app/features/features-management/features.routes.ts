// src/app/features/features-management/features.routes.ts
import { Routes } from '@angular/router';

export const FEATURES_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./pages/features-list-page/features-list-page.component')
        .then(m => m.FeaturesListPageComponent)
  },
  {
    path: 'new',
    loadComponent: () =>
      import('./pages/feature-form-page/feature-form-page.component')
        .then(m => m.FeatureFormPageComponent)
  },
  {
    path: ':id',
    loadComponent: () =>
      import('./pages/feature-detail-page/feature-detail-page.component')
        .then(m => m.FeatureDetailPageComponent)
  },
  {
    path: ':id/edit',
    loadComponent: () =>
      import('./pages/feature-form-page/feature-form-page.component')
        .then(m => m.FeatureFormPageComponent)
  }
];