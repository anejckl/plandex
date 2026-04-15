import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="min-h-screen flex items-center justify-center bg-surface">
      <div class="w-full max-w-sm">
        <div class="text-center mb-8">
          <h1 class="text-2xl font-bold text-text-primary">Welcome back</h1>
          <p class="text-text-secondary mt-1">Sign in to Plandex</p>
        </div>

        <div class="bg-white rounded-xl shadow-card border border-border p-6">
          @if (error()) {
            <div class="mb-4 p-3 rounded-md bg-red-50 text-danger text-sm">
              {{ error() }}
            </div>
          }

          <form (ngSubmit)="submit()" #f="ngForm" class="space-y-4">
            <div>
              <label class="block text-sm font-medium text-text-primary mb-1">Email</label>
              <input
                type="email"
                name="email"
                [(ngModel)]="email"
                required
                class="plandex-input"
                placeholder="you@example.com"
              />
            </div>
            <div>
              <label class="block text-sm font-medium text-text-primary mb-1">Password</label>
              <input
                type="password"
                name="password"
                [(ngModel)]="password"
                required
                class="plandex-input"
                placeholder="••••••••"
              />
            </div>
            <button
              type="submit"
              class="plandex-btn-primary w-full justify-center py-2"
              [disabled]="loading()"
            >
              {{ loading() ? 'Signing in…' : 'Sign in' }}
            </button>
          </form>
        </div>

        <p class="text-center text-sm text-text-secondary mt-4">
          Don't have an account?
          <a routerLink="/register" class="text-primary-600 font-medium hover:underline">Register</a>
        </p>
      </div>
    </div>
  `,
})
export class LoginComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  email = '';
  password = '';
  loading = signal(false);
  error = signal<string | null>(null);

  submit(): void {
    if (!this.email || !this.password) return;
    this.loading.set(true);
    this.error.set(null);

    this.auth.login(this.email, this.password).subscribe({
      next: () => this.router.navigate(['/boards']),
      error: (err) => {
        this.loading.set(false);
        this.error.set(err.status === 401 ? 'Invalid email or password.' : 'Login failed. Please try again.');
      },
    });
  }
}
