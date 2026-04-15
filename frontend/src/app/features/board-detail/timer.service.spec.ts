import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { vi, beforeEach, afterEach, describe, it, expect } from 'vitest';
import { TimerService } from './timer.service';
import { TimeEntry } from '../../shared/models';
import { firstValueFrom } from 'rxjs';

const makeEntry = (cardId: number, startedAt: string): TimeEntry => ({
  id: 1,
  cardId,
  userId: 1,
  startedAt,
  endedAt: null,
  durationSeconds: null,
});

describe('TimerService', () => {
  let service: TimerService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    vi.useFakeTimers();
    TestBed.configureTestingModule({
      providers: [
        TimerService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    service = TestBed.inject(TimerService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    service.ngOnDestroy();
    vi.useRealTimers();
  });

  it('starts with no active timer', async () => {
    const state = await firstValueFrom(service.active$);
    expect(state).toBeNull();
  });

  it('rehydrate sets active timer from server state', async () => {
    const startedAt = new Date(Date.now() - 5000).toISOString();
    service.rehydrate({ entryId: 1, cardId: 42, startedAt });

    const state = await firstValueFrom(service.active$);
    expect(state).not.toBeNull();
    expect(state!.cardId).toBe(42);
    expect(state!.elapsedSeconds).toBeGreaterThanOrEqual(4);
  });

  it('rehydrate with null clears timer', async () => {
    service.rehydrate({ entryId: 1, cardId: 42, startedAt: new Date().toISOString() });
    service.rehydrate(null);

    const state = await firstValueFrom(service.active$);
    expect(state).toBeNull();
  });

  it('start sets active timer and calls API', async () => {
    const startedAt = new Date().toISOString();
    service.start(5);

    const req = httpMock.expectOne('/api/cards/5/timer/start');
    req.flush(makeEntry(5, startedAt));

    // Allow the tap/subscribe to process
    await Promise.resolve();
    expect(service.currentState?.cardId).toBe(5);
  });

  it('starting second timer clears first locally', async () => {
    const t1 = new Date().toISOString();
    service.start(1);
    httpMock.expectOne('/api/cards/1/timer/start').flush(makeEntry(1, t1));
    await Promise.resolve();
    expect(service.currentState?.cardId).toBe(1);

    const t2 = new Date().toISOString();
    service.start(2);
    httpMock.expectOne('/api/cards/2/timer/start').flush(makeEntry(2, t2));
    await Promise.resolve();
    expect(service.currentState?.cardId).toBe(2);
  });

  it('stop clears the active timer', async () => {
    const startedAt = new Date().toISOString();
    service.start(3);
    httpMock.expectOne('/api/cards/3/timer/start').flush(makeEntry(3, startedAt));
    await Promise.resolve();

    service.stop(3);
    httpMock.expectOne('/api/cards/3/timer/stop').flush({
      ...makeEntry(3, startedAt),
      endedAt: new Date().toISOString(),
      durationSeconds: 1,
    });
    await Promise.resolve();

    expect(service.currentState).toBeNull();
  });

  it('tick increments elapsed seconds', async () => {
    const startedAt = new Date(Date.now() - 1000).toISOString();
    service.start(7);
    httpMock.expectOne('/api/cards/7/timer/start').flush(makeEntry(7, startedAt));
    await Promise.resolve();

    const before = service.currentState!.elapsedSeconds;
    vi.advanceTimersByTime(2000);
    const after = service.currentState!.elapsedSeconds;
    expect(after).toBeGreaterThan(before);
  });
});
