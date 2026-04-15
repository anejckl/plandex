import { Component, Input, inject, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { Card } from '../../shared/models';
import { DurationPipe } from '../../shared/duration.pipe';
import { TimerService } from './timer.service';

@Component({
  selector: 'app-card',
  standalone: true,
  imports: [CommonModule, DurationPipe],
  template: `
    <div
      class="plandex-card mb-2 fade-in overflow-hidden"
      [style.border-top]="accentColor() ? '3px solid ' + accentColor() : ''"
    >
      <div [class]="accentColor() ? 'p-3 pt-2' : 'p-3'">
        <!-- Labels row -->
        @if (card.labels.length > 0) {
          <div class="flex flex-wrap gap-1 mb-2">
            @for (label of card.labels; track label.id) {
              <span
                class="px-2 py-0.5 rounded-full text-white text-xs font-medium"
                [style.background-color]="label.color"
              >{{ label.name }}</span>
            }
          </div>
        }

        <!-- Title -->
        <p class="text-sm font-medium text-text-primary leading-snug">{{ card.title }}</p>

        <!-- Meta row -->
        <div class="flex items-center gap-2 mt-2 flex-wrap">
          <!-- Due date -->
          @if (card.dueDate) {
            <span
              class="text-xs px-1.5 py-0.5 rounded font-medium"
              [class]="dueDateClass()"
            >📅 {{ card.dueDate | date:'MMM d' }}</span>
          }

          <!-- Checklist progress -->
          @if (card.checklistTotal > 0) {
            <span class="text-xs text-text-muted">
              ✓ {{ card.checklistDone }}/{{ card.checklistTotal }}
            </span>
          }

          <!-- Assignee avatars -->
          @if ((card.assignees?.length ?? 0) > 0) {
            <div class="flex -space-x-1.5">
              @for (a of card.assignees; track a.userId) {
                <div
                  class="w-5 h-5 rounded-full ring-2 ring-white flex items-center justify-center text-white text-[9px] font-semibold"
                  [style.background-color]="avatarColor(a.userId)"
                  [title]="a.name"
                >{{ avatarInitials(a.name) }}</div>
              }
            </div>
          }

          <!-- Timer -->
          @if (isActive()) {
            <span class="flex items-center gap-1 text-xs text-timer font-mono ml-auto">
              <span class="timer-active-dot"></span>
              {{ timerState()!.elapsedSeconds | duration }}
            </span>
          } @else if (card.totalLoggedSeconds > 0) {
            <span class="text-xs text-text-muted ml-auto font-mono">
              {{ card.totalLoggedSeconds | duration }}
            </span>
          }
        </div>
      </div>
    </div>
  `,
})
export class CardComponent {
  @Input({ required: true }) card!: Card;

  private readonly timerService = inject(TimerService);

  readonly timerState = toSignal(this.timerService.active$, { initialValue: null });
  readonly isActive = computed(() => this.timerState()?.cardId === this.card?.id);
  readonly accentColor = computed(() => (this.card?.labels ?? [])[0]?.color ?? null);

  avatarInitials(name: string): string {
    return name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((p) => p[0]?.toUpperCase() ?? '')
      .join('') || '?';
  }

  avatarColor(userId: number): string {
    const hues = [210, 260, 340, 30, 160, 190, 290, 60];
    return `hsl(${hues[userId % hues.length]}, 55%, 50%)`;
  }

  dueDateClass(): string {
    if (!this.card.dueDate) return '';
    const due = new Date(this.card.dueDate);
    const now = new Date();
    const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
    const dueDay = new Date(due.getFullYear(), due.getMonth(), due.getDate());
    const diff = (dueDay.getTime() - today.getTime()) / 86400000;
    if (diff < 0) return 'bg-red-100 text-danger';          // overdue
    if (diff === 0) return 'bg-orange-100 text-orange-700'; // due today
    if (diff <= 2) return 'bg-yellow-100 text-yellow-700';  // due soon
    return 'text-text-muted';
  }
}
