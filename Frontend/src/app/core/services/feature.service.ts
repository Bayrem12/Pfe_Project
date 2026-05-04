// frontend/src/app/features/features/services/feature.service.ts

import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { 
  FeatureDTO, 
  FeatureListDTO,
  CreateFeatureRequest,
  UpdateFeatureRequest
} from '../models/feature.model';
import { ResponseHttp } from '../models/response-http.model';
import { environment } from '../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class FeatureService {
  private apiUrl = `${environment.apiUrl}/features`;

  constructor(private http: HttpClient) {}

  /**
   * Récupérer toutes les features d'un module
   */
  getFeaturesByModule(moduleId: string): Observable<ResponseHttp<FeatureListDTO[]>> {
    return this.http.get<ResponseHttp<FeatureListDTO[]>>(`${this.apiUrl}/by-module/${moduleId}`);
  }

  /**
   * Récupérer une feature par son ID
   */
  getFeatureById(id: string): Observable<ResponseHttp<FeatureDTO>> {
    return this.http.get<ResponseHttp<FeatureDTO>>(`${this.apiUrl}/${id}`);
  }

  /**
   * Créer une nouvelle feature
   */
  createFeature(request: CreateFeatureRequest): Observable<ResponseHttp<FeatureDTO>> {
    return this.http.post<ResponseHttp<FeatureDTO>>(this.apiUrl, request);
  }

  /**
   * Mettre à jour une feature
   */
  updateFeature(id: string, request: UpdateFeatureRequest): Observable<ResponseHttp<FeatureDTO>> {
    return this.http.put<ResponseHttp<FeatureDTO>>(`${this.apiUrl}/${id}`, request);
  }

  /**
   * Supprimer une feature
   */
  deleteFeature(id: string): Observable<ResponseHttp<boolean>> {
    return this.http.delete<ResponseHttp<boolean>>(`${this.apiUrl}/${id}`);
  }
}