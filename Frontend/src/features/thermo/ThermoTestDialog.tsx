import { useEffect, useState, type ReactElement } from 'react';
import { Modal } from '../../components/ui/Modal';
import { ApiError } from '../../lib/apiClient';
import { thermoApi, type ThermoRunDto, type ThermoRunSummaryDto } from './api';

interface ThermoTestDialogProps {
  run: ThermoRunSummaryDto;
  onClose: () => void;
  onSaved: (run: ThermoRunDto) => void;
}

/** One roll does not make this many bags. The wall a typo hits, not a judgement. */
const MaxBags = 200;

/**
 * The thermo form, filled in after the run (specification section 9).
 *
 * Two things are deliberately missing. The product, because the mould and the roll's
 * recipe decide it. And the piece count, because it is the bag count times what the
 * product holds — shown as he types, never typed.
 *
 * The roll's own weight, length and thickness sit at the top read-only. They are on the
 * paper form in that position, so the man expects to see them, but they already exist
 * against the roll and are never asked for twice.
 *
 * Saving this creates the bags and prints their labels.
 */
export function ThermoTestDialog({
  run,
  onClose,
  onSaved,
}: ThermoTestDialogProps): ReactElement {
  const [bagCount, setBagCount] = useState('');
  const [pieceWeight, setPieceWeight] = useState('');
  const [bagWeight, setBagWeight] = useState('');
  const [absorbent, setAbsorbent] = useState('');
  const [notes, setNotes] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  // The full run carries the roll's readings and the mould, which the list does not.
  const [detail, setDetail] = useState<ThermoRunDto | null>(null);

  useEffect(() => {
    let cancelled = false;
    void thermoApi.run(run.id).then(
      (loaded) => {
        if (!cancelled) {
          setDetail(loaded);
        }
      },
      () => {
        // The readings are a courtesy; the form still works without them.
      },
    );
    return () => {
      cancelled = true;
    };
  }, [run.id]);

  const bags = Number(bagCount);
  const bagsTyped = bagCount.trim() !== '' && Number.isInteger(bags) && bags > 0;
  const bagsLookWrong = bagsTyped && bags > MaxBags;

  const complete =
    bagsTyped &&
    !bagsLookWrong &&
    Number(pieceWeight) > 0 &&
    Number(bagWeight) > 0 &&
    (!run.isAbsorbent || absorbent.trim() !== '');

  async function save(): Promise<void> {
    setError(null);
    setIsSaving(true);
    try {
      const saved = await thermoApi.saveTest(run.id, {
        bagCount: bags,
        pieceWeight: Number(pieceWeight),
        bagWeight: Number(bagWeight),
        // A normal roll cannot have absorbed anything, so the box is not even shown.
        absorbentPercentage: run.isAbsorbent ? Number(absorbent) : 0,
        notes: notes.trim() === '' ? null : notes.trim(),
      });
      onSaved(saved);
      onClose();
    } catch (caught) {
      setError(
        caught instanceof ApiError ? caught.message : 'Something went wrong. Try again.',
      );
    } finally {
      setIsSaving(false);
    }
  }

  const readings = detail?.rollReadings ?? null;

  return (
    <Modal title={`Count what roll ${run.rollCode} made`} onClose={onClose}>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void save();
        }}
        noValidate
      >
        <div className="mb-5 flex flex-wrap items-center gap-3 border-b border-line pb-4 text-sm text-ink-muted">
          <span className="font-mono font-semibold text-ink">{run.rollCode}</span>
          <span>
            · {run.colorName} · recipe {run.recipeNumber} {run.recipeFamilyName}
          </span>
          {run.totalTimeMinutes !== null && (
            <span>· {run.totalTimeMinutes} minutes in the machine</span>
          )}
        </div>

        {/* Already measured at the extruder. Shown, never asked for again. */}
        {readings !== null && (
          <div className="mb-5 rounded-control bg-canvas px-4 py-3">
            <p className="mb-2 text-xs font-semibold tracking-wider text-ink-muted uppercase">
              The roll itself — measured at the extruder
            </p>
            <div className="grid grid-cols-2 gap-3 text-sm sm:grid-cols-4">
              <Reading label="Weight" value={`${String(readings.weight)} kg`} />
              <Reading label="Length" value={String(readings.length)} />
              <Reading label="Plate weight" value={`${String(readings.plateWeight)} g`} />
              <Reading label="Thickness" value={String(readings.averageThickness)} />
            </div>
          </div>
        )}

        <div className="grid gap-4 sm:grid-cols-3">
          <Field label="Bags produced" arabic="عدد الأكياس المنتجة" htmlFor="thermo-bags">
            <input
              id="thermo-bags"
              type="number"
              step="1"
              min="1"
              className="field-input"
              value={bagCount}
              disabled={isSaving}
              onChange={(event) => {
                setBagCount(event.target.value);
              }}
            />
          </Field>
          <Field
            label="Piece weight (g)"
            arabic="وزن الصحن المنتج"
            htmlFor="thermo-piece-weight"
          >
            <input
              id="thermo-piece-weight"
              type="number"
              step="0.001"
              className="field-input"
              value={pieceWeight}
              disabled={isSaving}
              onChange={(event) => {
                setPieceWeight(event.target.value);
              }}
            />
          </Field>
          <Field
            label="Bag weight (kg)"
            arabic="وزن الكيس المنتج الواحد"
            htmlFor="thermo-bag-weight"
          >
            <input
              id="thermo-bag-weight"
              type="number"
              step="0.001"
              className="field-input"
              value={bagWeight}
              disabled={isSaving}
              onChange={(event) => {
                setBagWeight(event.target.value);
              }}
            />
          </Field>
        </div>

        {bagsLookWrong && (
          <p className="mb-4 rounded-control border border-s-4 border-warn/30 border-s-warn bg-warn-soft px-4 py-3 text-sm font-medium text-warn">
            One roll does not make {bagCount} bags. Check the number.
          </p>
        )}

        {/* Absorbency comes from what was mixed, so a normal roll is never asked. */}
        {run.isAbsorbent && (
          <div className="mb-4 sm:max-w-xs">
            <Field
              label="Absorbency (%)"
              arabic="نسبة الإمتصاص للصحن المنتج"
              htmlFor="thermo-absorbent"
            >
              <input
                id="thermo-absorbent"
                type="number"
                step="0.01"
                min="0"
                max="100"
                className="field-input"
                value={absorbent}
                disabled={isSaving}
                onChange={(event) => {
                  setAbsorbent(event.target.value);
                }}
              />
            </Field>
          </div>
        )}

        <div className="mb-4 rounded-control bg-canvas px-4 py-3">
          <div className="flex items-baseline justify-between gap-3">
            <span className="text-sm font-medium text-ink-soft">
              Bags this will create
            </span>
            <span className="text-lg font-bold text-ink">
              {bagsTyped && !bagsLookWrong ? bagCount : '—'}
            </span>
          </div>
          <p className="mt-1 text-xs text-ink-muted">
            Each one gets its own barcode. The pieces are worked out from the product, so
            they are never typed — and what the product is comes from the mould and this
            roll&apos;s recipe.
          </p>
        </div>

        <div className="mb-4">
          <label className="field-label" htmlFor="thermo-notes">
            Note <span className="font-normal text-ink-muted">(optional)</span>
          </label>
          <input
            id="thermo-notes"
            className="field-input"
            maxLength={300}
            value={notes}
            disabled={isSaving}
            onChange={(event) => {
              setNotes(event.target.value);
            }}
          />
        </div>

        {error !== null && (
          <p
            role="alert"
            className="mb-4 rounded-control border border-s-4 border-bad/30 border-s-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
          >
            {error}
          </p>
        )}

        <button type="submit" className="btn-primary" disabled={isSaving || !complete}>
          {isSaving ? 'Saving…' : 'Save and print the bag labels'}
        </button>
        <p className="mt-2 text-xs text-ink-muted">
          Saving this creates the bags.
        </p>
      </form>
    </Modal>
  );
}

function Reading({ label, value }: { label: string; value: string }): ReactElement {
  return (
    <div>
      <p className="text-xs text-ink-muted">{label}</p>
      <p className="font-semibold text-ink">{value}</p>
    </div>
  );
}

function Field({
  label,
  arabic,
  htmlFor,
  children,
}: {
  label: string;
  arabic: string;
  htmlFor: string;
  children: ReactElement;
}): ReactElement {
  return (
    <div className="mb-4">
      <label className="field-label" htmlFor={htmlFor}>
        {label}{' '}
        {/* The label the form has always had, so the man finds his box by eye. */}
        <span className="font-normal text-ink-muted" dir="rtl">
          {arabic}
        </span>
      </label>
      {children}
    </div>
  );
}
