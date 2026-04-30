import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from './api.service';
import { ResponseHttp } from '../models/response-http.model';
import { TestRunDetail, TestRunListPayload } from '../models/test-run.model';

@Injectable({ providedIn: 'root' })
export class TestRunService {
  constructor(private apiService: ApiService) {}

  getTestRuns(params?: {
    page?: number;
    pageSize?: number;
    search?: string;
    status?: string;
  }): Observable<ResponseHttp<TestRunListPayload>> {
    return this.apiService.get<TestRunListPayload>('test-runs', params);
  }

  getTestRunDetail(runId: string): Observable<ResponseHttp<TestRunDetail>> {
    return this.apiService.get<TestRunDetail>(`test-runs/${runId}`);
  }
}
