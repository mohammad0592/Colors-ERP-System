import { useState, type ReactElement } from 'react';
import { Modal } from '../../components/ui/Modal';
import { ScanField } from '../../components/ui/ScanField';
import { ApiError } from '../../lib/apiClient';
import { cleanCode, type EntryMethod } from '../../lib/barcodeScanner';
import { formatDate } from '../shifts/shiftFormat';
import { thermoApi, type AvailableRollDto, type ThermoRunDto } from './api';

interface StartRunDialogProps {
  line: {
    shiftLineId: number;
    lineName: string;
    shiftLabel: string;
    mouldName: string | null;
  };
  rolls: AvailableRollDto[];
  onClose: () => void;
  onStarted: (run: ThermoRunDto) => void;
}

/**
 * Putting a roll into the thermo (specification section 9).
 *
 * The scanner comes first, because that is what the floor does. Picking from the list is
 * there for the office, and for a label too torn to read — both in the one box, which is
 * `ScanField` and behaves the same wherever a code is asked for.
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
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  // The list offers barcodes, so scanning and picking end at the same value and there is
  // no second piece of state that can disagree with the box.
  const chosen = rolls.find((r) => r.barcode === cleanCode(barcode)) ?? null;

  async function start(code: string, entry: EntryMethod): Promise<void> {
    if (code === '') {
      return;
    }

    setError(null);
    setIsSaving(true);
    try {
      const run = await thermoApi.startRun(
        {
          rollBarcode: code,
          rollId: null,
          shiftLineId: line.shiftLineId,
          startedAt: null,
          notes: null,
        },
        entry,
      );
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
      <div>
        <div className="mb-5 border-b border-line pb-4 text-sm text-ink-muted">
          <p>
            {line.lineName} — {line.shiftLabel}
          </p>
          <p className="mt-1">
            Mould: <strong className="text-ink">{line.mouldName ?? 'none set'}</strong>
            {line.mouldName !== null && (
              <span> — this and the roll&apos;s recipe decide what is made.</span>
            )}
          </p>
        </div>

        <div className="mb-4">
          <ScanField
            label="Scan the roll"
            placeholder="R000123"
            value={barcode}
            onChange={setBarcode}
            onSubmit={(code, entry) => {
              void start(code, entry);
            }}
            options={rolls.map((roll) => ({
              value: roll.barcode,
              label: `${roll.rollCode} — ${roll.colorName}, ${roll.recipeFamilyName}, ${formatDate(roll.productionDate)}`,
            }))}
            optionsHint="measured rolls in stock, oldest first"
            submitLabel="Start forming"
            busy={isSaving}
          />
        </div>

        {chosen !== null && (
          <p className="mb-4 rounded-control bg-canvas px-4 py-3 text-sm text-ink-soft">
            <strong className="font-mono text-ink">{chosen.rollCode}</strong> ·{' '}
            {chosen.colorName} · recipe {chosen.recipeNumber} {chosen.recipeFamilyName}
            {chosen.weight !== null && <> · {chosen.weight} kg</>}
            {chosen.length !== null && <> · length {chosen.length}</>}
            {chosen.isAbsorbent && (
              <span className="ms-2 rounded-full bg-brand-50 px-2 py-0.5 text-xs font-semibold text-brand-700">
                Absorbent
              </span>
            )}
          </p>
        )}

        {rolls.length === 0 && (
          <p className="mb-4 rounded-control border border-line bg-canvas px-4 py-3 text-sm text-ink-soft">
            No roll has been measured and left in stock. A roll cannot come here until it
            has been measured, because once it is formed into plates there is nothing left
            to measure.
          </p>
        )}

        {error !== null && (
          <p
            role="alert"
            className="mb-4 rounded-control border border-s-4 border-bad/30 border-s-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
          >
            {error}
          </p>
        )}

        <p className="mt-2 text-xs text-ink-muted">
          The roll leaves the store as it goes in.
        </p>
      </div>
    </Modal>
  );
}
