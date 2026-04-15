import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { Board } from '../../shared/models';

@Component({
  selector: 'app-boards-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="p-6">
      <div class="max-w-6xl mx-auto">
        <div class="flex items-center justify-between mb-6">
          <h1 class="text-xl font-semibold text-text-primary">My Boards</h1>
        </div>

        @if (loading()) {
          <div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4">
            @for (i of [1,2,3,4]; track i) {
              <div class="skeleton h-24 rounded-card"></div>
            }
          </div>
        } @else {
          <div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4">
            @for (board of boards(); track board.id) {
              <div class="plandex-card p-4 h-24 flex flex-col justify-between group relative">
                <!-- Name (inline edit or click to open) -->
                @if (editingId() === board.id) {
                  <input
                    class="plandex-input text-sm font-medium w-full"
                    [(ngModel)]="editingName"
                    (blur)="saveName(board)"
                    (keyup.enter)="saveName(board)"
                    (keyup.escape)="editingId.set(null)"
                    autofocus
                  />
                } @else {
                  <span
                    class="font-medium text-text-primary cursor-pointer hover:text-primary-700 line-clamp-2 flex-1"
                    (click)="openBoard(board.id)"
                  >{{ board.name }}</span>
                }

                <!-- Actions row -->
                <div class="flex items-center justify-between mt-auto pt-1">
                  <span class="text-xs text-text-muted">{{ board.createdAt | date:'MMM d, y' }}</span>
                  <div class="flex gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                    <button
                      class="text-xs text-text-secondary hover:text-primary-600"
                      (click)="startEdit(board)"
                      title="Rename"
                    >✎</button>
                    <button
                      class="text-xs text-text-secondary hover:text-danger"
                      (click)="deleteBoard(board)"
                      title="Delete"
                    >✕</button>
                  </div>
                </div>
              </div>
            }

            <!-- Create board card -->
            @if (!showCreateForm()) {
              <button
                class="h-24 rounded-card border-2 border-dashed border-border text-text-secondary hover:border-primary-400 hover:text-primary-600 transition-colors flex items-center justify-center text-sm font-medium"
                (click)="showCreateForm.set(true)"
              >+ Create board</button>
            } @else {
              <div class="bg-white rounded-card border border-border p-3 shadow-card">
                <input
                  class="plandex-input mb-2 text-sm"
                  placeholder="Board name"
                  [(ngModel)]="newBoardName"
                  (keyup.enter)="createBoard()"
                  (keyup.escape)="cancelCreate()"
                  autofocus
                />
                <div class="flex gap-1">
                  <button class="plandex-btn-primary text-xs px-2 py-1" (click)="createBoard()">Create</button>
                  <button class="plandex-btn-ghost text-xs px-2 py-1" (click)="cancelCreate()">Cancel</button>
                </div>
              </div>
            }
          </div>

          @if (boards().length === 0 && !showCreateForm()) {
            <div class="text-center py-16">
              <p class="text-text-muted text-lg">No boards yet.</p>
              <p class="text-text-muted text-sm mt-1">Create your first board to get started.</p>
            </div>
          }
        }
      </div>
    </div>
  `,
})
export class BoardsListComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  boards = signal<Board[]>([]);
  loading = signal(true);
  showCreateForm = signal(false);
  newBoardName = '';

  editingId = signal<number | null>(null);
  editingName = '';

  ngOnInit(): void {
    this.api.get<Board[]>('/boards').subscribe({
      next: (b) => { this.boards.set(b); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  openBoard(id: number): void {
    this.router.navigate(['/boards', id]);
  }

  createBoard(): void {
    const name = this.newBoardName.trim();
    if (!name) return;
    this.api.post<Board>('/boards', { name }).subscribe((b) => {
      this.boards.update((list) => [...list, b]);
      this.cancelCreate();
    });
  }

  cancelCreate(): void {
    this.newBoardName = '';
    this.showCreateForm.set(false);
  }

  startEdit(board: Board): void {
    this.editingId.set(board.id);
    this.editingName = board.name;
  }

  saveName(board: Board): void {
    const name = this.editingName.trim();
    this.editingId.set(null);
    if (!name || name === board.name) return;
    this.api.put(`/boards/${board.id}`, { name }).subscribe(() => {
      this.boards.update((bs) => bs.map((b) => b.id === board.id ? { ...b, name } : b));
    });
  }

  deleteBoard(board: Board): void {
    if (!confirm(`Delete "${board.name}"? All its data will be permanently removed.`)) return;
    this.api.delete(`/boards/${board.id}`).subscribe(() => {
      this.boards.update((bs) => bs.filter((b) => b.id !== board.id));
    });
  }
}
