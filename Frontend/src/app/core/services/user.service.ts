import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ResponseHttp } from '../models/response-http.model';

export interface User {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  roles: string[];
  isActive: boolean;
  createdDate?: string;
  modifiedDate?: string;
}

export interface UserDto {
  firstName: string;
  lastName: string;
  email: string;
  isActive: boolean;
}

export interface CreateUserDto {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
}

export interface UpdateUserRolesDto {
  roles: string[];
}

@Injectable({
  providedIn: 'root'
})
export class UserService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/user`;
  private authUrl = `${environment.apiUrl}/auth`;

  // ✅ GET /api/user
  getAllUsers(): Observable<ResponseHttp<User[]>> {
    return this.http.get<ResponseHttp<User[]>>(this.baseUrl);
  }

  // ✅ GET /api/user/{id}
  getUserById(id: string): Observable<ResponseHttp<User>> {
    return this.http.get<ResponseHttp<User>>(`${this.baseUrl}/${id}`);
  }

  // ✅ GET /api/user/search?keyword=
  searchUsers(keyword: string): Observable<ResponseHttp<User[]>> {
    return this.http.get<ResponseHttp<User[]>>(`${this.baseUrl}/search?keyword=${keyword}`);
  }

  // ✅ PUT /api/user/{id}
  updateUser(id: string, dto: UserDto): Observable<ResponseHttp<User>> {
    return this.http.put<ResponseHttp<User>>(`${this.baseUrl}/${id}`, dto);
  }

  // ✅ PUT /api/user/{id}/roles
  updateUserRoles(id: string, roles: string[]): Observable<ResponseHttp<any>> {
    return this.http.put<ResponseHttp<any>>(`${this.baseUrl}/${id}/roles`, { roles });
  }

  // ✅ POST /api/user/{id}/toggle-status
  toggleStatus(id: string): Observable<ResponseHttp<any>> {
    return this.http.post<ResponseHttp<any>>(`${this.baseUrl}/${id}/toggle-status`, {});
  }

  // ✅ POST /api/auth/register
  inviteUser(dto: CreateUserDto): Observable<ResponseHttp<any>> {
    return this.http.post<ResponseHttp<any>>(`${this.authUrl}/register`, dto);
  }
}