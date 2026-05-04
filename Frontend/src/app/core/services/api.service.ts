import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ResponseHttp } from '../models/response-http.model';

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  private http = inject(HttpClient);
  private baseUrl = environment.apiUrl;

  get<T>(endpoint: string, params?: any): Observable<ResponseHttp<T>> {
    let httpParams = new HttpParams();
    
    if (params) {
      Object.keys(params).forEach(key => {
        if (params[key] !== null && params[key] !== undefined) {
          httpParams = httpParams.set(key, params[key].toString());
        }
      });
    }

    return this.http.get<ResponseHttp<T>>(`${this.baseUrl}/${endpoint}`, { params: httpParams });
  }

  post<T>(endpoint: string, body: any): Observable<ResponseHttp<T>> {
    return this.http.post<ResponseHttp<T>>(`${this.baseUrl}/${endpoint}`, body);
  }

  put<T>(endpoint: string, body: any): Observable<ResponseHttp<T>> {
    return this.http.put<ResponseHttp<T>>(`${this.baseUrl}/${endpoint}`, body);
  }

  delete<T>(endpoint: string): Observable<ResponseHttp<T>> {
    return this.http.delete<ResponseHttp<T>>(`${this.baseUrl}/${endpoint}`);
  }
}
