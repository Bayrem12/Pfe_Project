import { Routes } from '@angular/router';

export const NLP_ROUTES: Routes = [
  {
    path: '',
    redirectTo: 'action-mappings',
    pathMatch: 'full'
  },
  {
    path: 'action-mappings',
    loadComponent: () => import('./pages/nlp-action-mappings-page/nlp-action-mappings-page.component').then(m => m.NlpActionMappingsPageComponent)
  },
  {
    path: 'gherkin-parser',
    loadComponent: () => import('./pages/gherkin-parser-page/gherkin-parser-page.component').then(m => m.GherkinParserPageComponent)
  },
  {
    path: 'scenario-analysis/:id',
    loadComponent: () => import('./pages/scenario-analysis-page/scenario-analysis-page.component').then(m => m.ScenarioAnalysisPageComponent)
  }
];
