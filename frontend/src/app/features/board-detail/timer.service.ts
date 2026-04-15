import { Injectable, inject, OnDestroy } from '@angular/core';
import { BehaviorSubject, interval, Subscription } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { ActiveTimer, TimeEntry } from '../../shared/models';

export interface ActiveTimerState {
  cardId: number;
  startedAt: Date;
  elapsedSeconds: number;
}

@Injectable({ providedIn: 'root' })
export class TimerService implements OnDestroy {
  private readonly api = inject(ApiService);

  private readonly _active$ = new BehaviorSubject<ActiveTimerState | null>(null);
  readonly active$ = this._active$.asObservable();

  private tickSub: Subscription | null = null;

  /** Called from APP_INITIALIZER or AuthService after login. */
  rehydrate(timer: ActiveTimer | null | undefined): void {
    if (timer) {
      this.setActive(timer.cardId, new Date(timer.startedAt));
    } else {
      this.clearActive();
    }
  }

  start(cardId: number): void {
    this.api.post<TimeEntry>(`/cards/${cardId}/timer/start`).subscribe((entry) => {
      // If there was a previously active card, it was stopped server-side; clear locally too
      this.setActive(cardId, new Date(entry.startedAt));
    });
  }

  stop(cardId: number): void {
    this.api.post<TimeEntry>(`/cards/${cardId}/timer/stop`).subscribe(() => {
      this.clearActive();
    });
  }

  get currentState(): ActiveTimerState | null {
    return this._active$.value;
  }

  private setActive(cardId: number, startedAt: Date): void {
    this.clearActive();
    const state: ActiveTimerState = {
      cardId,
      startedAt,
      elapsedSeconds: Math.floor((Date.now() - startedAt.getTime()) / 1000),
    };
    this._active$.next(state);

    this.tickSub = interval(1000).subscribe(() => {
      const current = this._active$.value;
      if (current) {
        this._active$.next({
          ...current,
          elapsedSeconds: Math.floor((Date.now() - current.startedAt.getTime()) / 1000),
        });
      }
    });
  }

  private clearActive(): void {
    this.tickSub?.unsubscribe();
    this.tickSub = null;
    this._active$.next(null);
  }

  ngOnDestroy(): void {
    this.clearActive();
  }
}
