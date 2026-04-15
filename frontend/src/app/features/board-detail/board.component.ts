import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  CdkDropList, CdkDrag, CdkDragDrop, CdkDragPlaceholder,
  moveItemInArray, transferArrayItem
} from '@angular/cdk/drag-drop';
import { ApiService } from '../../core/api.service';
import { TimerService } from './timer.service';
import { BoardDetail, Card, BoardList, Label } from '../../shared/models';
import { ListComponent } from './list.component';
import { CardDetailComponent } from './card-detail.component';
import { DurationPipe } from '../../shared/duration.pipe';
import { BoardEventsService } from './board-events.service';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-board',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    CdkDropList, CdkDrag, CdkDragPlaceholder,
    ListComponent, CardDetailComponent, DurationPipe,
  ],
  template: `
    @if (loading()) {
      <div class="p-6 flex gap-4">
        @for (i of [1,2,3]; track i) {
          <div class="skeleton w-72 h-48 rounded-lg shrink-0"></div>
        }
      </div>
    } @else if (board()) {
      <div class="flex flex-col h-full">

        <!-- Board header -->
        <div class="px-6 py-3 border-b border-border bg-white flex items-center gap-2 flex-wrap">

          <!-- Board name (inline edit) -->
          @if (editingBoardName()) {
            <input
              class="plandex-input text-sm font-bold w-48"
              [(ngModel)]="boardNameDraft"
              (blur)="saveBoardName()"
              (keyup.enter)="saveBoardName()"
              (keyup.escape)="editingBoardName.set(false)"
              autofocus
            />
          } @else {
            <h1
              class="font-bold text-text-primary cursor-pointer hover:text-primary-600"
              (click)="startEditBoardName()"
              title="Click to rename"
            >{{ board()!.name }}</h1>
          }

          <span class="text-text-muted text-sm">· {{ filteredLists().length }} lists</span>

          <!-- Add list -->
          @if (!showAddList()) {
            <button class="plandex-btn-ghost text-xs ml-2" (click)="showAddList.set(true)">+ Add list</button>
          } @else {
            <div class="flex items-center gap-1 ml-2">
              <input
                class="plandex-input text-sm w-40"
                placeholder="List name"
                [(ngModel)]="newListName"
                (keyup.enter)="addList()"
                (keyup.escape)="showAddList.set(false)"
                [ngModelOptions]="{standalone: true}"
                autofocus
              />
              <button class="plandex-btn-primary text-xs px-2 py-1" (click)="addList()">Add</button>
              <button class="plandex-btn-ghost text-xs px-2 py-1" (click)="showAddList.set(false)">✕</button>
            </div>
          }

          <!-- Search -->
          <div class="ml-auto flex items-center gap-2">
            <input
              class="plandex-input text-sm w-48 py-1"
              placeholder="🔍 Search cards…"
              [(ngModel)]="searchQuery"
              [ngModelOptions]="{standalone: true}"
            />
            <button
              class="plandex-btn-ghost text-xs"
              [class.text-primary-600]="showArchived()"
              (click)="toggleArchived()"
            >Archived</button>
            <!-- Delete board -->
            <button
              class="plandex-btn-ghost text-xs text-danger hover:bg-red-50"
              (click)="deleteBoard()"
            >Delete board</button>
          </div>
        </div>

        <!-- Stats bar -->
        <div class="px-6 py-1.5 border-b border-border bg-surface text-xs text-text-muted flex items-center gap-3">
          <span>{{ stats().totalCards }} cards</span>
          @if (stats().checklistTotal > 0) {
            <span>·</span>
            <span>{{ stats().checklistDone }}/{{ stats().checklistTotal }} checklist items</span>
          }
          @if (stats().totalSeconds > 0) {
            <span>·</span>
            <span>{{ stats().totalSeconds | duration }} logged</span>
          }
        </div>

        <!-- Lists scroll area with list-level drag -->
        <div class="flex-1 overflow-x-auto">
          <div class="flex gap-4 p-6 min-h-full">
            <!-- List drag wrapper -->
            <div
              cdkDropList
              cdkDropListOrientation="horizontal"
              [cdkDropListData]="board()!.lists"
              id="lists-container"
              class="flex gap-4"
              (cdkDropListDropped)="onListDrop($event)"
            >
              @for (list of filteredLists(); track list.id) {
                <div cdkDrag [cdkDragData]="list" cdkDragLockAxis="x">
                  <!-- Drag handle on list header -->
                  <div cdkDragHandle class="absolute top-2 left-1/2 -translate-x-1/2 w-8 h-1 bg-border rounded-full cursor-grab opacity-0 hover:opacity-60"></div>
                  <app-list
                    [list]="list"
                    [connectedTo]="cardListIds()"
                    (drop)="onCardDrop($event)"
                    (openCard)="openCardDetail($event)"
                    (addCard$)="onAddCard($event)"
                    (renamed)="onListRenamed($event)"
                    (deleted)="onListDeleted($event)"
                  />
                  <div *cdkDragPlaceholder class="w-72 h-32 rounded-lg bg-primary-100 border-2 border-dashed border-primary-300 shrink-0"></div>
                </div>
              }
            </div>
          </div>
        </div>

        <!-- Archived cards drawer -->
        @if (showArchived()) {
          <div class="border-t border-border bg-surface px-6 py-4 max-h-72 overflow-y-auto">
            <div class="flex items-center justify-between mb-3">
              <h3 class="text-sm font-semibold text-text-primary">Archived cards</h3>
              @if (archivedLoading()) {
                <span class="text-xs text-text-muted">Loading…</span>
              }
            </div>
            @if (archivedCards().length === 0 && !archivedLoading()) {
              <p class="text-sm text-text-muted">No archived cards.</p>
            }
            <div class="space-y-2">
              @for (card of archivedCards(); track card.id) {
                <div class="flex items-center justify-between bg-white rounded-lg border border-border px-3 py-2">
                  <div>
                    <p class="text-sm font-medium text-text-primary">{{ card.title }}</p>
                    <div class="flex flex-wrap gap-1 mt-1">
                      @for (label of card.labels; track label.id) {
                        <span
                          class="px-1.5 py-0.5 rounded-full text-white text-xs"
                          [style.background-color]="label.color"
                        >{{ label.name }}</span>
                      }
                    </div>
                  </div>
                  <div class="flex items-center gap-2 shrink-0 ml-4">
                    <button
                      class="plandex-btn-ghost text-xs"
                      (click)="restoreCard(card)"
                    >Restore</button>
                    <button
                      class="plandex-btn-ghost text-xs text-danger hover:bg-red-50"
                      (click)="purgeCard(card)"
                    >Delete forever</button>
                  </div>
                </div>
              }
            </div>
          </div>
        }
      </div>
    }

    <!-- Card detail modal -->
    @if (selectedCardId()) {
      <app-card-detail
        [cardId]="selectedCardId()!"
        [boardId]="board()!.id"
        [boardLabels]="boardLabels"
        (close)="selectedCardId.set(null)"
        (cardDeleted)="onCardDeleted($event)"
        (cardUpdated)="onCardUpdated($event)"
        (labelCreated)="onLabelCreated($event)"
        (labelDeleted)="onLabelDeleted($event)"
        (cardDuplicated)="onCardDuplicated($event)"
      />
    }
  `,
})
export class BoardComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(ApiService);
  readonly timerService = inject(TimerService);
  private readonly boardEvents = inject(BoardEventsService);
  private eventsSub?: Subscription;

  board = signal<BoardDetail | null>(null);
  loading = signal(true);
  selectedCardId = signal<number | null>(null);
  showAddList = signal(false);
  newListName = '';

  editingBoardName = signal(false);
  boardNameDraft = '';
  searchQuery = '';

  readonly boardLabels = signal<Label[]>([]);

  showArchived = signal(false);
  archivedCards = signal<Card[]>([]);
  archivedLoading = signal(false);

  readonly stats = computed(() => {
    const lists = this.board()?.lists ?? [];
    const allCards = lists.flatMap((l) => l.cards);
    return {
      totalCards: allCards.length,
      checklistDone: allCards.reduce((s, c) => s + (c.checklistDone ?? 0), 0),
      checklistTotal: allCards.reduce((s, c) => s + (c.checklistTotal ?? 0), 0),
      totalSeconds: allCards.reduce((s, c) => s + (c.totalLoggedSeconds ?? 0), 0),
    };
  });

  readonly filteredLists = computed(() => {
    const b = this.board();
    if (!b) return [];
    const q = this.searchQuery.trim().toLowerCase();
    if (!q) return b.lists;
    return b.lists.map((l) => ({
      ...l,
      cards: l.cards.filter((c) =>
        c.title.toLowerCase().includes(q) ||
        (c.labels ?? []).some((lb) => lb.name.toLowerCase().includes(q))
      ),
    }));
  });

  readonly cardListIds = computed(() =>
    this.filteredLists().map((l) => 'list-' + l.id)
  );

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.api.get<BoardDetail>(`/boards/${id}`).subscribe({
      next: (b) => {
        this.board.set(b);
        this.boardLabels.set(b.labels);
        this.loading.set(false);
        this.connectSSE(id);
      },
      error: () => this.loading.set(false),
    });
  }

  ngOnDestroy(): void {
    this.eventsSub?.unsubscribe();
    this.boardEvents.disconnect();
  }

  private connectSSE(boardId: number): void {
    this.boardEvents.connect(boardId);
    this.eventsSub = this.boardEvents.events.subscribe((ev) => {
      switch (ev.type) {
        case 'card-created':
          this.onSSECardCreated(ev.data as Card);
          break;
        case 'card-updated':
          this.onCardUpdated(ev.data as Card & { id: number; title: string });
          break;
        case 'card-deleted': {
          const p = ev.data as { cardId: number };
          this.onCardDeleted(p.cardId);
          break;
        }
        case 'list-created':
          this.onSSEListCreated(ev.data as BoardList);
          break;
        case 'list-updated':
          this.onSSEListUpdated(ev.data as BoardList);
          break;
        case 'list-deleted': {
          const p = ev.data as { listId: number };
          this.onListDeleted(p.listId);
          break;
        }
        case 'label-created':
          this.onSSELabelCreated(ev.data as Label);
          break;
        case 'label-deleted': {
          const p = ev.data as { labelId: number };
          this.onLabelDeleted(p.labelId);
          break;
        }
      }
    });
  }

  private onSSECardCreated(card: Card): void {
    this.board.update((b) => {
      if (!b) return b;
      const alreadyExists = b.lists.some((l) => l.cards.some((c) => c.id === card.id));
      if (alreadyExists) return b;
      return {
        ...b,
        lists: b.lists.map((l) => l.id === card.listId ? { ...l, cards: [...l.cards, card] } : l),
      };
    });
  }

  private onSSEListCreated(list: BoardList): void {
    this.board.update((b) => {
      if (!b) return b;
      if (b.lists.some((l) => l.id === list.id)) return b;
      return { ...b, lists: [...b.lists, { ...list, cards: [] }] };
    });
  }

  private onSSEListUpdated(list: BoardList): void {
    this.board.update((b) => b ? {
      ...b,
      lists: b.lists.map((l) => l.id === list.id ? { ...l, name: list.name } : l),
    } : b);
  }

  private onSSELabelCreated(label: Label): void {
    this.boardLabels.update((ls) => {
      if (ls.some((l) => l.id === label.id)) return ls;
      return [...ls, label];
    });
    this.board.update((b) => {
      if (!b || b.labels.some((l) => l.id === label.id)) return b;
      return { ...b, labels: [...b.labels, label] };
    });
  }

  // --- Board rename/delete ---
  startEditBoardName(): void {
    this.boardNameDraft = this.board()!.name;
    this.editingBoardName.set(true);
  }

  saveBoardName(): void {
    const name = this.boardNameDraft.trim();
    this.editingBoardName.set(false);
    if (!name || name === this.board()!.name) return;
    this.api.put(`/boards/${this.board()!.id}`, { name }).subscribe(() => {
      this.board.update((b) => b ? { ...b, name } : b);
    });
  }

  deleteBoard(): void {
    if (!confirm(`Delete board "${this.board()!.name}"? This will delete all lists, cards, and data inside it.`)) return;
    this.api.delete(`/boards/${this.board()!.id}`).subscribe(() => {
      this.router.navigate(['/boards']);
    });
  }

  // --- Lists ---
  addList(): void {
    const name = this.newListName.trim();
    if (!name) return;
    const boardId = this.board()!.id;
    this.api.post<BoardList>(`/boards/${boardId}/lists`, { name }).subscribe((list) => {
      this.board.update((b) => b ? { ...b, lists: [...b.lists, { ...list, cards: [] }] } : b);
      this.newListName = '';
      this.showAddList.set(false);
    });
  }

  onListRenamed({ listId, name }: { listId: number; name: string }): void {
    this.board.update((b) => b ? {
      ...b,
      lists: b.lists.map((l) => l.id === listId ? { ...l, name } : l),
    } : b);
  }

  onListDeleted(listId: number): void {
    this.board.update((b) => b ? { ...b, lists: b.lists.filter((l) => l.id !== listId) } : b);
  }

  // --- List drag to reorder ---
  onListDrop(event: CdkDragDrop<BoardList[]>): void {
    const lists = this.board()!.lists;
    moveItemInArray(lists, event.previousIndex, event.currentIndex);
    this.board.update((b) => b ? { ...b, lists: [...lists] } : b);
    const movedList = lists[event.currentIndex];
    this.api.put(`/lists/${movedList.id}`, { position: event.currentIndex + 1 }).subscribe();
  }

  // --- Cards ---
  openCardDetail(card: Card): void {
    this.selectedCardId.set(card.id);
  }

  onAddCard({ listId, title }: { listId: number; title: string }): void {
    this.api.post<Card>(`/lists/${listId}/cards`, { title }).subscribe((card) => {
      this.board.update((b) => b ? {
        ...b,
        lists: b.lists.map((l) => l.id === listId ? { ...l, cards: [...l.cards, card] } : l),
      } : b);
    });
  }

  onCardDrop(event: CdkDragDrop<Card[]>): void {
    const card: Card = event.item.data;
    const src = event.previousContainer;
    const tgt = event.container;

    if (src === tgt) {
      moveItemInArray(tgt.data, event.previousIndex, event.currentIndex);
    } else {
      transferArrayItem(src.data, tgt.data, event.previousIndex, event.currentIndex);
    }

    const targetListId = Number(tgt.id.replace('list-', ''));
    this.api.put<Card>(`/cards/${card.id}`, { listId: targetListId, position: event.currentIndex + 1 })
      .subscribe({
        error: () => {
          if (src === tgt) moveItemInArray(tgt.data, event.currentIndex, event.previousIndex);
          else transferArrayItem(tgt.data, src.data, event.currentIndex, event.previousIndex);
        },
      });
  }

  onCardDeleted(cardId: number): void {
    this.selectedCardId.set(null);
    this.board.update((b) => b ? {
      ...b,
      lists: b.lists.map((l) => ({ ...l, cards: l.cards.filter((c) => c.id !== cardId) })),
    } : b);
  }

  onCardUpdated(updated: { id: number; title: string; labels?: Label[] }): void {
    this.board.update((b) => b ? {
      ...b,
      lists: b.lists.map((l) => ({
        ...l,
        cards: l.cards.map((c) => c.id === updated.id ? { ...c, ...updated } : c),
      })),
    } : b);
  }

  // --- Labels ---
  onLabelCreated(partial: Label): void {
    const boardId = this.board()!.id;
    this.api.post<Label>(`/boards/${boardId}/labels`, { name: partial.name, color: partial.color })
      .subscribe((label) => {
        this.boardLabels.update((ls) => [...ls, label]);
        this.board.update((b) => b ? { ...b, labels: [...b.labels, label] } : b);
      });
  }

  onLabelDeleted(labelId: number): void {
    this.boardLabels.update((ls) => ls.filter((l) => l.id !== labelId));
    this.board.update((b) => b ? {
      ...b,
      labels: b.labels.filter((l) => l.id !== labelId),
      lists: b.lists.map((l) => ({
        ...l,
        cards: l.cards.map((c) => ({
          ...c,
          labels: (c.labels ?? []).filter((lb) => lb.id !== labelId),
        })),
      })),
    } : b);
  }

  onCardDuplicated(card: Card): void {
    this.board.update((b) => b ? {
      ...b,
      lists: b.lists.map((l) => l.id === card.listId ? { ...l, cards: [...l.cards, card] } : l),
    } : b);
  }

  // --- Archive ---
  toggleArchived(): void {
    const next = !this.showArchived();
    this.showArchived.set(next);
    if (next) this.loadArchived();
  }

  private loadArchived(): void {
    this.archivedLoading.set(true);
    this.api.get<Card[]>(`/boards/${this.board()!.id}/archived-cards`).subscribe({
      next: (cards) => { this.archivedCards.set(cards); this.archivedLoading.set(false); },
      error: () => this.archivedLoading.set(false),
    });
  }

  restoreCard(card: Card): void {
    this.api.post(`/cards/${card.id}/restore`, {}).subscribe(() => {
      this.archivedCards.update((cs) => cs.filter((c) => c.id !== card.id));
      this.board.update((b) => b ? {
        ...b,
        lists: b.lists.map((l) => l.id === card.listId ? { ...l, cards: [...l.cards, card] } : l),
      } : b);
    });
  }

  purgeCard(card: Card): void {
    if (!confirm(`Permanently delete "${card.title}"? This cannot be undone.`)) return;
    this.api.delete(`/cards/${card.id}/purge`).subscribe(() => {
      this.archivedCards.update((cs) => cs.filter((c) => c.id !== card.id));
    });
  }
}
