import { describe, it, expect } from 'vitest';
import { DurationPipe } from './duration.pipe';

describe('DurationPipe', () => {
  const pipe = new DurationPipe();

  it.each([
    [0,     '0:00:00'],
    [59,    '0:00:59'],
    [60,    '0:01:00'],
    [3600,  '1:00:00'],
    [3661,  '1:01:01'],
    [5025,  '1:23:45'],
    [86399, '23:59:59'],
    [-5,    '0:00:00'],  // negative clamped to 0
  ])('transforms %i seconds to %s', (input, expected) => {
    expect(pipe.transform(input)).toBe(expected);
  });
});
