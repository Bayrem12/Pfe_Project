import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ResponseHttp } from '../models/response-http.model';
import { Module, CreateModuleRequest } from '../models/module.model';

@Injectable({ providedIn: 'root' })
export class ModuleService {
  private apiUrl = `${environment.apiUrl}/modules`;

  constructor(private http: HttpClient) {}

  getModulesByProjectId(projectId: string): Observable<ResponseHttp<Module[]>> {
    return this.http.get<ResponseHttp<Module[]>>(
      `${this.apiUrl}/by-project/${projectId}`
    );
  }

  createModule(payload: CreateModuleRequest): Observable<string> {
    return this.http.post<string>(this.apiUrl, {
      ProjectId: payload.projectId,
      Name: payload.name,
      Description: payload.description,
      DisplayOrder: payload.displayOrder
    });
  }

  deleteModule(moduleId: string): Observable<ResponseHttp<boolean>> {
    return this.http.delete<ResponseHttp<boolean>>(`${this.apiUrl}/${moduleId}`);
  }
}