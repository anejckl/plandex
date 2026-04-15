import {
  Component, Input, Output, EventEmitter, OnInit, inject, signal, computed
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { toSignal } from '@angular/core/rxjs-interop';
import { ApiService } from '../../core/api.service';
import { TimerService } from './timer.service';
import { CardDetail, Card, Label, ChecklistItem, Checklist, Assignee, BoardMember } from '../../shared/models';
import { DurationPipe } from '../../shared/duration.pipe';
import { MarkdownPipe } from '../../shared/markdown.pipe';

const LABEL_COLORS = [
  '#ef4444','#f97316','#f59e0b','#84cc16',
  '#10b981','#06b6d4','#3b82f6','#8b5cf6','#ec4899','#64748b',
];

@Component({
  selector: 'app-card-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, DurationPipe, MarkdownPipe],
  template: `
    <div class="modal-backdrop" (click)="onBackdropClick($event)">
      <div class="modal-panel mx-4" (click)="$event.stopPropagation()">
        @if (loading()) {
          <div class="p-6">
            <div class="skeleton h-6 w-2/3 mb-4"></div>
            <div class="skeleton h-4 w-full mb-2"></div>
            <div class="skeleton h-4 w-5/6"></div>
          </div>
        } @else if (card()) {
          <div class="p-6">

            <!-- Title -->
            @if (editingTitle()) {
              <input
                class="plandex-input text-lg font-semibold mb-1 w-full"
                [(ngModel)]="titleDraft"
                (blur)="saveTitle()"
                (keyup.enter)="saveTitle()"
                (keyup.escape)="editingTitle.set(false)"
                autofocus
              />
            } @else {
              <h2
                class="text-lg font-semibold text-text-primary mb-3 cursor-pointer hover:bg-surface rounded px-1 -mx-1"
                (click)="startEditTitle()"
              >{{ card()!.title }}</h2>
            }

            <!-- Labels section -->
            <div class="mb-4">
              <div class="flex items-center justify-between mb-2">
                <label class="text-xs font-medium text-text-secondary uppercase tracking-wide">Labels</label>
                <button class="plandex-btn-ghost text-xs px-2 py-0.5" (click)="toggleLabelPanel()">
                  {{ showLabelPanel() ? 'Done' : '+ Manage' }}
                </button>
              </div>

              <!-- Assigned labels -->
              <div class="flex flex-wrap gap-1 mb-2">
                @for (label of card()!.labels; track label.id) {
                  <span
                    class="px-2 py-0.5 rounded-full text-white text-xs font-medium flex items-center gap-1"
                    [style.background-color]="label.color"
                  >
                    {{ label.name }}
                    @if (showLabelPanel()) {
                      <button class="opacity-70 hover:opacity-100" (click)="removeLabel(label.id)">✕</button>
                    }
                  </span>
                }
                @if (card()!.labels.length === 0) {
                  <span class="text-xs text-text-muted">No labels</span>
                }
              </div>

              <!-- Label panel -->
              @if (showLabelPanel()) {
                <div class="border border-border rounded-lg p-3 bg-surface">
                  <p class="text-xs font-medium text-text-secondary mb-2">Board labels</p>
                  <div class="flex flex-wrap gap-1 mb-3">
                    @for (label of boardLabels(); track label.id) {
                      <div class="flex items-center gap-0.5">
                        <button
                          class="px-2 py-0.5 rounded-full text-white text-xs font-medium opacity-80 hover:opacity-100 transition-opacity"
                          [class.ring-2]="isAssigned(label.id)"
                          [class.ring-white]="isAssigned(label.id)"
                          [class.opacity-100]="isAssigned(label.id)"
                          [style.background-color]="label.color"
                          (click)="toggleLabel(label)"
                        >{{ label.name }} {{ isAssigned(label.id) ? '✓' : '' }}</button>
                        <button
                          class="text-text-muted hover:text-danger text-xs leading-none ml-0.5"
                          title="Delete label"
                          (click)="deleteLabel(label.id)"
                        >✕</button>
                      </div>
                    }
                    @if (boardLabels().length === 0) {
                      <span class="text-xs text-text-muted">No labels yet — create one below</span>
                    }
                  </div>

                  <!-- Create new label -->
                  @if (!showNewLabelForm()) {
                    <button class="plandex-btn-ghost text-xs" (click)="showNewLabelForm.set(true)">+ New label</button>
                  } @else {
                    <div class="flex flex-col gap-2">
                      <input
                        class="plandex-input text-sm"
                        placeholder="Label name"
                        [(ngModel)]="newLabelName"
                        (keyup.enter)="createLabel()"
                        autofocus
                      />
                      <div class="flex flex-wrap gap-1">
                        @for (color of labelColors; track color) {
                          <button
                            class="w-6 h-6 rounded-full border-2 transition-transform hover:scale-110"
                            [style.background-color]="color"
                            [class.border-gray-800]="newLabelColor === color"
                            [class.border-transparent]="newLabelColor !== color"
                            (click)="newLabelColor = color"
                          ></button>
                        }
                      </div>
                      <div class="flex gap-1">
                        <button class="plandex-btn-primary text-xs px-2 py-1" (click)="createLabel()">Create</button>
                        <button class="plandex-btn-ghost text-xs px-2 py-1" (click)="showNewLabelForm.set(false)">Cancel</button>
                      </div>
                    </div>
                  }
                </div>
              }
            </div>

            <!-- Assignees section -->
            <div class="mb-4">
              <div class="flex items-center justify-between mb-2">
                <label class="text-xs font-medium text-text-secondary uppercase tracking-wide">Assignees</label>
                <button class="plandex-btn-ghost text-xs px-2 py-0.5" (click)="toggleAssigneePanel()">
                  {{ showAssigneePanel() ? 'Done' : '+ Assign' }}
                </button>
              </div>

              <div class="flex flex-wrap gap-1 mb-2">
                @for (a of card()!.assignees; track a.userId) {
                  <span
                    class="px-2 py-0.5 rounded-full text-white text-xs font-medium flex items-center gap-1"
                    [style.background-color]="assigneeColor(a.userId)"
                    [title]="a.email"
                  >
                    <span class="font-semibold">{{ assigneeInitials(a.name) }}</span>
                    <span>{{ a.name }}</span>
                    @if (showAssigneePanel()) {
                      <button class="opacity-70 hover:opacity-100" (click)="unassign(a.userId)">✕</button>
                    }
                  </span>
                }
                @if (card()!.assignees.length === 0) {
                  <span class="text-xs text-text-muted">Nobody assigned</span>
                }
              </div>

              @if (showAssigneePanel()) {
                <div class="border border-border rounded-lg p-3 bg-surface">
                  <p class="text-xs font-medium text-text-secondary mb-2">Board members</p>
                  @if (unassignedMembers().length === 0) {
                    <p class="text-xs text-text-muted">Everyone is already assigned.</p>
                  }
                  <div class="flex flex-wrap gap-1">
                    @for (m of unassignedMembers(); track m.userId) {
                      <button
                        class="px-2 py-0.5 rounded-full text-white text-xs font-medium opacity-80 hover:opacity-100 flex items-center gap-1"
                        [style.background-color]="assigneeColor(m.userId)"
                        (click)="assign(m.userId)"
                      >
                        <span class="font-semibold">{{ assigneeInitials(m.name) }}</span>
                        <span>{{ m.name }}</span>
                      </button>
                    }
                  </div>
                </div>
              }
            </div>

            <!-- Due date -->
            <div class="mb-4">
              <label class="block text-xs font-medium text-text-secondary uppercase tracking-wide mb-1">Due date</label>
              <div class="flex items-center gap-2">
                <input
                  type="date"
                  class="plandex-input w-48"
                  [ngModel]="card()!.dueDate?.substring(0, 10) ?? ''"
                  (change)="saveDueDate($event)"
                />
                @if (card()!.dueDate) {
                  <button
                    class="text-text-muted hover:text-danger text-sm leading-none"
                    title="Clear due date"
                    (click)="clearDueDate()"
                  >✕</button>
                }
              </div>
            </div>

            <!-- Description -->
            <div class="mb-4">
              <div class="flex items-center justify-between mb-1">
                <label class="text-xs font-medium text-text-secondary uppercase tracking-wide">Description</label>
                @if (descriptionDraft) {
                  <button
                    class="plandex-btn-ghost text-xs px-2 py-0.5"
                    (click)="descPreview.set(!descPreview())"
                  >{{ descPreview() ? 'Edit' : 'Preview' }}</button>
                }
              </div>
              @if (descPreview()) {
                <div
                  class="prose-sm text-sm text-text-primary leading-relaxed border border-border rounded-lg p-3 bg-surface min-h-[4rem]"
                  [innerHTML]="descriptionDraft | markdown"
                ></div>
              } @else {
                <textarea
                  class="plandex-input resize-none"
                  rows="3"
                  placeholder="Add a description… (Markdown supported)"
                  [(ngModel)]="descriptionDraft"
                  (blur)="saveDescription()"
                ></textarea>
              }
            </div>

            <!-- Timer section -->
            <div class="mb-4 p-3 rounded-lg bg-surface border border-border">
              <div class="flex items-center justify-between">
                <span class="text-xs font-medium text-text-secondary uppercase tracking-wide">Time tracking</span>
                @if (isTimerActive()) {
                  <button class="plandex-btn-secondary text-xs" (click)="stopTimer()">Stop</button>
                } @else {
                  <button class="plandex-btn-primary text-xs" (click)="startTimer()">Start</button>
                }
              </div>
              <div class="mt-2 flex items-center gap-3">
                @if (isTimerActive()) {
                  <span class="flex items-center gap-1.5 text-timer font-mono text-sm font-medium">
                    <span class="timer-active-dot"></span>
                    {{ timerState()!.elapsedSeconds | duration }}
                  </span>
                }
                <span class="text-xs text-text-muted">Total: {{ totalLogged() | duration }}</span>
              </div>
              @if (card()!.timeEntries.length > 0) {
                <div class="mt-3 space-y-1">
                  @for (entry of card()!.timeEntries; track entry.id) {
                    <div class="flex items-center justify-between text-xs text-text-muted">
                      <span>{{ entry.startedAt | date:'MMM d, H:mm' }}
                        @if (entry.endedAt) { – {{ entry.endedAt | date:'H:mm' }} }
                        @else { <em class="text-timer">running</em> }
                      </span>
                      <div class="flex items-center gap-2">
                        @if (entry.durationSeconds != null) {
                          <span class="font-mono">{{ entry.durationSeconds | duration }}</span>
                        }
                        <button class="text-danger hover:text-red-700" (click)="deleteEntry(entry.id)" title="Delete">✕</button>
                      </div>
                    </div>
                  }
                </div>
              }
            </div>

            <!-- Checklists -->
            @for (checklist of card()!.checklists; track checklist.id) {
              <div class="mb-4">
                <div class="flex items-center justify-between mb-2">
                  <h3 class="text-sm font-semibold text-text-primary">{{ checklist.title }}</h3>
                  <div class="flex items-center gap-2">
                    <span class="text-xs text-text-muted">{{ doneCount(checklist.items) }}/{{ checklist.items.length }}</span>
                    <button class="text-xs text-text-muted hover:text-danger" (click)="deleteChecklist(checklist.id)" title="Delete checklist">✕</button>
                  </div>
                </div>
                @if (checklist.items.length > 0) {
                  <div class="h-1.5 bg-surface-board rounded-full mb-2 overflow-hidden">
                    <div
                      class="h-full bg-success rounded-full transition-all duration-300"
                      [style.width.%]="checklist.items.length ? (doneCount(checklist.items) / checklist.items.length) * 100 : 0"
                    ></div>
                  </div>
                }
                @for (item of checklist.items; track item.id) {
                  <div class="flex items-center gap-2 py-1 hover:bg-surface rounded px-1 -mx-1 group">
                    <input
                      type="checkbox"
                      class="rounded text-primary-600 shrink-0 cursor-pointer"
                      [checked]="item.isDone"
                      (change)="toggleItem(checklist.id, item)"
                    />
                    @if (editingItemId() === item.id) {
                      <input
                        class="plandex-input text-sm flex-1 py-0.5"
                        [(ngModel)]="editingItemText"
                        (blur)="saveItemEdit(checklist.id, item)"
                        (keyup.enter)="saveItemEdit(checklist.id, item)"
                        (keyup.escape)="editingItemId.set(null)"
                        autofocus
                      />
                    } @else {
                      <span
                        class="text-sm flex-1 cursor-pointer"
                        [class.line-through]="item.isDone"
                        [class.text-text-muted]="item.isDone"
                        (click)="startEditItem(item)"
                      >{{ item.text }}</span>
                    }
                    <button
                      class="text-xs text-text-muted opacity-0 group-hover:opacity-100 hover:text-danger shrink-0"
                      (click)="deleteChecklistItem(checklist.id, item.id)"
                    >✕</button>
                  </div>
                }

                <!-- Add item inline -->
                @if (addingItemTo() === checklist.id) {
                  <div class="mt-1 flex gap-1">
                    <input
                      class="plandex-input text-sm flex-1"
                      placeholder="Item text…"
                      [(ngModel)]="newItemText"
                      (keyup.enter)="addChecklistItem(checklist.id)"
                      (keyup.escape)="addingItemTo.set(null)"
                      autofocus
                    />
                    <button class="plandex-btn-primary text-xs px-2 py-1" (click)="addChecklistItem(checklist.id)">Add</button>
                    <button class="plandex-btn-ghost text-xs px-2 py-1" (click)="addingItemTo.set(null)">✕</button>
                  </div>
                } @else {
                  <button
                    class="mt-1 text-xs text-text-secondary hover:text-text-primary"
                    (click)="addingItemTo.set(checklist.id); newItemText = ''"
                  >+ Add item</button>
                }
              </div>
            }

            <!-- Add checklist -->
            @if (addingChecklist()) {
              <div class="mb-4 flex gap-1">
                <input
                  class="plandex-input text-sm flex-1"
                  placeholder="Checklist title…"
                  [(ngModel)]="newChecklistTitle"
                  (keyup.enter)="createChecklist()"
                  (keyup.escape)="addingChecklist.set(false)"
                  autofocus
                />
                <button class="plandex-btn-primary text-xs px-2 py-1" (click)="createChecklist()">Add</button>
                <button class="plandex-btn-ghost text-xs px-2 py-1" (click)="addingChecklist.set(false)">✕</button>
              </div>
            } @else {
              <button
                class="plandex-btn-ghost text-xs mb-4"
                (click)="addingChecklist.set(true); newChecklistTitle = ''"
              >+ Add checklist</button>
            }

            <!-- Actions -->
            <div class="flex justify-between pt-4 border-t border-border">
              <button class="plandex-btn-danger text-xs" (click)="archiveCard()">Archive</button>
              <div class="flex gap-2">
                <button class="plandex-btn-ghost text-xs" (click)="duplicateCard()">Duplicate</button>
                <button class="plandex-btn-ghost text-xs" (click)="close.emit()">Close</button>
              </div>
            </div>
          </div>
        }
      </div>
    </div>
  `,
})
export class CardDetailComponent implements OnInit {
  @Input({ required: true }) cardId!: number;
  @Input({ required: true }) boardId!: number;
  @Input() boardLabels = signal<Label[]>([]);
  @Input() boardMembers = signal<BoardMember[]>([]);
  @Input() currentUserId = 0;
  @Output() close = new EventEmitter<void>();
  @Output() cardDeleted = new EventEmitter<number>();
  @Output() cardUpdated = new EventEmitter<CardDetail>();
  @Output() labelCreated = new EventEmitter<Label>();
  @Output() labelDeleted = new EventEmitter<number>();
  @Output() cardDuplicated = new EventEmitter<Card>();
  @Output() cardAssigneesChanged = new EventEmitter<{ cardId: number; assignees: Assignee[] }>();

  private readonly api = inject(ApiService);
  private readonly timerService = inject(TimerService);

  card = signal<CardDetail | null>(null);
  loading = signal(true);
  editingTitle = signal(false);
  titleDraft = '';
  descriptionDraft = '';
  descPreview = signal(false);

  showLabelPanel = signal(false);
  showNewLabelForm = signal(false);
  newLabelName = '';
  newLabelColor = LABEL_COLORS[4];
  readonly labelColors = LABEL_COLORS;

  showAssigneePanel = signal(false);

  readonly unassignedMembers = computed(() => {
    const assignedIds = new Set((this.card()?.assignees ?? []).map((a) => a.userId));
    return this.boardMembers().filter((m) => !assignedIds.has(m.userId));
  });

  addingChecklist = signal(false);
  newChecklistTitle = '';

  addingItemTo = signal<number | null>(null);
  newItemText = '';

  editingItemId = signal<number | null>(null);
  editingItemText = '';

  readonly timerState = toSignal(this.timerService.active$, { initialValue: null });
  readonly isTimerActive = computed(() => this.timerState()?.cardId === this.cardId);

  readonly totalLogged = computed(() => {
    const c = this.card();
    if (!c) return 0;
    const closed = c.timeEntries
      .filter((e) => e.endedAt != null)
      .reduce((sum, e) => sum + (e.durationSeconds ?? 0), 0);
    const live = this.isTimerActive() ? (this.timerState()?.elapsedSeconds ?? 0) : 0;
    return closed + live;
  });

  ngOnInit(): void {
    this.loadCard();
  }

  private loadCard(): void {
    this.loading.set(true);
    this.api.get<CardDetail>(`/cards/${this.cardId}`).subscribe({
      next: (c) => {
        this.card.set(c);
        this.titleDraft = c.title;
        this.descriptionDraft = c.description ?? '';
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  onBackdropClick(e: MouseEvent): void {
    if ((e.target as HTMLElement).classList.contains('modal-backdrop')) {
      this.close.emit();
    }
  }

  startEditTitle(): void {
    this.titleDraft = this.card()!.title;
    this.editingTitle.set(true);
  }

  saveTitle(): void {
    const title = this.titleDraft.trim();
    if (!title || title === this.card()!.title) { this.editingTitle.set(false); return; }
    this.api.put<CardDetail>(`/cards/${this.cardId}`, { title }).subscribe((c) => {
      this.card.set(c);
      this.cardUpdated.emit(c);
      this.editingTitle.set(false);
    });
  }

  saveDescription(): void {
    const description = this.descriptionDraft;
    if (description === (this.card()!.description ?? '')) return;
    this.api.put<CardDetail>(`/cards/${this.cardId}`, { description }).subscribe((c) => {
      this.card.set(c);
      this.cardUpdated.emit(c);
    });
  }

  saveDueDate(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    const dueDate = value || null;
    this.api.put<CardDetail>(`/cards/${this.cardId}`, { dueDate }).subscribe((c) => {
      this.card.set(c);
      this.cardUpdated.emit(c);
    });
  }

  clearDueDate(): void {
    this.api.put<CardDetail>(`/cards/${this.cardId}`, { clearDueDate: true }).subscribe((c) => {
      this.card.set(c);
      this.cardUpdated.emit(c);
    });
  }

  // --- Labels ---
  toggleLabelPanel(): void {
    this.showLabelPanel.update((v) => !v);
    if (!this.showLabelPanel()) this.showNewLabelForm.set(false);
  }

  isAssigned(labelId: number): boolean {
    return (this.card()?.labels ?? []).some((l) => l.id === labelId);
  }

  toggleLabel(label: Label): void {
    if (this.isAssigned(label.id)) {
      this.removeLabel(label.id);
    } else {
      this.api.post(`/cards/${this.cardId}/labels/${label.id}`).subscribe(() => {
        this.card.update((c) => {
          if (!c) return c;
          const updated = { ...c, labels: [...c.labels, label] };
          this.cardUpdated.emit(updated);
          return updated;
        });
      });
    }
  }

  removeLabel(labelId: number): void {
    this.api.delete(`/cards/${this.cardId}/labels/${labelId}`).subscribe(() => {
      this.card.update((c) => {
        if (!c) return c;
        const updated = { ...c, labels: c.labels.filter((l) => l.id !== labelId) };
        this.cardUpdated.emit(updated);
        return updated;
      });
    });
  }

  createLabel(): void {
    const name = this.newLabelName.trim();
    if (!name) return;
    this.labelCreated.emit({ id: 0, boardId: this.boardId, name, color: this.newLabelColor });
    this.showNewLabelForm.set(false);
    this.newLabelName = '';
  }

  deleteLabel(labelId: number): void {
    if (!confirm('Delete this label from the board? It will be removed from all cards.')) return;
    this.api.delete(`/labels/${labelId}`).subscribe(() => {
      // Remove from this card's local labels if assigned
      this.card.update((c) => c ? { ...c, labels: c.labels.filter((l) => l.id !== labelId) } : c);
      this.labelDeleted.emit(labelId);
    });
  }

  // --- Checklists ---
  createChecklist(): void {
    const title = this.newChecklistTitle.trim();
    if (!title) return;
    this.api.post<Checklist>(`/cards/${this.cardId}/checklists`, { title }).subscribe((cl) => {
      this.card.update((c) => c ? { ...c, checklists: [...c.checklists, { ...cl, items: [] }] } : c);
      this.addingChecklist.set(false);
      this.newChecklistTitle = '';
    });
  }

  deleteChecklist(checklistId: number): void {
    if (!confirm('Delete this checklist and all its items?')) return;
    this.api.delete(`/checklists/${checklistId}`).subscribe(() => {
      this.card.update((c) => c ? { ...c, checklists: c.checklists.filter((ch) => ch.id !== checklistId) } : c);
    });
  }

  addChecklistItem(checklistId: number): void {
    const text = this.newItemText.trim();
    if (!text) return;
    this.api.post<ChecklistItem>(`/checklists/${checklistId}/items`, { text }).subscribe((item) => {
      this.card.update((c) => c ? {
        ...c,
        checklists: c.checklists.map((ch) =>
          ch.id === checklistId ? { ...ch, items: [...ch.items, item] } : ch
        ),
      } : c);
      this.newItemText = '';
      this.addingItemTo.set(null);
    });
  }

  startEditItem(item: ChecklistItem): void {
    this.editingItemId.set(item.id);
    this.editingItemText = item.text;
  }

  saveItemEdit(checklistId: number, item: ChecklistItem): void {
    const text = this.editingItemText.trim();
    this.editingItemId.set(null);
    if (!text || text === item.text) return;
    this.api.put<ChecklistItem>(`/checklist-items/${item.id}`, { text }).subscribe(() => {
      this.card.update((c) => c ? {
        ...c,
        checklists: c.checklists.map((ch) =>
          ch.id === checklistId
            ? { ...ch, items: ch.items.map((it) => it.id === item.id ? { ...it, text } : it) }
            : ch
        ),
      } : c);
    });
  }

  deleteChecklistItem(checklistId: number, itemId: number): void {
    this.api.delete(`/checklist-items/${itemId}`).subscribe(() => {
      this.card.update((c) => c ? {
        ...c,
        checklists: c.checklists.map((ch) =>
          ch.id === checklistId ? { ...ch, items: ch.items.filter((i) => i.id !== itemId) } : ch
        ),
      } : c);
    });
  }

  toggleItem(checklistId: number, item: ChecklistItem): void {
    this.api
      .put<ChecklistItem>(`/checklist-items/${item.id}`, { isDone: !item.isDone })
      .subscribe(() => {
        this.card.update((c) => c ? {
          ...c,
          checklists: c.checklists.map((ch) =>
            ch.id === checklistId
              ? { ...ch, items: ch.items.map((it) => it.id === item.id ? { ...it, isDone: !it.isDone } : it) }
              : ch
          ),
        } : c);
      });
  }

  // --- Timer ---
  startTimer(): void {
    this.timerService.start(this.cardId);
    setTimeout(() => this.loadCard(), 500);
  }

  stopTimer(): void {
    this.timerService.stop(this.cardId);
    setTimeout(() => this.loadCard(), 500);
  }

  deleteEntry(entryId: number): void {
    this.api.delete(`/time-entries/${entryId}`).subscribe(() => {
      this.card.update((c) => c ? { ...c, timeEntries: c.timeEntries.filter((e) => e.id !== entryId) } : c);
    });
  }

  archiveCard(): void {
    if (!confirm('Archive this card? You can restore it from the board\'s Archived view.')) return;
    this.api.delete(`/cards/${this.cardId}`).subscribe(() => {
      this.cardDeleted.emit(this.cardId);
    });
  }

  duplicateCard(): void {
    const c = this.card();
    if (!c) return;
    this.api.post<Card>(`/lists/${c.listId}/cards`, {
      title: `(Copy) ${c.title}`,
      description: c.description,
      dueDate: c.dueDate,
    }).subscribe((newCard) => {
      this.cardDuplicated.emit(newCard);
    });
  }

  doneCount(items: ChecklistItem[]): number {
    return items.filter((i) => i.isDone).length;
  }

  // --- Assignees ---
  toggleAssigneePanel(): void {
    this.showAssigneePanel.update((v) => !v);
  }

  assign(userId: number): void {
    this.api.post<Assignee>(`/cards/${this.cardId}/assignees`, { userId }).subscribe({
      next: (assignee) => {
        this.card.update((c) => {
          if (!c) return c;
          const updated = { ...c, assignees: [...c.assignees, assignee] };
          this.cardAssigneesChanged.emit({ cardId: c.id, assignees: updated.assignees });
          return updated;
        });
      },
    });
  }

  unassign(userId: number): void {
    this.api.delete(`/cards/${this.cardId}/assignees/${userId}`).subscribe({
      next: () => {
        this.card.update((c) => {
          if (!c) return c;
          const updated = { ...c, assignees: c.assignees.filter((a) => a.userId !== userId) };
          this.cardAssigneesChanged.emit({ cardId: c.id, assignees: updated.assignees });
          return updated;
        });
      },
    });
  }

  assigneeInitials(name: string): string {
    return name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((p) => p[0]?.toUpperCase() ?? '')
      .join('') || '?';
  }

  assigneeColor(userId: number): string {
    const hues = [210, 260, 340, 30, 160, 190, 290, 60];
    return `hsl(${hues[userId % hues.length]}, 55%, 50%)`;
  }
}
