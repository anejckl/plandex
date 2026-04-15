import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { Component } from '@angular/core';
import { AuthService } from './auth.service';
import { AuthResponse, User } from '../shared/models';
import { firstValueFrom } from 'rxjs';

@Component({ template: '', standalone: true })
class StubComponent {}

const mockUser: User = { id: 1, email: 'a@test.com', name: 'Alice' };
const mockAuth: AuthResponse = { accessToken: 'tok123', user: mockUser };

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        AuthService,
        provideRouter([{ path: 'login', component: StubComponent }]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('stores access token and user on login', () => {
    service.login('a@test.com', 'password').subscribe();
    const req = httpMock.expectOne('/api/auth/login');
    req.flush(mockAuth);

    expect(service.accessToken).toBe('tok123');
    expect(service.currentUser).toEqual(mockUser);
  });

  it('stores user on register', () => {
    service.register('a@test.com', 'password', 'Alice').subscribe();
    const req = httpMock.expectOne('/api/auth/register');
    req.flush(mockAuth);

    expect(service.currentUser?.email).toBe('a@test.com');
  });

  it('clears user and token on logout', () => {
    service.login('a@test.com', 'password').subscribe();
    httpMock.expectOne('/api/auth/login').flush(mockAuth);

    service.logout().subscribe();
    httpMock.expectOne('/api/auth/logout').flush({});

    expect(service.accessToken).toBeNull();
    expect(service.currentUser).toBeNull();
  });

  it('emits null user initially', async () => {
    const user = await firstValueFrom(service.user$);
    expect(user).toBeNull();
  });

  it('loadCurrentUser sets user on success', () => {
    service.loadCurrentUser().subscribe();
    const req = httpMock.expectOne('/api/auth/me');
    req.flush(mockUser);

    expect(service.currentUser).toEqual(mockUser);
  });

  it('loadCurrentUser emits null on 401', async () => {
    const result$ = service.loadCurrentUser();
    const p = firstValueFrom(result$);
    httpMock.expectOne('/api/auth/me').flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });
    const result = await p;
    expect(result).toBeNull();
    expect(service.currentUser).toBeNull();
  });
});
