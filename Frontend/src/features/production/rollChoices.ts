/**
 * The recipe, product and colour last chosen when logging a roll, remembered on this
 * tablet (specification section 19.12).
 *
 * The extruder runs the same recipe, product and colour for hours, so each new roll opens
 * already set to what the last one was — across shifts too, since the machine does not
 * stop at a shift change. Kept on the tablet rather than the server because it is a
 * convenience, not a record: a tablet that cannot store it simply asks again.
 */

const key = 'colors.lastRollChoices';

export interface RollChoices {
  recipeVersionId?: number;
  productId?: number;
  colorId?: number;
}

export function readRollChoices(): RollChoices {
  try {
    const stored = window.localStorage.getItem(key);
    if (stored === null) {
      return {};
    }

    const parsed: unknown = JSON.parse(stored);
    return typeof parsed === 'object' && parsed !== null ? parsed : {};
  } catch {
    return {};
  }
}

export function saveRollChoices(choices: RollChoices): void {
  try {
    window.localStorage.setItem(key, JSON.stringify(choices));
  } catch {
    // Private browsing or a full store. The next roll asks again; nothing is lost.
  }
}
