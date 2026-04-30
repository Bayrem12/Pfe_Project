import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable,of, map } from 'rxjs';
import { Project, CreateProjectRequest, UpdateProjectRequest, ProjectMember } from '../models/project.model';
import { ResponseHttp } from '../models/response-http.model';
import { AuthService } from './auth.service';
import { environment } from '../../../environments/environment';

export function toSlug(name: string): string {
  return name
    .toLowerCase()
    .trim()
    .replace(/[^a-z0-9\s-]/g, '')
    .replace(/\s+/g, '-')
    .replace(/-+/g, '-');
}

@Injectable({
  providedIn: 'root'
})
export class ProjectService {
  private apiUrl = `${environment.apiUrl}/projet`;
  private authService = inject(AuthService);

  constructor(private http: HttpClient) {}

  // GET - Récupérer tous les projets d'un utilisateur
  getProjects(userId: string): Observable<ResponseHttp> {
    return this.http.get<ResponseHttp>(`${this.apiUrl}?userId=${userId}`);
  }

  /**
   * GET all projects for the current logged-in user.
   * Resolves userId internally from AuthService.
   * Returns a clean Project[] (extracted from the paginated response).
   */
  getUserProjects(pageNumber = 1, pageSize = 100): Observable<Project[]> {
    const userId = this.authService.getCurrentUserId();
    if (!userId) {
      return of([]);
    }
    return this.http.get<ResponseHttp>(`${this.apiUrl}?userId=${userId}&pageNumber=${pageNumber}&pageSize=${pageSize}`).pipe(
      map(response => response?.resultat?.items || [])
    );
  }

  // GET - Récupérer un projet par ID
  getProjectById(id: string): Observable<ResponseHttp<Project>> {
    return this.http.get<ResponseHttp<Project>>(`${this.apiUrl}/${id}`);
  }

  // POST - Créer un nouveau projet
  createProject(project: CreateProjectRequest): Observable<ResponseHttp<Project>> {
    return this.http.post<ResponseHttp<Project>>(this.apiUrl, project);
  }

  // PUT - Mettre à jour un projet
  updateProject(id: string, project: UpdateProjectRequest): Observable<ResponseHttp<Project>> {
    return this.http.put<ResponseHttp<Project>>(`${this.apiUrl}/${id}`, project);
  }

  // DELETE - Supprimer un projet
  deleteProject(id: string): Observable<ResponseHttp> {
    return this.http.delete<ResponseHttp>(`${this.apiUrl}/${id}`);
  }

  // GET - Récupérer les membres d'un projet
  getProjectMembers(projectId: string): Observable<ResponseHttp<ProjectMember[]>> {
    return this.http.get<ResponseHttp<ProjectMember[]>>(`${this.apiUrl}/${projectId}/members`);
  }
}
