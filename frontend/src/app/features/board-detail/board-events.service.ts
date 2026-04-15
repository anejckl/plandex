import { Injectable, OnDestroy, inject } from '@angular/core';
import { Observable, Subject } from 'rxjs';
import { AuthService } from '../../core/auth.service';

export interface BoardEvent {
  type: string;
  data: unknown;
}

@Injectable({ providedIn: 'root' })
export class BoardEventsService implements OnDestroy {
  private readonly auth = inject(AuthService);
  private es: EventSource | null = null;
  private readonly events$ = new Subject<BoardEvent>();
  private currentBoardId: number | null = null;
  // One-shot guard against reconnect storms: only try to refresh the token
  // once per connect() call. Reset on a clean disconnect() so the next
  // connect() starts fresh.
  private refreshAttempted = false;

  readonly events: Observable<BoardEvent> = this.events$.asObservable();

  connect(boardId: number): void {
    if (this.currentBoardId === boardId) return;
    this.disconnect();

    const token = this.auth.accessToken;
    if (!token) return;

    this.currentBoardId = boardId;
    // EventSource can't set Authorization headers, so the backend accepts
    // ?access_token= on SSE paths only (scoped in Program.cs).
    const url = `/api/boards/${boardId}/events?access_token=${encodeURIComponent(token)}`;
    this.es = new EventSource(url, { withCredentials: true });

    const types = [
      'board-updated',
      'card-created', 'card-updated', 'card-deleted',
      'list-created', 'list-updated', 'list-deleted',
      'label-created', 'label-deleted', 'label-assigned', 'label-removed',
      'member-added', 'member-removed',
      'card-assigned', 'card-unassigned',
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
      // The native EventSource retries transient disconnects on its own and
      // flips to readyState=CONNECTING during those. We only act when it
      // has given up (readyState=CLOSED), which in practice means either the
      // ?access_token= expired (401) or the server is gone for good.
      if (this.es?.readyState !== EventSource.CLOSED) return;
      if (this.refreshAttempted) return;
      this.refreshAttempted = true;

      const boardIdToResume = this.currentBoardId;
      this.auth.refresh().subscribe({
        next: () => {
          // Fully tear down before reconnecting so connect()'s same-board
          // early-return doesn't skip us.
          this.disconnect();
          if (boardIdToResume !== null) this.connect(boardIdToResume);
        },
        error: () => {
          // Refresh failed — user session is dead. Drop the stream; the
          // auth interceptor will bounce them to /login on the next HTTP
          // call they try to make.
          this.disconnect();
        },
      });
    };
  }

  disconnect(): void {
    this.es?.close();
    this.es = null;
    this.currentBoardId = null;
    this.refreshAttempted = false;
  }

  ngOnDestroy(): void {
    this.disconnect();
  }
}
