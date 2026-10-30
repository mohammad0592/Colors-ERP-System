import type { TranslationKey } from '../../lib/i18n/en';
import type { ScreenPath } from '../../routes/access';
import type { ShiftLineDto } from './api';

/**
 * The three shift configuration screens, one per line (specification section 19.18).
 *
 * Kept apart from the screen itself because the shift window links to them too.
 */

export type ShiftConfigKind = 'extruder' | 'thermo' | 'recycler';

export interface ShiftConfigLine {
  key: ShiftConfigKind;
  label: TranslationKey;
  path: ScreenPath;
  matches: (line: ShiftLineDto) => boolean;
}

// Which line each screen is, read off what the line does — never its name, so a renamed
// line still lands on the right screen (specification section 4).
export const shiftConfigLines: ShiftConfigLine[] = [
  {
    key: 'extruder',
    label: 'shiftCfg.extruder',
    path: '/production/shift-config/extruder',
    matches: (line) => line.makesRolls,
  },
  {
    key: 'thermo',
    label: 'shiftCfg.thermo',
    path: '/production/shift-config/thermo',
    matches: (line) => line.formsBags,
  },
  {
    key: 'recycler',
    label: 'shiftCfg.recycler',
    path: '/production/shift-config/recycler',
    matches: (line) => line.recycles,
  },
];

/**
 * A line's own configuration screen, for links to it from elsewhere — the shift window
 * links to each line of its shift. Undefined for a line that is none of the three.
 */
export function shiftConfigFor(line: ShiftLineDto): ShiftConfigLine | undefined {
  return shiftConfigLines.find((kind) => kind.matches(line));
}
