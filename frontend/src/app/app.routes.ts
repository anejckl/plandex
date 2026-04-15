import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';

export const routes: Routes = [
  { path: 'login',    loadComponent: () => import('./features/auth/login.component').then(m => m.LoginComponent) },
  { path: 'register', loadComponent: () => import('./features/auth/register.component').then(m => m.RegisterComponent) },
  {
    path: '',
    loadComponent: () => import('./shared/shell.component').then(m => m.ShellComponent),
    canActivate: [authGuard],
    children: [
      { path: 'boards',     loadComponent: () => import('./features/boards/boards-list.component').then(m => m.BoardsListComponent) },
      { path: 'boards/:id', loadComponent: () => import('./features/board-detail/board.component').then(m => m.BoardComponent) },
      { path: '',           redirectTo: 'boards', pathMatch: 'full' },
    ],
  },
  { path: '**', redirectTo: 'login' },
];
