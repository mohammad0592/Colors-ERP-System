import { describe, expect, it } from 'vitest';
import { colourSwatch } from './colourSwatch';

describe('colourSwatch', () => {
  it('paints a colour by its name, whatever the case or spacing', () => {
    expect(colourSwatch(' Blue ')).toBe(colourSwatch('blue'));
    expect(colourSwatch('blue')).not.toBeNull();
  });

  it('knows the Arabic names too', () => {
    expect(colourSwatch('أبيض')).toBe(colourSwatch('White'));
  });

  it('does not guess at a colour it has no paint for', () => {
    expect(colourSwatch('Champagne')).toBeNull();
    expect(colourSwatch(null)).toBeNull();
  });
});
