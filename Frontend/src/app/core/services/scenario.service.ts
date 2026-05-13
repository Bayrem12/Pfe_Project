import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ResponseHttp } from '../models/response-http.model';
import { 
  ScenarioDto, 
  ScenarioDetailDto,
  CreateScenarioRequest,
  UpdateScenarioRequest,
  ValidationResultDto,
  ImportScenarioRequest,
  ScenarioStatus
} from '../../core/models/scenario.model';
import { environment } from '../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class ScenarioService {
  private apiUrl = `${environment.apiUrl}/scenarios`;

  constructor(private http: HttpClient) {}

  /**
   * Récupérer tous les scénarios d'un projet avec filtres
   */
  
  getScenarios(
    projectId: string, 
    search?: string, 
    status?: ScenarioStatus
  ): Observable<ResponseHttp<ScenarioDto[]>> {
    let params = new HttpParams().set('projectId', projectId);
    
    if (search) {
      params = params.set('search', search);
    }
    if (status) {
      params = params.set('status', status);
    }

    return this.http.get<ResponseHttp<ScenarioDto[]>>(this.apiUrl, { params });
  }

  /**
   * Récupérer un scénario par son ID
   */
  getScenarioById(id: string): Observable<ResponseHttp<ScenarioDetailDto>> {
    return this.http.get<ResponseHttp<ScenarioDetailDto>>(`${this.apiUrl}/${id}`);
  }

  /**
   * Créer un nouveau scénario
   */
  createScenario(request: CreateScenarioRequest): Observable<ResponseHttp<ScenarioDetailDto>> {
    return this.http.post<ResponseHttp<ScenarioDetailDto>>(this.apiUrl, request);
  }

  /**
   * Mettre à jour un scénario
   */
  updateScenario(id: string, request: UpdateScenarioRequest): Observable<ResponseHttp<ScenarioDetailDto>> {
    return this.http.put<ResponseHttp<ScenarioDetailDto>>(`${this.apiUrl}/${id}`, request);
  }

  /**
   * Supprimer un scénario
   */
  deleteScenario(id: string): Observable<ResponseHttp<boolean>> {
    return this.http.delete<ResponseHttp<boolean>>(`${this.apiUrl}/${id}`);
  }

  /**
   * Valider la syntaxe Gherkin
   */
  validateGherkin(gherkinContent: string): Observable<ResponseHttp<ValidationResultDto>> {
    return this.http.post<ResponseHttp<ValidationResultDto>>(`${this.apiUrl}/validate`, {
      gherkinContent
    });
  }

  /**
   * Save AI quality score after analysis
   */
  saveQualityScore(id: string, score: number, label: string): Observable<ResponseHttp<ScenarioDto>> {
    return this.http.put<ResponseHttp<ScenarioDto>>(`${this.apiUrl}/${id}/quality-score`, { score, label });
  }

  /**
   * Exporter un scénario au format .feature
   */
  exportScenario(id: string): Observable<ResponseHttp<string>> {
    return this.http.get<ResponseHttp<string>>(`${this.apiUrl}/${id}/export`);
  }

  /**
   * Importer des scénarios depuis un fichier .feature
   */
  importScenarios(request: ImportScenarioRequest): Observable<ResponseHttp<any>> {
    return this.http.post<ResponseHttp<any>>(`${this.apiUrl}/import`, request);
  }
  
  
}

