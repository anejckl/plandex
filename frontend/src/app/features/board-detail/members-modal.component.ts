import { Component, EventEmitter, Input, Output, OnInit, inject, signal, computed, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ApiService } from '../../core/api.service';
import { BoardMember } from '../../shared/models';

@Component({
  selector: 'app-members-modal',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="modal-backdrop" (click)="onBackdropClick($event)">
      <div class="modal-panel max-w-md w-full mx-4" (click)="$event.stopPropagation()">
        <div class="p-6">
          <div class="flex items-center justify-between mb-4">
            <h2 class="text-lg font-semibold text-text-primary">Board members</h2>
            <button class="text-text-muted hover:text-text-primary" (click)="close.emit()">✕</button>
          </div>

          <!-- Add member (owner only) -->
          @if (isOwner()) {
            <form class="mb-4" (submit)="$event.preventDefault(); addMember()">
              <label class="block text-xs font-medium text-text-secondary uppercase tracking-wide mb-1">
                Invite by email
              </label>
              <div class="flex gap-2">
                <input
                  type="email"
                  class="plandex-input text-sm flex-1"
                  placeholder="teammate@example.com"
                  [(ngModel)]="emailDraft"
                  name="email"
                  [disabled]="adding()"
                />
                <button
                  type="submit"
                  class="plandex-btn-primary text-xs px-3"
                  [disabled]="adding() || !emailDraft.trim()"
                >Add</button>
              </div>
              @if (addError()) {
                <p class="mt-1 text-xs text-danger">{{ addError() }}</p>
              }
              <p class="mt-1 text-xs text-text-muted">
                User must already have a plandex account.
              </p>
            </form>
          }

          <!-- Members list -->
          <div class="space-y-2">
            @for (m of sortedMembers(); track m.userId) {
              <div class="flex items-center gap-3 p-2 rounded-lg hover:bg-surface">
                <div
                  class="w-8 h-8 rounded-full flex items-center justify-center text-white text-xs font-semibold shrink-0"
                  [style.background-color]="avatarColor(m.userId)"
                >{{ initials(m.name) }}</div>
                <div class="flex-1 min-w-0">
                  <p class="text-sm font-medium text-text-primary truncate">
                    {{ m.name }}
                    @if (m.userId === currentUserId) { <span class="text-text-muted text-xs">(you)</span> }
                  </p>
                  <p class="text-xs text-text-muted truncate">{{ m.email }}</p>
                </div>
                <span
                  class="text-xs px-2 py-0.5 rounded-full shrink-0"
                  [class.bg-primary-100]="m.role === 'Owner'"
                  [class.text-primary-700]="m.role === 'Owner'"
                  [class.bg-surface-board]="m.role === 'Member'"
                  [class.text-text-secondary]="m.role === 'Member'"
                >{{ m.role }}</span>

                @if (canRemove(m)) {
                  <button
                    class="text-xs text-text-muted hover:text-danger shrink-0"
                    [disabled]="removing() === m.userId"
                    (click)="removeMember(m)"
                    [title]="m.userId === currentUserId ? 'Leave board' : 'Remove member'"
                  >{{ m.userId === currentUserId ? 'Leave' : 'Remove' }}</button>
                }
              </div>
            }
          </div>
        </div>
      </div>
    </div>
  `,
})
export class MembersModalComponent implements OnInit {
  @Input({ required: true }) boardId!: number;
  @Input({ required: true }) currentUserId!: number;
  // Signal input so the computed below reactively tracks changes when the
  // parent's boardMembers signal updates (e.g. via SSE member-removed).
  readonly members = input.required<BoardMember[]>();
  @Output() close = new EventEmitter<void>();
  @Output() memberAdded = new EventEmitter<BoardMember>();
  @Output() memberRemoved = new EventEmitter<number>();

  private readonly api = inject(ApiService);

  readonly sortedMembers = computed(() =>
    [...this.members()].sort((a, b) => {
      if (a.role !== b.role) return a.role === 'Owner' ? -1 : 1;
      return a.name.localeCompare(b.name);
    })
  );

  readonly isOwner = computed(() =>
    this.members().some((m) => m.userId === this.currentUserId && m.role === 'Owner')
  );

  emailDraft = '';
  adding = signal(false);
  addError = signal<string | null>(null);
  removing = signal<number | null>(null);

  ngOnInit(): void {
    // NOTE: members is passed from parent as a signal input; no fetch needed.
  }

  canRemove(m: BoardMember): boolean {
    if (this.isOwner()) return true;
    return m.userId === this.currentUserId;
  }

  addMember(): void {
    const email = this.emailDraft.trim();
    if (!email) return;
    this.adding.set(true);
    this.addError.set(null);
    this.api
      .post<BoardMember>(`/boards/${this.boardId}/members`, { email })
      .subscribe({
        next: (m) => {
          this.memberAdded.emit(m);
          this.emailDraft = '';
          this.adding.set(false);
        },
        error: (err: HttpErrorResponse) => {
          this.adding.set(false);
          if (err.status === 404) this.addError.set('No user with that email.');
          else if (err.status === 409) this.addError.set('Already a member.');
          else if (err.status === 403) this.addError.set('Only the owner can add members.');
          else this.addError.set('Could not add member.');
        },
      });
  }

  removeMember(m: BoardMember): void {
    const isSelf = m.userId === this.currentUserId;
    const msg = isSelf
      ? 'Leave this board? You will lose access immediately.'
      : `Remove ${m.name} from this board?`;
    if (!confirm(msg)) return;

    this.removing.set(m.userId);
    this.api.delete(`/boards/${this.boardId}/members/${m.userId}`).subscribe({
      next: () => {
        this.removing.set(null);
        this.memberRemoved.emit(m.userId);
      },
      error: (err: HttpErrorResponse) => {
        this.removing.set(null);
        if (err.status === 409) alert('Cannot remove the last owner of a board.');
        else alert('Could not remove member.');
      },
    });
  }

  onBackdropClick(e: MouseEvent): void {
    if ((e.target as HTMLElement).classList.contains('modal-backdrop')) {
      this.close.emit();
    }
  }

  initials(name: string): string {
    return name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((p) => p[0]?.toUpperCase() ?? '')
      .join('') || '?';
  }

  // Deterministic pastel color per user id so avatars stay consistent.
  avatarColor(userId: number): string {
    const hues = [210, 260, 340, 30, 160, 190, 290, 60];
    return `hsl(${hues[userId % hues.length]}, 55%, 50%)`;
  }
}
