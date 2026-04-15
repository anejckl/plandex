import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, tap, catchError, of } from 'rxjs';
import { Router } from '@angular/router';
import { User, AuthResponse } from '../shared/models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly _user$ = new BehaviorSubject<User | null>(null);
  readonly user$ = this._user$.asObservable();

  private _accessToken: string | null = null;

  get accessToken(): string | null {
    return this._accessToken;
  }

  get currentUser(): User | null {
    return this._user$.value;
  }

  register(email: string, password: string, name: string): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>('/api/auth/register', { email, password, name }, { withCredentials: true })
      .pipe(tap((r) => this.handleAuth(r)));
  }

  login(email: string, password: string): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>('/api/auth/login', { email, password }, { withCredentials: true })
      .pipe(tap((r) => this.handleAuth(r)));
  }

  logout(): Observable<unknown> {
    return this.http.post('/api/auth/logout', {}, { withCredentials: true }).pipe(
      tap(() => {
        this._accessToken = null;
        this._user$.next(null);
        this.router.navigate(['/login']);
      }),
      catchError(() => {
        this._accessToken = null;
        this._user$.next(null);
        this.router.navigate(['/login']);
        return of(null);
      })
    );
  }

  /** Refresh the access token using the httpOnly cookie. */
  refresh(): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>('/api/auth/refresh', {}, { withCredentials: true })
      .pipe(tap((r) => this.handleAuth(r)));
  }

  /** Rehydrate session on app load — returns null if not logged in. */
  loadCurrentUser(): Observable<User | null> {
    return this.http
      .get<User>('/api/auth/me', { withCredentials: true })
      .pipe(
        tap((u) => this._user$.next(u)),
        catchError(() => {
          this._user$.next(null);
          return of(null);
        })
      );
  }

  private handleAuth(r: AuthResponse): void {
    this._accessToken = r.accessToken;
    this._user$.next(r.user);
  }
}
