import { useEffect, useId, useRef, useState, type ReactElement } from 'react';
import {
  currentEnvironment,
  startScanning,
  unavailableReason,
  type EntryMethod,
  type ScannerHandle,
} from '../../lib/barcodeScanner';
import { Modal } from './Modal';
import { useTranslation } from '../../hooks/useTranslation';
import { colourSwatch } from '../../lib/colourSwatch';

/** One thing the field can be filled from without scanning or typing. */
export interface ScanOption {
  /** The code that goes in the box — a barcode, or a roll code. */
  value: string;
  /** Everything about it in one line. Searched as the man types, and read by screen readers. */
  label: string;
  /** The code as it is written on the label, where that differs from the barcode's capitals. */
  code?: string | undefined;
  /** What it is, in a few words: "Absorbent Big Plate". */
  title?: string | undefined;
  /** Its colour's name, painted as a swatch so the list reads by colour at a glance. */
  colour?: string | undefined;
  /** Short facts in small chips: the roll, the date, the weight. */
  details?: string[] | undefined;
  /** Something to notice before choosing it — "Not completed". */
  flag?: string | undefined;
}

interface ScanFieldProps {
  /** Wording above the box: "Scan a bag", "Scan a pallet". */
  label: string;
  value: string;
  onChange: (value: string) => void;
  /**
   * Called when a code is settled on — scanned, or Enter pressed on a typed one, or one
   * chosen from the list. The entry method comes with it, because section 12 asks that
   * typing be marked and only this component knows which happened.
   */
  onSubmit: (value: string, entry: EntryMethod) => void;
  /** What the list offers. Leave it out and the field is scan-or-type only. */
  options?: ScanOption[];
  /** Wording for the list, e.g. "only Black Big Plate fits this pallet". */
  optionsHint?: string;
  placeholder?: string;
  disabled?: boolean;
  /** Wording on the button that acts on a typed code. */
  submitLabel?: string;
  busy?: boolean;
}

/**
 * One box for a barcode: scan it, type it, or pick it off the list.
 *
 * ### Why one component and not three inputs
 *
 * Every screen that takes a code had built its own — a box, and beside it a separate
 * dropdown for the office, each worded differently. A man moving from the pallet screen
 * to dispatch met a different arrangement of the same three ways of naming the same kind
 * of thing.
 *
 * The three ways are not alternatives to choose between. They are one way with two
 * fallbacks, in the order the floor actually reaches for them:
 *
 * | | When |
 * |---|---|
 * | **Scan** | always, when the label is readable and the tablet can |
 * | **Type** | the label is torn — section 12 insists this must never be blocked |
 * | **Pick** | there is no label in hand at all, or the office is working from a list |
 *
 * So they share one box. The list sits under it as cards, every one in view: what each
 * thing is, its colour and the facts that tell two alike apart. It was the browser's own
 * `datalist` once, which showed nothing until something was typed, looked different on
 * every tablet, and squeezed a bag's whole description into one grey line. Typing in the
 * box filters the cards.
 *
 * Tapping a card puts its code in the box and marks it; the button beside the box does
 * the rest. Not straight away, because a list on a tablet is scrolled with the same
 * finger that taps it, and a scroll that lands on a card must not put a bag on a pallet.
 *
 * The camera button hides itself where it cannot work, saying why (see
 * `lib/barcodeScanner.ts`). A code is never blocked by that — typing is always there.
 */
