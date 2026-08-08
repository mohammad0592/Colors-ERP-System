/**
 * Looking a word up in a table of words, safely.
 *
 * Every screen that turns a stored name into a readable one does the same thing: look
 * it up, and if it is not there show the raw name rather than hiding the line or
 * printing "undefined". A new role or a new kind of record must appear the day it
 * exists, even before anybody has written a sentence for it.
 *
 * The reason this is a function and not `words[key] ?? key`: every plain object
 * inherits `constructor`, `toString` and a handful of others, so the plain version
 * answers those with a **function**. TypeScript believes the type and says nothing, and
 * React renders a function as nothing at all — a blank cell where a word should be.
 */
export function wordFor(words: Record<string, string>, key: string): string {
  return Object.hasOwn(words, key) ? (words[key] ?? key) : key;
}
