import { useState, type ReactElement } from 'react';
import { Modal } from '../../components/ui/Modal';
import { ApiError } from '../../lib/apiClient';
import { productionApi, type RollDto, type RollSummaryDto } from './api';

interface RollTestDialogProps {
  roll: RollSummaryDto;
  onClose: () => void;
  onSaved: (roll: RollDto) => void;
}

/** A roll weighs between these. 350 means the length went into the weight box. */
const MinWeight = 50;
const MaxWeight = 150;

/**
 * The measurements, taken once as the roll leaves the extruder
 * (specification section 8).
 *
 * The four thickness boxes are named by position — RS, RM, LM, LS — because that is
 * what the gauge shows and what the man reads. Their average is displayed as he types
 * but never typed itself.
 *
 * Saving this is what lets the thermo use the roll. It is not approval: nothing is
 * compared against a limit, and no roll is rejected for its numbers.
 */
export function RollTestDialog({
  roll,
  onClose,
  onSaved,
}: RollTestDialogProps): ReactElement {
  const [weight, setWeight] = useState('');
  const [length, setLength] = useState('');
  const [plateWeight, setPlateWeight] = useState('');
  const [rs, setRs] = useState('');
  const [rm, setRm] = useState('');
  const [lm, setLm] = useState('');
  const [ls, setLs] = useState('');
  const [notes, setNotes] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  const readings = [rs, rm, lm, ls].map(Number);
  const allReadings = [rs, rm, lm, ls].every((v) => v.trim() !== '') &&
    readings.every((v) => Number.isFinite(v) && v > 0);

  const average = allReadings
    ? Math.round((readings.reduce((sum, v) => sum + v, 0) / 4) * 1000) / 1000
    : null;

  const weightNumber = Number(weight);
  const weightTyped = weight.trim() !== '' && Number.isFinite(weightNumber);
  const weightLooksWrong = weightTyped && (weightNumber < MinWeight || weightNumber > MaxWeight);

  const complete =
    weightTyped &&
    !weightLooksWrong &&
    Number(length) > 0 &&
    Number(plateWeight) > 0 &&
    allReadings;

  async function save(): Promise<void> {
    setError(null);
    setIsSaving(true);
    try {
      const saved = await productionApi.saveTest(roll.id, {
        weight: weightNumber,
        length: Number(length),
        plateWeight: Number(plateWeight),
        thicknessRs: readings[0] ?? 0,
        thicknessRm: readings[1] ?? 0,
        thicknessLm: readings[2] ?? 0,
        thicknessLs: readings[3] ?? 0,
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

  return (
    <Modal title={`Measure roll ${roll.rollCode}`} onClose={onClose}>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void save();
        }}
        noValidate
      >
        <div className="mb-5 flex flex-wrap items-center gap-3 border-b border-line pb-4 text-sm text-ink-muted">
          <span className="font-mono font-semibold text-ink">{roll.rollCode}</span>
          <span>
            · {roll.colorName} · recipe {roll.recipeNumber} {roll.recipeFamilyName}
          </span>
        </div>

        <div className="grid gap-4 sm:grid-cols-3">
          <Field label="Weight (kg)" htmlFor="test-weight">
            <input
              id="test-weight"
              type="number"
              step="0.001"
              className="field-input"
              value={weight}
              disabled={isSaving}
              onChange={(event) => {
                setWeight(event.target.value);
              }}
            />
          </Field>
          <Field label="Length" htmlFor="test-length">
            <input
              id="test-length"
              type="number"
              step="0.001"
              className="field-input"
              value={length}
              disabled={isSaving}
              onChange={(event) => {
                setLength(event.target.value);
              }}
            />
          </Field>
          <Field label="Plate weight (g)" htmlFor="test-plate">
            <input
              id="test-plate"
              type="number"
              step="0.001"
              className="field-input"
              value={plateWeight}
              disabled={isSaving}
              onChange={(event) => {
                setPlateWeight(event.target.value);
              }}
            />
          </Field>
        </div>

        {/* The mistake that is already in the real Roll Log: the length typed into
            the weight box. Caught while he is still at the machine. */}
        {weightLooksWrong && (
          <p className="mb-4 rounded-control border border-l-4 border-warn/30 border-l-warn bg-warn-soft px-4 py-3 text-sm font-medium text-warn">
            A roll weighs between {MinWeight} and {MaxWeight} kg. Is {weight} the length
            typed into the weight box?
          </p>
        )}

        <p className="field-label">Thickness, four readings across the roll</p>
        <div className="mb-4 grid grid-cols-2 gap-3 sm:grid-cols-4">
          <Reading id="test-rs" label="RS" value={rs} onChange={setRs} disabled={isSaving} />
          <Reading id="test-rm" label="RM" value={rm} onChange={setRm} disabled={isSaving} />
          <Reading id="test-lm" label="LM" value={lm} onChange={setLm} disabled={isSaving} />
          <Reading id="test-ls" label="LS" value={ls} onChange={setLs} disabled={isSaving} />
        </div>

        <div className="mb-4 rounded-control bg-canvas px-4 py-3">
          <div className="flex items-baseline justify-between gap-3">
            <span className="text-sm font-medium text-ink-soft">Average thickness</span>
            <span className="text-lg font-bold text-ink">{average ?? '—'}</span>
          </div>
          <p className="mt-1 text-xs text-ink-muted">
            The mean of the four. Worked out, never typed.
          </p>
        </div>

        <div className="mb-4">
          <label className="field-label" htmlFor="test-notes">
            Note <span className="font-normal text-ink-muted">(optional)</span>
          </label>
          <input
            id="test-notes"
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
            className="mb-4 rounded-control border border-l-4 border-bad/30 border-l-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
          >
            {error}
          </p>
        )}

        <button type="submit" className="btn-primary" disabled={isSaving || !complete}>
          {isSaving ? 'Saving…' : 'Save and release the roll'}
        </button>
        <p className="mt-2 text-xs text-ink-muted">
          The thermo can use the roll once this is saved. Nothing here is compared
          against a limit — the point is only that the measurement was taken, because
          after forming there is nothing left to measure.
        </p>
      </form>
    </Modal>
  );
}

function Field({
  label,
  htmlFor,
  children,
}: {
  label: string;
  htmlFor: string;
  children: ReactElement;
}): ReactElement {
  return (
    <div className="mb-4">
      <label className="field-label" htmlFor={htmlFor}>
        {label}
      </label>
      {children}
    </div>
  );
}

function Reading({
  id,
  label,
  value,
  onChange,
  disabled,
}: {
  id: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
  disabled: boolean;
}): ReactElement {
  return (
    <div>
      <label className="field-label" htmlFor={id}>
        {label}
      </label>
      <input
        id={id}
        type="number"
        step="0.001"
        min="0"
        className="field-input text-center"
        value={value}
        disabled={disabled}
        onChange={(event) => {
          onChange(event.target.value);
        }}
      />
    </div>
  );
}
