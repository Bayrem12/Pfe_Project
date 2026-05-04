import { Routes } from '@angular/router';
import { scenarioCreateGuard, scenarioEditGuard } from '../../core/guards/scenario-permissions.guard';

export const SCENARIO_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/scenarios-list-page/scenarios-list-page.component')
      .then(m => m.ScenariosListPageComponent)
  },
  {
    path: 'new',
    canActivate: [scenarioCreateGuard],
    loadComponent: () => import('./pages/scenario-editor-page/scenario-editor-page.component')
      .then(m => m.ScenarioEditorPageComponent)
  },
  {
    path: ':id/edit',
    canActivate: [scenarioEditGuard],
    loadComponent: () => import('./pages/scenario-editor-page/scenario-editor-page.component')
      .then(m => m.ScenarioEditorPageComponent)
  },
  {
    path: ':id',
    loadComponent: () => import('./pages/scenario-detail-page/scenario-detail-page.component')
      .then(m => m.ScenarioDetailPageComponent)
  }
];