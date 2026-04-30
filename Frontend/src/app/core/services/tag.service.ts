import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { CreateTagRequest, ProjectTag } from '../models/tag.model';
import { ResponseHttp } from '../models/response-http.model';

@Injectable({
  providedIn: 'root'
})
export class TagService {
  private api = inject(ApiService);

  getProjectTags(projectId: string): Observable<ResponseHttp<ProjectTag[]>> {
    return this.api.get<ProjectTag[]>(`Tags/by-project/${projectId}`);
  }

  createTag(payload: CreateTagRequest): Observable<ResponseHttp<ProjectTag>> {
    return this.api.post<ProjectTag>('tags', {
      projectId: payload.projectId,
      name: payload.name,
      color: payload.color,
      description: payload.description ?? null
    });
  }

  deleteTag(tagId: string): Observable<ResponseHttp<void>> {
    return this.api.delete<void>(`tags/${tagId}`);
  }
}
