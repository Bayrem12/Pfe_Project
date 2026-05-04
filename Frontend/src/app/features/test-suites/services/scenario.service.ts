import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { ApiService } from '../../../core/services/api.service';
import { ScenarioDto, ScenarioStatus } from '../models/test-suite.model';

@Injectable({
  providedIn: 'root'
})
export class ScenarioService {
  private apiService = inject(ApiService);

  getScenariosByProject(projectId: string, search?: string, status?: ScenarioStatus): Observable<ScenarioDto[]> {
    const params: any = { projectId };
    if (search) params.search = search;
    if (status !== undefined) params.status = status;

    return this.apiService.get<ScenarioDto[]>('scenarios', params).pipe(
      map(response => response.resultat || [])
    );
  }
}
