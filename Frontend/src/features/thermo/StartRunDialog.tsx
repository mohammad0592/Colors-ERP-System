import { useState, type ReactElement } from 'react';
import { Modal } from '../../components/ui/Modal';
import { ApiError } from '../../lib/apiClient';
import { formatDate } from '../shifts/shiftFormat';
import { thermoApi, type AvailableRollDto, type ThermoRunDto } from './api';

interface StartRunDialogProps {
  line: { shiftLineId: number; label: string; mouldName: string | null };
  rolls: AvailableRollDto[];
  onClose: () => void;
  onStarted: (run: ThermoRunDto) => void;
}

/**
 * Putting a roll into the thermo (specification section 9).
 *
 * The scanner comes first, because that is what the floor does — the box is focused the
 * moment the dialog opens, and a scanner types then presses Enter. Picking from the list
 * is there for the office, and for a label too torn to read.
 *
 * Nothing else is asked for. The recipe, the colour and the product all come from the
 * roll and the mould, so there is nothing here to get wrong.
 */
export function StartRunDialog({
  line,
  rolls,
  onClose,
  onStarted,
}: StartRunDialogProps): ReactElement {
  const [barcode, setBarcode] = useState('');
  const [picked, setPicked] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  const chosen = rolls.find((r) => r.id === picked) ?? null;
  const typed = barcode.trim() !== '';
  const complete = typed || picked !== null;

  async function start(): Promise<void> {
    setError(null);
    setIsSaving(true);
    try {
      const run = await thermoApi.startRun({
        // A scanned label wins: it is the roll actually in his hands.
        rollBarcode: typed ? barcode.trim() : null,
        rollId: typed ? null : picked,
        shiftLineId: line.shiftLineId,
        startedAt: null,
        notes: null,
      });
      onStarted(run);
      onClose();
    } catch (caught) {
      setError(
        caught instanceof ApiError ? caught.message : 'Something went wrong. Try again.',
      );
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <Modal title="Put a roll into the thermo" onClose={onClose}>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void start();
        }}
        noValidate
      >
        <div className="mb-5 border-b border-line pb-4 text-sm text-ink-muted">
          <p>{line.label}</p>
          <p className="mt-1">
            Mould: <strong className="text-ink">{line.mouldName ?? 'none set'}</strong>
            {line.mouldName !== null && (
              <span> — this and the roll&apos;s recipe decide what is made.</span>
            )}
          </p>
        </div>

        <div className="mb-4">
          <label className="field-label" htmlFor="run-barcode">
            Scan the roll
          </label>
          <input
            id="run-barcode"
            // The scanner types into whatever has focus, so this box must have it.
            autoFocus
            className="field-input font-mono"
            placeholder="R000123"
            value={barcode}
            disabled={isSaving}
            onChange={(event) => {
              setBarcode(event.target.value);
              setPicked(null);
            }}
          />
        </div>

        <div className="mb-4">
          <label className="field-label" htmlFor="run-roll">
            Or pick one <span className="font-normal text-ink-muted">(oldest first)</span>
          </label>
          <select
            id="run-roll"
            className="field-input"
            value={picked === null ? '' : String(picked)}
            disabled={isSaving || typed}
            onChange={(event) => {
              setPicked(event.target.value === '' ? null : Number(event.target.value));
            }}
          >
            <option value="">Choose a roll…</option>
            {rolls.map((roll) => (
              <option key={roll.id} value={roll.id}>
                {roll.rollCode} — {roll.colorName}, {roll.recipeFamilyName},{' '}
                {formatDate(roll.productionDate)}
              </option>
            ))}
          </select>
        </div>

        {chosen !== null && (
          <p className="mb-4 rounded-control bg-canvas px-4 py-3 text-sm text-ink-soft">
            <strong className="font-mono text-ink">{chosen.rollCode}</strong> ·{' '}
            {chosen.colorName} · recipe {chosen.recipeNumber} {chosen.recipeFamilyName}
            {chosen.weight !== null && <> · {chosen.weight} kg</>}
            {chosen.isAbsorbent && (
              <span className="ml-2 rounded-full bg-brand-50 px-2 py-0.5 text-xs font-semibold text-brand-700">
                Absorbent
              </span>
            )}
          </p>
        )}

        {rolls.length === 0 && (
          <p className="mb-4 rounded-control border border-line bg-canvas px-4 py-3 text-sm text-ink-soft">
            No roll has been measured and left in stock. A roll cannot come here until it
            has been measured, because once it is formed into plates there is nothing
            left to measure.
          </p>
        )}

        {error !== null && (
          <p
            role="alert"
            className="mb-4 rounded-control border border-l-4 border-bad/30 border-l-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
          >
            {error}
          </p>
        )}

        <button type="submit" className="btn-primary" disabled={isSaving || !complete}>
          {isSaving ? 'Starting…' : 'Start forming'}
        </button>
        <p className="mt-2 text-xs text-ink-muted">
          The roll leaves the store the moment it goes in, so nobody else can pick it.
        </p>
      </form>
    </Modal>
  );
}
