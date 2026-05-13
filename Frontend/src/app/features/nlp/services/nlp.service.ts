import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { ApiService } from '../../../core/services/api.service';
import {
  ParseGherkinRequest,
  ParseGherkinResponse,
  StepAnalysis,
  CreateActionMappingRequest,
  ActionMapping
} from '../models/nlp.model';

@Injectable({
  providedIn: 'root'
})
export class NlpService {
  private apiService = inject(ApiService);

  /**
   * Parse raw Gherkin text into structured steps
   * POST /api/nlp/parse
   * @param gherkinContent Raw Gherkin text to parse
   */
  parseGherkin(gherkinContent: string): Observable<ParseGherkinResponse> {
    const request: ParseGherkinRequest = { gherkinContent };
    return this.apiService.post<ParseGherkinResponse>('nlp/parse', request).pipe(
      map(response => response.resultat)
    );
  }

  /**
   * Analyze all steps of a scenario using NLP
   * POST /api/nlp/analyze/{scenarioId}
   * @param scenarioId The scenario GUID to analyze
   */
  analyzeScenario(scenarioId: string): Observable<StepAnalysis[]> {
    return this.apiService.post<StepAnalysis[]>(`nlp/analyze/${scenarioId}`, {}).pipe(
      map(response => response.resultat)
    );
  }

  /**
   * Create a new action mapping
   * POST /api/nlp/action-mappings/{projectId}
   * @param projectId The project GUID
   * @param mapping The action mapping to create
   */
  createActionMapping(projectId: string, mapping: CreateActionMappingRequest): Observable<ActionMapping> {
    return this.apiService.post<ActionMapping>(`nlp/action-mappings/${projectId}`, mapping).pipe(
      map(response => response.resultat)
    );
  }

  /**
   * Get all action mappings for a project
   * GET /api/nlp/action-mappings/{projectId}
   */
  getActionMappings(projectId: string): Observable<ActionMapping[]> {
    return this.apiService.get<ActionMapping[]>(`nlp/action-mappings/${projectId}`).pipe(
      map(response => response.resultat || [])
    );
  }

  /**
   * Delete an action mapping
   * DELETE /api/nlp/action-mappings/{mappingId}
   */
  deleteActionMapping(mappingId: string): Observable<void> {
    return this.apiService.delete<void>(`nlp/action-mappings/${mappingId}`).pipe(
      map(response => response.resultat)
    );
  }

  /**
   * Update an action mapping
   * PUT /api/nlp/action-mappings/{mappingId}
   */
  updateActionMapping(mappingId: string, mapping: CreateActionMappingRequest): Observable<ActionMapping> {
    return this.apiService.put<ActionMapping>(`nlp/action-mappings/${mappingId}`, mapping).pipe(
      map(response => response.resultat)
    );
  }

  /**
   * Toggle action mapping active status
   * PUT /api/nlp/action-mappings/{mappingId}/status
   */
  toggleActionMappingStatus(mappingId: string, isActive: boolean): Observable<ActionMapping> {
    return this.apiService.put<ActionMapping>(`nlp/action-mappings/${mappingId}/status`, { isActive }).pipe(
      map(response => response.resultat)
    );
  }

  /**
   * Analyze the quality of a scenario's steps before execution.
   * POST /api/nlp/analyze-quality
   * Works before saving — no scenarioId required.
   */
  analyzeQuality(scenarioName: string, steps: { keyword: string; text: string }[], language = 'en'): Observable<ScenarioQualityResult> {
    return this.apiService.post<ScenarioQualityResult>('nlp/analyze-quality', {
      scenarioName,
      steps,
      language,
    }).pipe(map(r => r.resultat));
  }

  /**
   * Analyze a single failed step — returns root cause, category, explanation,
   * suggested fix and confidence. Triggered manually from the UI when a test
   * has failed.
   * POST /api/nlp/analyze-failure
   */
  analyzeFailure(req: AnalyzeFailureRequest): Observable<FailureAnalysisResult> {
    return this.apiService.post<FailureAnalysisResult>('nlp/analyze-failure', req)
      .pipe(map(r => r.resultat));
  }
}

// ── Quality analysis models ───────────────────────────────────────────────

export interface QualityIssue {
  severity: 'error' | 'warning' | 'info';
  step_index: number | null;
  step_text: string | null;
  message: string;
  why: string;
}

export interface ScenarioQualityResult {
  quality_score: number;
  quality_label: 'good' | 'medium' | 'poor';
  issues: QualityIssue[];
  suggestions: string[];
  improved_steps: { keyword: string; text: string }[];
  best_practices: string[];
  using_llm: boolean;
  analysis_method?: string;  // regex | semantic-ai | zero-shot-ai | llm-ai
}

// ── Failure analysis models ───────────────────────────────────────────────

export interface AnalyzeFailureRequest {
  stepText: string;
  errorMessage: string;
  selector?: string;
  keyword?: string;
  visualFallbackUsed?: boolean;
  retryCount?: number;
}

export interface FailureAnalysisResult {
  category: string;          // test | application | detection | timing | environment | unknown
  root_cause: string;
  title: string;
  explanation: string;
  where: string;
  is_test_issue: boolean;
  suggested_fix: string;
  confidence: number;        // 0..1
  analysis_method?: string;  // regex | semantic-ai | zero-shot-ai | llm-ai | fallback
}
