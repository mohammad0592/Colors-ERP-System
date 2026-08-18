import {
  useEffect,
  useId,
  useRef,
  useState,
  type ReactElement,
} from 'react';
import {
  currentEnvironment,
  startScanning,
  unavailableReason,
  type EntryMethod,
  type ScannerHandle,
} from '../../lib/barcodeScanner';
import { Modal } from './Modal';

/** One thing the field can be filled from without scanning or typing. */
export interface ScanOption {
  /** The code that goes in the box — a barcode, or a roll code. */
  value: string;
  /** What the man reads: "B000123 — Black Big Plate, from roll 09WN180726A". */
  label: string;
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
 * So they share one box. The list is the browser's own `datalist`, which is what makes
 * this a single input rather than an input plus a dropdown: the same box accepts typing
 * and offers the list, and the browser filters as the man types.
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

      <div className="mb-3 flex gap-2">
        <input
          id={id}
          ref={box}
          className="field-input flex-1 font-mono text-lg"
          placeholder={placeholder}
          autoComplete="off"
          list={options === undefined ? undefined : listId}
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
            aria-label="Read the label with the camera"
            title="Read the label with the camera"
          >
            Camera
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
        <datalist id={listId}>
          {options.map((option) => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
        </datalist>
      )}

      {options !== undefined && (
        <p className="mb-2 text-xs text-ink-muted">
          {options.length === 0
            ? 'Nothing is waiting to be chosen from.'
            : `Or start typing to choose from ${String(options.length)} — ${optionsHint ?? 'what the screen already knows about'}.`}
        </p>
      )}

      {blocked !== null && <p className="mb-2 text-xs text-ink-muted">{blocked}</p>}

      {cameraError !== null && (
        <p
          role="alert"
          className="mb-2 rounded-control border border-l-4 border-bad/30 border-l-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
        >
          {cameraError}
        </p>
      )}

      {isCameraOpen && (
        <Modal title="Hold the label up to the camera" onClose={closeCamera}>
          <video ref={video} className="w-full rounded-control bg-canvas" playsInline muted />
          <p className="mt-3 text-sm text-ink-muted">
            It reads the code by itself and closes. If the label is torn, close this and
            type the code.
          </p>
        </Modal>
      )}
    </div>
  );
}
