import { HttpInterceptorFn, HttpRequest, HttpHandlerFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';
import { Router } from '@angular/router';

export const authInterceptor: HttpInterceptorFn = (
  req: HttpRequest<unknown>,
  next: HttpHandlerFn
) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const addToken = (r: HttpRequest<unknown>) => {
    const token = auth.accessToken;
    return token
      ? r.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : r;
  };

  // Never intercept auth endpoints themselves (avoid loops)
  const isAuthEndpoint =
    req.url.includes('/api/auth/refresh') ||
    req.url.includes('/api/auth/login') ||
    req.url.includes('/api/auth/register');

  return next(addToken(req)).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 401 && !isAuthEndpoint) {
        // Try refresh once, then retry original request
        return auth.refresh().pipe(
          switchMap(() => next(addToken(req))),
          catchError(() => {
            router.navigate(['/login']);
            return throwError(() => err);
          })
        );
      }
      return throwError(() => err);
    })
  );
};
