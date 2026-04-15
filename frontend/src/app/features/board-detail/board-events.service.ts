import { Injectable, OnDestroy } from '@angular/core';
import { Observable, Subject } from 'rxjs';

export interface BoardEvent {
  type: string;
  data: unknown;
}

@Injectable({ providedIn: 'root' })
export class BoardEventsService implements OnDestroy {
  private es: EventSource | null = null;
  private readonly events$ = new Subject<BoardEvent>();
  private currentBoardId: number | null = null;

  readonly events: Observable<BoardEvent> = this.events$.asObservable();

  connect(boardId: number): void {
    if (this.currentBoardId === boardId) return;
    this.disconnect();

    this.currentBoardId = boardId;
    const url = `/api/boards/${boardId}/events`;
    this.es = new EventSource(url, { withCredentials: true });

    const types = [
      'card-created', 'card-updated', 'card-deleted',
      'list-created', 'list-updated', 'list-deleted',
      'label-created', 'label-deleted', 'label-assigned', 'label-removed',
    ];

    for (const type of types) {
      this.es.addEventListener(type, (e: MessageEvent) => {
        try {
          this.events$.next({ type, data: JSON.parse(e.data) });
        } catch {
          // ignore malformed events
        }
      });
    }

    this.es.onerror = () => {
      // Browser will auto-reconnect for persistent EventSource errors;
      // we just swallow the noise here.
    };
  }

  disconnect(): void {
    this.es?.close();
    this.es = null;
    this.currentBoardId = null;
  }

  ngOnDestroy(): void {
    this.disconnect();
  }
}
