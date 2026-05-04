import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { ApiService } from './api.service';
import { ResponseHttp } from '../models/response-http.model';

export interface AiRunRequest {
  scenarioId: string;
  /** When false, the browser is visible so the user can watch the test execute. Default true. */
  isHeadless?: boolean;
}

export interface AiRunSuiteRequest {
  testSuiteId: string;
  /** When false, the browser is visible so the user can watch the test execute. Default true. */
  isHeadless?: boolean;
}

export interface AiRunResult {
  executionId: string;
}

@Injectable({ providedIn: 'root' })
export class IaAgentService {
  private apiService = inject(ApiService);

  /**
   * POST /api/ai-runs
   * Triggers the AI Test Agent pipeline for a given scenario.
   * Returns the new TestExecution Id (UUID) so the caller can redirect
   * to the test-run detail page.
   */
  runScenario(request: AiRunRequest): Observable<AiRunResult> {
    return this.apiService
      .post<AiRunResult>('ai-runs', request)
      .pipe(map((r: ResponseHttp<AiRunResult>) => r.resultat));
  }

  /**
   * POST /api/ai-runs/test-suite
   * Runs every scenario of a test suite under a single TestExecution.
   */
  runTestSuite(request: AiRunSuiteRequest): Observable<AiRunResult> {
    return this.apiService
      .post<AiRunResult>('ai-runs/test-suite', request)
      .pipe(map((r: ResponseHttp<AiRunResult>) => r.resultat));
  }
}
