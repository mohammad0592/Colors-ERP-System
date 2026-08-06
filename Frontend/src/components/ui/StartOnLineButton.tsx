import type { ReactElement } from 'react';

export interface StartableLine {
  shiftLineId: number;
  /** "Extruder", "Thermo" — what the button says when there is a choice to make. */
  lineName: string;
  /** "shift A, 06/08/2026" — which shift the work will be recorded against. */
  shiftLabel: string;
}

interface StartOnLineButtonProps {
  /** The lines this action can start on. Already filtered to open shifts. */
  lines: StartableLine[];
  /** "Start a batch", "Put a roll in", "Start a pallet". */
  action: string;
  onStart: (shiftLineId: number) => void;
}

/**
 * The button that starts work on a line.
 *
 * A button rather than a dropdown, because the factory runs <b>one shift at a time</b>
 * — A and B cannot both be working — so the list had exactly one option in it and cost
 * the operator two taps to pick the only thing available.
 *
 * It stays a list of buttons rather than assuming one, because two lines of the same
 * shift can both be open: a shift that runs the extruder and the thermo has a row for
 * each. One line is one button; two lines are two buttons, each naming its line.
 * Neither case hides anything behind a menu.
 *
 * The shift is printed under the button rather than inside it. The operator needs to
 * know what his work will be recorded against, but he is not choosing it.
 */
export function StartOnLineButton({
  lines,
  action,
  onStart,
}: StartOnLineButtonProps): ReactElement | null {
  if (lines.length === 0) {
    return null;
  }

  const only = lines.length === 1 ? lines[0] : null;

  if (only !== undefined && only !== null) {
    return (
      <div className="flex flex-col items-stretch gap-1 sm:items-end">
        <button
          type="button"
          className="h-touch rounded-control bg-brand-600 px-5 font-semibold text-white transition-colors hover:bg-brand-700 active:bg-brand-800"
          onClick={() => {
            onStart(only.shiftLineId);
          }}
        >
          {action}
        </button>
        <span className="text-xs text-ink-muted">
          {only.lineName} · {only.shiftLabel}
        </span>
      </div>
    );
  }

  return (
    <div className="flex flex-wrap gap-2">
      {lines.map((line) => (
        <div key={line.shiftLineId} className="flex flex-col items-stretch gap-1">
          <button
            type="button"
            className="h-touch rounded-control bg-brand-600 px-5 font-semibold text-white transition-colors hover:bg-brand-700 active:bg-brand-800"
            onClick={() => {
              onStart(line.shiftLineId);
            }}
          >
            {action} — {line.lineName}
          </button>
          <span className="text-xs text-ink-muted">{line.shiftLabel}</span>
        </div>
      ))}
    </div>
  );
}
