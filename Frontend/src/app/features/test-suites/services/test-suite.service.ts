import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import {
  TestSuiteDto,
  TestSuiteWithCasesDto,
  CreateTestSuiteRequest,
  UpdateTestSuiteRequest
} from '../models/test-suite.model';

@Injectable({
  providedIn: 'root'
})
export class TestSuiteService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/testSuites`;

  getTestSuitesByProject(projectId: string): Observable<TestSuiteDto[]> {
    return this.http.get<TestSuiteDto[]>(`${this.baseUrl}/by-project/${projectId}`);
  }

  getTestSuiteWithCases(suiteId: string): Observable<TestSuiteWithCasesDto> {
    return this.http.get<TestSuiteWithCasesDto>(`${this.baseUrl}/${suiteId}/with-cases`);
  }

  createTestSuite(data: CreateTestSuiteRequest): Observable<string> {
    return this.http.post<string>(this.baseUrl, data);
  }

  updateTestSuite(id: string, data: UpdateTestSuiteRequest): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, data);
  }

  deleteTestSuite(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  addScenarioToSuite(suiteId: string, scenarioId: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${suiteId}/scenarios/${scenarioId}`, {});
  }

  removeScenarioFromSuite(suiteId: string, scenarioId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${suiteId}/scenarios/${scenarioId}`);
  }
}
