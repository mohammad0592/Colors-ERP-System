/**
 * The colour a swatch is painted in, from the colour's name in Master Data — so a list of
 * rolls or bags can be read by colour at a glance, the way the floor sorts them.
 *
 * Matched on the name in either language, because the names are typed by people and
 * the factory's own are partly Arabic. A colour nobody has a paint for gets a neutral
 * grey with a dashed edge rather than a guess: a wrong colour on a swatch would be worse
 * than none.
 */

const paints: Record<string, string> = {
  white: '#ffffff',
  أبيض: '#ffffff',
  ابيض: '#ffffff',
  black: '#111827',
  أسود: '#111827',
  اسود: '#111827',
  green: '#16a34a',
  أخضر: '#16a34a',
  اخضر: '#16a34a',
  yellow: '#facc15',
  أصفر: '#facc15',
  اصفر: '#facc15',
  blue: '#2563eb',
  أزرق: '#2563eb',
  ازرق: '#2563eb',
  red: '#dc2626',
  أحمر: '#dc2626',
  orange: '#f97316',
  برتقالي: '#f97316',
  pink: '#f472b6',
  زهري: '#f472b6',
  purple: '#9333ea',
  بنفسجي: '#9333ea',
  brown: '#92400e',
  بني: '#92400e',
  grey: '#9ca3af',
  gray: '#9ca3af',
  رمادي: '#9ca3af',
  // محير — a mix, so it is painted as one.
  mixed: 'conic-gradient(#facc15, #16a34a, #2563eb, #f472b6, #facc15)',
  محير: 'conic-gradient(#facc15, #16a34a, #2563eb, #f472b6, #facc15)',
};

/** A CSS background for the colour, or null when there is no paint for that name. */
export function colourSwatch(name: string | null | undefined): string | null {
  if (name === null || name === undefined) {
    return null;
  }
  return paints[name.trim().toLowerCase()] ?? null;
}
