import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { LoginRequest, LoginResponse } from '../models/auth.models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private static readonly tokenKey = 'mini-orders-access-token';
  private readonly http = inject(HttpClient);

  readonly accessToken = signal<string | null>(localStorage.getItem(AuthService.tokenKey));

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>('/auth/login', request).pipe(
      tap(response => {
        localStorage.setItem(AuthService.tokenKey, response.accessToken);
        this.accessToken.set(response.accessToken);
      })
    );
  }

  logout(): void {
    localStorage.removeItem(AuthService.tokenKey);
    this.accessToken.set(null);
  }
}