export function ScanField({
  label,
  value,
  onChange,
  onSubmit,
  options,
  optionsHint,
  placeholder,
  disabled = false,
  submitLabel = 'Go',
  busy = false,
}: ScanFieldProps): ReactElement {
  const { t } = useTranslation();
  const id = useId();
  const listId = `${id}-list`;
  const box = useRef<HTMLInputElement>(null);
  const video = useRef<HTMLVideoElement>(null);
  const scanner = useRef<ScannerHandle | null>(null);

  const [isCameraOpen, setIsCameraOpen] = useState(false);
  const [cameraError, setCameraError] = useState<string | null>(null);

  // Worked out once. It cannot change while the screen is open, and asking the browser
  // on every render would be three lookups a keystroke.
  const [blocked] = useState(() => unavailableReason(currentEnvironment()));

  // Back to the box whenever it becomes usable again, so scan-scan-scan keeps its
  // rhythm. It has to be an effect: the box is disabled while a request is in flight,
  // and focusing a disabled input does nothing.
  useEffect(() => {
    if (!busy && !isCameraOpen && !disabled) {
      box.current?.focus();
      box.current?.select();
    }
  }, [busy, isCameraOpen, disabled]);

  // The camera must be released when this screen goes away, or the light stays on and
  // the next screen asking for it is refused.
  useEffect(() => {
    return () => {
      scanner.current?.stop();
      scanner.current = null;
    };
  }, []);

  async function openCamera(): Promise<void> {
    setCameraError(null);
    setIsCameraOpen(true);

    // The video element does not exist until the dialog has rendered.
    await Promise.resolve();

    const element = video.current;
    if (element === null) {
      return;
    }

    try {
      scanner.current = await startScanning(element, (code) => {
        closeCamera();
        onChange(code);
        onSubmit(code, 'Scanned');
      });
    } catch {
      // Overwhelmingly this is the man saying no to the camera prompt, which is a
      // decision and not a fault. Either way he can still type.
      setCameraError('The camera could not be opened. Type the code instead.');
    }
  }

  function closeCamera(): void {
    scanner.current?.stop();
    scanner.current = null;
    setIsCameraOpen(false);
  }

  /** The cards still matching what is in the box. All of them while it is empty. */
  function matching(all: ScanOption[]): ScanOption[] {
    const wanted = value.trim().toLowerCase();
    if (wanted === '') {
      return all;
    }
    return all.filter((option) =>
      [
        option.value,
        option.code,
        option.label,
        option.title,
        option.colour,
        ...(option.details ?? []),
      ].some((text) => text?.toLowerCase().includes(wanted) === true),
    );
  }

  /** Typed, unless it is exactly one of the offered codes — then he picked it. */
  function methodFor(entered: string): EntryMethod {
    return options?.some((option) => option.value === entered) === true
      ? 'Picked'
      : 'Typed';
  }

  const trimmed = value.trim();

  return (
    <div>
      <label className="field-label" htmlFor={id}>
        {label}
      </label>

      {/* Wraps in a narrow panel: the buttons drop under the box rather than squeeze it
          to nothing, as they did in the pallet screen's side panel. */}
      <div className="mb-3 flex flex-wrap gap-2">
        <input
          id={id}
          ref={box}
          className="field-input min-w-0 flex-[1_1_14rem] font-mono text-lg"
          placeholder={placeholder}
          autoComplete="off"
          aria-controls={options === undefined ? undefined : listId}
          value={value}
          disabled={disabled || busy}
          onChange={(event) => {
            onChange(event.target.value);
          }}
          onKeyDown={(event) => {
            // A hardware scanner types the code and presses Enter, and so does a man.
            if (event.key === 'Enter') {
              event.preventDefault();
              if (trimmed !== '') {
                onSubmit(trimmed, methodFor(trimmed));
              }
            }
          }}
        />

        {blocked === null && (
          <button
            type="button"
            className="inline-flex h-field shrink-0 items-center justify-center rounded-control border-2 border-line bg-surface px-5 text-base font-semibold text-ink transition-colors hover:border-brand-600 hover:text-brand-700 disabled:cursor-not-allowed disabled:text-ink-muted"
            disabled={disabled || busy}
            onClick={() => {
              void openCamera();
            }}
            aria-label={t('action.readWithCamera')}
            title={t('action.readWithCamera')}
          >
            {t('ui.camera')}
          </button>
        )}

        <button
          type="button"
          className="btn-primary w-auto shrink-0 px-6"
          disabled={disabled || busy || trimmed === ''}
          onClick={() => {
            onSubmit(trimmed, methodFor(trimmed));
          }}
        >
          {busy ? 'Working…' : submitLabel}
        </button>
      </div>

      {options !== undefined && (
        <OptionList
          id={listId}
          options={options}
          shown={matching(options)}
          hint={optionsHint}
          current={trimmed}
          disabled={disabled || busy}
          onPick={(option) => {
            onChange(option.value);
          }}
        />
      )}

      {blocked !== null && <p className="mb-2 text-xs text-ink-muted">{blocked}</p>}

      {cameraError !== null && (
        <p
          role="alert"
          className="mb-2 rounded-control border border-s-4 border-bad/30 border-s-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
        >
          {cameraError}
        </p>
      )}

      {isCameraOpen && (
        <Modal title={t('ui.holdLabelUp')} onClose={closeCamera}>
          <video
            ref={video}
            className="w-full rounded-control bg-canvas"
            playsInline
            muted
          />
          <p className="mt-3 text-sm text-ink-muted">
            It reads the code by itself and closes. If the label is torn, close this and
            type the code.
          </p>
        </Modal>
      )}
    </div>
  );
}

