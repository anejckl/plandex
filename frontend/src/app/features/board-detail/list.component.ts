import { Component, Input, Output, EventEmitter, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CdkDropList, CdkDrag, CdkDragDrop, CdkDragPlaceholder } from '@angular/cdk/drag-drop';
import { ApiService } from '../../core/api.service';
import { BoardList, Card } from '../../shared/models';
import { CardComponent } from './card.component';

@Component({
  selector: 'app-list',
  standalone: true,
  imports: [CommonModule, FormsModule, CdkDropList, CdkDrag, CdkDragPlaceholder, CardComponent],
  template: `
    <div class="plandex-list">
      <!-- Header -->
      <div class="flex items-center justify-between px-1 pb-2 group">
        @if (editingName()) {
          <input
            class="plandex-input text-sm font-semibold flex-1 py-0.5 px-1 h-auto"
            [(ngModel)]="nameDraft"
            (blur)="saveName()"
            (keyup.enter)="saveName()"
            (keyup.escape)="editingName.set(false)"
            autofocus
          />
        } @else {
          <span
            class="text-sm font-semibold text-text-primary cursor-pointer hover:text-primary-600 flex-1 truncate"
            (click)="startEdit()"
          >{{ list.name }}</span>
        }
        <div class="flex items-center gap-1 shrink-0">
          <span class="text-xs text-text-muted bg-white rounded-full px-1.5 py-0.5 border border-border">
            {{ list.cards.length }}
          </span>
          <button
            class="text-xs text-text-muted opacity-0 group-hover:opacity-100 hover:text-danger transition-opacity ml-1"
            (click)="onDelete()"
            title="Delete list"
          >✕</button>
        </div>
      </div>

      <!-- Cards drop zone -->
      <div
        cdkDropList
        [cdkDropListData]="list.cards"
        [cdkDropListConnectedTo]="connectedTo"
        [id]="'list-' + list.id"
        class="flex flex-col min-h-[4px]"
        (cdkDropListDropped)="drop.emit($event)"
      >
        @for (card of list.cards; track card.id) {
          <div cdkDrag [cdkDragData]="card" (click)="openCard.emit(card)">
            <app-card [card]="card" />
            <div *cdkDragPlaceholder class="h-16 rounded-card bg-primary-100 border-2 border-dashed border-primary-300 mb-2"></div>
          </div>
        }
      </div>

      <!-- Add card -->
      @if (!showAddCard()) {
        <button
          class="w-full text-left text-xs text-text-secondary px-1 py-1.5 hover:text-text-primary hover:bg-white/50 rounded transition-colors"
          (click)="showAddCard.set(true)"
        >+ Add card</button>
      } @else {
        <div class="bg-white rounded-card border border-border p-2 mt-1 shadow-card">
          <textarea
            class="plandex-input text-sm resize-none mb-2"
            rows="2"
            placeholder="Card title…"
            [(ngModel)]="newCardTitle"
            (keyup.enter)="addCard()"
            (keyup.escape)="cancelAdd()"
            autofocus
          ></textarea>
          <div class="flex gap-1">
            <button class="plandex-btn-primary text-xs px-2 py-1" (click)="addCard()">Add</button>
            <button class="plandex-btn-ghost text-xs px-2 py-1" (click)="cancelAdd()">Cancel</button>
          </div>
        </div>
      }
    </div>
  `,
})
export class ListComponent {
  @Input({ required: true }) list!: BoardList;
  @Input() connectedTo: string[] = [];
  @Output() drop = new EventEmitter<CdkDragDrop<Card[]>>();
  @Output() openCard = new EventEmitter<Card>();
  @Output() addCard$ = new EventEmitter<{ listId: number; title: string }>();
  @Output() renamed = new EventEmitter<{ listId: number; name: string }>();
  @Output() deleted = new EventEmitter<number>();

  private readonly api = inject(ApiService);

  showAddCard = signal(false);
  newCardTitle = '';
  editingName = signal(false);
  nameDraft = '';

  startEdit(): void {
    this.nameDraft = this.list.name;
    this.editingName.set(true);
  }

  saveName(): void {
    const name = this.nameDraft.trim();
    this.editingName.set(false);
    if (!name || name === this.list.name) return;
    this.api.put(`/lists/${this.list.id}`, { name }).subscribe(() => {
      this.renamed.emit({ listId: this.list.id, name });
    });
  }

  onDelete(): void {
    if (!confirm(`Delete list "${this.list.name}" and all its cards?`)) return;
    this.api.delete(`/lists/${this.list.id}`).subscribe(() => {
      this.deleted.emit(this.list.id);
    });
  }

  addCard(): void {
    const title = this.newCardTitle.trim();
    if (!title) return;
    this.addCard$.emit({ listId: this.list.id, title });
    this.cancelAdd();
  }

  cancelAdd(): void {
    this.newCardTitle = '';
    this.showAddCard.set(false);
  }
}
