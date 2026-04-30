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
}