/**
 * The cards under the box. Their own component so the field above stays about the box,
 * the camera and the button.
 */
function OptionList({
  id,
  options,
  shown,
  hint,
  current,
  disabled,
  onPick,
}: {
  id: string;
  options: ScanOption[];
  shown: ScanOption[];
  hint: string | undefined;
  /** What is in the box, so the card it names is marked as the one chosen. */
  current: string;
  disabled: boolean;
  onPick: (option: ScanOption) => void;
}): ReactElement {
  const { t } = useTranslation();

  if (options.length === 0) {
    return (
      <p className="mb-3 rounded-control border border-dashed border-line px-4 py-3 text-sm text-ink-muted">
        {t('scan.nothingToChoose')}
      </p>
    );
  }

  const chosen = current.toUpperCase();

  return (
    <div className="mb-3 rounded-control border border-line bg-canvas p-2">
      <div className="flex items-baseline justify-between gap-3 px-1 pb-2">
        <span className="text-xs font-semibold tracking-wider text-ink-soft uppercase">
          {t('scan.orChoose')}
        </span>
        <span className="text-xs text-ink-muted tabular-nums">
          {shown.length === options.length
            ? String(options.length)
            : `${String(shown.length)} / ${String(options.length)}`}
        </span>
      </div>
      {hint !== undefined && <p className="px-1 pb-2 text-xs text-ink-muted">{hint}</p>}

      {shown.length === 0 ? (
        <p className="px-1 pb-1 text-sm text-ink-muted">{t('scan.noMatch')}</p>
      ) : (
        <ul
          id={id}
          className="grid max-h-80 grid-cols-[repeat(auto-fill,minmax(min(15rem,100%),1fr))] gap-1.5 overflow-y-auto"
        >
          {shown.map((option) => {
            const isChosen = option.value.toUpperCase() === chosen;
            const paint = colourSwatch(option.colour);
            return (
              <li key={option.value}>
                <button
                  type="button"
                  aria-label={option.label}
                  aria-pressed={isChosen}
                  disabled={disabled}
                  onClick={() => {
                    onPick(option);
                  }}
                  className={[
                    'flex h-full w-full items-start gap-3 rounded-control border bg-surface px-3 py-2.5 text-start transition-colors disabled:cursor-not-allowed disabled:opacity-60',
                    isChosen
                      ? 'border-brand-600 ring-1 ring-brand-600'
                      : 'border-line hover:border-brand-400 hover:bg-brand-50',
                  ].join(' ')}
                >
                  {option.colour !== undefined && (
                    <span
                      aria-hidden="true"
                      title={option.colour}
                      className={[
                        'mt-0.5 size-5 shrink-0 rounded-full border',
                        paint === null
                          ? 'border-dashed border-ink-muted bg-canvas'
                          : 'border-ink/20',
                      ].join(' ')}
                      style={paint === null ? undefined : { background: paint }}
                    />
                  )}
                  <span className="min-w-0 flex-1">
                    <span className="flex flex-wrap items-center gap-x-2 gap-y-1">
                      <span className="font-mono text-sm font-bold break-all text-ink">
                        {option.code ?? option.value}
                      </span>
                      {option.flag !== undefined && (
                        <span className="rounded-full bg-warn-soft px-2 py-0.5 text-[11px] font-semibold text-warn">
                          {option.flag}
                        </span>
                      )}
                    </span>
                    {option.title !== undefined && (
                      <span className="block text-sm text-ink-soft">{option.title}</span>
                    )}
                    {option.details !== undefined && option.details.length > 0 && (
                      <span className="mt-1.5 flex flex-wrap gap-1">
                        {option.details.map((detail) => (
                          <span
                            key={detail}
                            className="rounded bg-canvas px-1.5 py-0.5 text-[11px] break-all text-ink-soft"
                          >
                            {detail}
                          </span>
                        ))}
                      </span>
                    )}
                    {/* A caller that gives no parts still shows its one line. */}
                    {option.title === undefined && option.details === undefined && (
                      <span className="block text-sm text-ink-soft">{option.label}</span>
                    )}
                  </span>
                </button>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
