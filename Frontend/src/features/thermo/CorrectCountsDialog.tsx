import { useQuery } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { Modal } from '../../components/ui/Modal';
import { useTranslation } from '../../hooks/useTranslation';
import { ApiError } from '../../lib/apiClient';
import { thermoApi, type ThermoRunDto, type ThermoTestReportDto } from './api';

interface CorrectCountsDialogProps {
  runId: number;
  rollCode: string;
  onClose: () => void;
  /** With the codes of any bags the correction made, so their labels can be printed. */
  onCorrected: (run: ThermoRunDto, newBarcodes: string[]) => void;
}

/**
 * Corrects counts saved wrong (specification section 19.27) — a bag count typed 140 for
 * 14, a weight off by a slip of the finger.
 *
 * The figures start as they were saved. Put the bag count up and the missing bags are
 * made, with new labels; put it down and the extra ones are cancelled — only bags not on a
 * pallet, newest first, and their labels stop scanning. The reason is required.
 */
export function CorrectCountsDialog({
  runId,
  rollCode,
  onClose,
  onCorrected,
}: CorrectCountsDialogProps): ReactElement {
  const { t } = useTranslation();
  const run = useQuery({
    queryKey: ['thermo-run', runId],
    queryFn: () => thermoApi.run(runId),
  });

  return (
    <Modal title={`${t('correct.countsTitle')} — ${rollCode}`} onClose={onClose}>
      {run.isPending ? (
        <p className="text-ink-muted">{t('common.loading')}</p>
      ) : run.isError || run.data.testReport === null ? (
        <p className="text-bad">{t('common.somethingWentWrong')}</p>
      ) : (
        <CorrectCountsForm
          run={run.data}
          saved={run.data.testReport}
          onClose={onClose}
          onCorrected={onCorrected}
        />
      )}
    </Modal>
  );
}

function CorrectCountsForm({
  run,
  saved,
  onClose,
  onCorrected,
}: {
  run: ThermoRunDto;
  /** The counts as they were saved — the starting point of the correction. */
  saved: ThermoTestReportDto;
  onClose: () => void;
  onCorrected: (run: ThermoRunDto, newBarcodes: string[]) => void;
}): ReactElement {
  const { t } = useTranslation();
  const [bagCount, setBagCount] = useState(String(saved.bagCount));
  const [pieceWeight, setPieceWeight] = useState(String(saved.pieceWeight));
  const [bagWeight, setBagWeight] = useState(String(saved.bagWeight));
  const [absorbent, setAbsorbent] = useState(String(saved.absorbentPercentage));
  const [notes, setNotes] = useState(saved.notes ?? '');
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  const bags = Number(bagCount);
  const fewer = Number.isInteger(bags) && bags < saved.bagCount;
  const more = Number.isInteger(bags) && bags > saved.bagCount;

  async function save(): Promise<void> {
    setError(null);
    setIsSaving(true);
    try {
      const before = new Set(run.bags.map((bag) => bag.barcode));
      const corrected = await thermoApi.correctTest(run.id, {
        bagCount: bags,
        pieceWeight: Number(pieceWeight),
        bagWeight: Number(bagWeight),
        absorbentPercentage: run.isAbsorbent ? Number(absorbent) : 0,
        notes: notes.trim() === '' ? null : notes.trim(),
        reason: reason.trim(),
      });
      onCorrected(
        corrected,
        corrected.bags.map((bag) => bag.barcode).filter((code) => !before.has(code)),
      );
      onClose();
    } catch (caught) {
      setError(
        caught instanceof ApiError ? caught.message : t('common.somethingWentWrong'),
      );
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <form
      onSubmit={(event) => {
        event.preventDefault();
        void save();
      }}
      noValidate
    >
      <p className="mb-4 text-sm text-ink-soft">{t('correct.countsExplain')}</p>

      <div className="grid gap-4 sm:grid-cols-3">
        <NumberBox
          id="correct-bags"
          label={t('thermo.bagsProduced')}
          value={bagCount}
          onChange={setBagCount}
          disabled={isSaving}
          step="1"
        />
        <NumberBox
          id="correct-piece-weight"
          label={t('thermo.pieceWeightG')}
          value={pieceWeight}
          onChange={setPieceWeight}
          disabled={isSaving}
        />
        <NumberBox
          id="correct-bag-weight"
          label={t('thermo.bagWeightKg')}
          value={bagWeight}
          onChange={setBagWeight}
          disabled={isSaving}
        />
      </div>

      {run.isAbsorbent && (
        <div className="max-w-xs">
          <NumberBox
            id="correct-absorbent"
            label={t('correct.absorbency')}
            value={absorbent}
            onChange={setAbsorbent}
            disabled={isSaving}
          />
        </div>
      )}

      {/* Says what the bag count will do before it is done. */}
      {fewer && (
        <p className="mb-4 rounded-control border border-s-4 border-warn/30 border-s-warn bg-warn-soft px-4 py-3 text-sm text-warn">
          {saved.bagCount - bags} {t('correct.bagsWillBeCancelled')}
        </p>
      )}
      {more && (
        <p className="mb-4 rounded-control border border-s-4 border-brand-200 border-s-brand-600 bg-brand-50 px-4 py-3 text-sm text-brand-700">
          {bags - saved.bagCount} {t('correct.bagsWillBeMade')}
        </p>
      )}

      <div className="mb-4">
        <label className="field-label" htmlFor="correct-notes">
          {t('field.note')}
        </label>
        <input
          id="correct-notes"
          className="field-input"
          maxLength={500}
          value={notes}
          disabled={isSaving}
          onChange={(event) => {
            setNotes(event.target.value);
          }}
        />
      </div>

      <div className="mb-4">
        <label className="field-label" htmlFor="correct-reason">
          {t('correct.whyCorrect')}
        </label>
        <textarea
          id="correct-reason"
          className="field-input min-h-20"
          maxLength={500}
          value={reason}
          disabled={isSaving}
          onChange={(event) => {
            setReason(event.target.value);
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

      <button
        type="submit"
        className="btn-primary"
        disabled={isSaving || reason.trim() === '' || !Number.isInteger(bags) || bags < 1}
      >
        {isSaving ? t('common.saving') : t('correct.saveCorrection')}
      </button>
    </form>
  );
}

function NumberBox({
  id,
  label,
  value,
  onChange,
  disabled,
  step = '0.001',
}: {
  id: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
  disabled: boolean;
  step?: string;
}): ReactElement {
  return (
    <div className="mb-4">
      <label className="field-label" htmlFor={id}>
        {label}
      </label>
      <input
        id={id}
        type="number"
        step={step}
        min="0"
        className="field-input"
        value={value}
        disabled={disabled}
        onChange={(event) => {
          onChange(event.target.value);
        }}
      />
    </div>
  );
}
