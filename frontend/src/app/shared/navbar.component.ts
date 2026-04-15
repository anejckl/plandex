import { Component, inject, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AuthService } from '../core/auth.service';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <nav class="h-12 bg-white border-b border-border flex items-center px-4 gap-4 shrink-0">
      <a routerLink="/boards" class="font-bold text-primary-700 text-base tracking-tight">Plandex</a>

      @if (boardName) {
        <span class="text-text-muted">/</span>
        <span class="text-sm font-medium text-text-primary">{{ boardName }}</span>
      }

      <div class="ml-auto flex items-center gap-2">
        @if (auth.currentUser; as user) {
          <span class="text-sm text-text-secondary hidden sm:inline">{{ user.name }}</span>
          <button
            class="plandex-btn-ghost text-xs px-2 py-1"
            (click)="auth.logout().subscribe()"
          >
            Sign out
          </button>
        }
      </div>
    </nav>
  `,
})
export class NavbarComponent {
  readonly auth = inject(AuthService);
  @Input() boardName?: string;
}
