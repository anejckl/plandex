import { ApplicationConfig, provideBrowserGlobalErrorListeners, APP_INITIALIZER } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';

import { routes } from './app.routes';
import { authInterceptor } from './core/auth.interceptor';
import { AuthService } from './core/auth.service';
import { TimerService } from './features/board-detail/timer.service';
import { ActiveTimer } from './shared/models';
import { HttpClient } from '@angular/common/http';
import { catchError, of, switchMap, tap } from 'rxjs';

function initializeApp(auth: AuthService, timer: TimerService, http: HttpClient) {
  return () =>
    auth.loadCurrentUser().pipe(
      switchMap((user) => {
        if (!user) return of(null);
        return http
          .get<ActiveTimer | null>('/api/timer/active', { withCredentials: true })
          .pipe(catchError(() => of(null)));
      }),
      tap((activeTimer) => timer.rehydrate(activeTimer))
    );
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),
    {
      provide: APP_INITIALIZER,
      useFactory: initializeApp,
      deps: [AuthService, TimerService, HttpClient],
      multi: true,
    },
  ],
};
