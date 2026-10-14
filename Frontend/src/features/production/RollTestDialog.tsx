import { useQuery } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { useTranslation } from '../../hooks/useTranslation';
import { Modal } from '../../components/ui/Modal';
import { ApiError } from '../../lib/apiClient';
import type { TranslationKey } from '../../lib/i18n/en';
import { productionApi, type RollDto, type RollSummaryDto } from './api';
import { formatCountdown, useNow } from './soakTimer';

interface RollTestDialogProps {
  roll: RollSummaryDto;
  onClose: () => void;
  onSaved: (roll: RollDto) => void;
}

/**
 * The roll test (specification section 8): the sample plate, the four thickness readings,
 * and on an absorbent roll the absorbency. Weight and length are measured on the production
 * screen instead (section 19.12).
 *
 * Saved as it goes (section 19.15). Whatever has been typed is kept when he saves, and he
 * comes back for the rest; the roll waits at Needs Test until every reading is in. Opened
 * on a finished test, it corrects it.
 *
 * The absorbency is two visits ten minutes apart: the piece is weighed dry and saved, which
 * starts the clock on the server, and the after-water box opens when the ten minutes are up.
 *
 * It is not approval: nothing is compared against a limit, and no roll is rejected for its
 * numbers.
 */
export function RollTestDialog({
  roll,
  onClose,
  onSaved,
}: RollTestDialogProps): ReactElement {
  const { t } = useTranslation();

  // What has been saved so far, so the boxes open filled in.
  const full = useQuery({
    queryKey: ['roll', roll.id],
    queryFn: () => productionApi.roll(roll.id),
  });

  return (
    <Modal title={`${t('rolls.testTitle')} — ${roll.rollCode}`} onClose={onClose}>
      {full.isPending ? (
        <p className="text-ink-muted">{t('common.loading')}</p>
      ) : full.isError ? (
        <p className="text-bad">{t('rolls.loadFailed')}</p>
      ) : (
        <RollTestForm
          roll={full.data}
          isAbsorbent={roll.isAbsorbent}
          onClose={onClose}
          onSaved={onSaved}
        />
      )}
    </Modal>
  );
}

/** A box's value: null when empty, so the server keeps it as not yet taken. */
function reading(value: string): number | null {
  return value.trim() === '' ? null : Number(value);
}

/** What a saved reading shows in its box. */
function shown(value: number | null | undefined): string {
  return value === null || value === undefined ? '' : String(value);
}

function RollTestForm({
  roll,
  isAbsorbent,
  onClose,
  onSaved,
}: {
  roll: RollDto;
  isAbsorbent: boolean;
  onClose: () => void;
  onSaved: (roll: RollDto) => void;
}): ReactElement {
  const { t } = useTranslation();
  const saved = roll.testReport;
  const [plateWeight, setPlateWeight] = useState(shown(saved?.plateWeight));
  const [rs, setRs] = useState(shown(saved?.thicknessRs));
  const [rm, setRm] = useState(shown(saved?.thicknessRm));
  const [lm, setLm] = useState(shown(saved?.thicknessLm));
  const [ls, setLs] = useState(shown(saved?.thicknessLs));
  const [notes, setNotes] = useState(saved?.notes ?? '');
  // Absorbent rolls only (section 19.14): a piece weighed dry, then after ten minutes in
  // water. The percentage is shown as he types, never typed.
  const [dry, setDry] = useState(shown(saved?.absorbencyDryWeight));
  const [wet, setWet] = useState(shown(saved?.absorbencyWetWeight));
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  const values = {
    plate: reading(plateWeight),
    rs: reading(rs),
    rm: reading(rm),
    lm: reading(lm),
    ls: reading(ls),
    dry: isAbsorbent ? reading(dry) : null,
    wet: isAbsorbent ? reading(wet) : null,
  };
  const typed = Object.values(values).filter((v) => v !== null);
  // A box with something in it must hold a real reading; empty is fine, it waits.
  const anyBad = typed.some((v) => !Number.isFinite(v) || v <= 0);

  const thickness = [values.rs, values.rm, values.lm, values.ls];
  const average = thickness.every((v) => v !== null && v > 0)
    ? Math.round((thickness.reduce<number>((sum, v) => sum + (v ?? 0), 0) / 4) * 1000) /
      1000
    : null;

  // The clock is the server's: it started when the dry weight was saved.
  const dueAt =
    saved?.absorbencyWetDueAt === null || saved?.absorbencyWetDueAt === undefined
      ? null
      : Date.parse(saved.absorbencyWetDueAt);
  const now = useNow(dueAt !== null);
  const drySaved = (saved?.absorbencyDryWeighedAt ?? null) !== null;
  const soaking = dueAt !== null && now < dueAt;
  const wetOpen = drySaved && !soaking;

  const absorbencyBackwards =
    values.dry !== null && values.wet !== null && values.wet < values.dry;
  const absorbency =
    values.dry !== null && values.dry > 0 && values.wet !== null && !absorbencyBackwards
      ? Math.round(((values.wet - values.dry) / values.dry) * 100 * 100) / 100
      : null;

  // What is still to be taken, said in words under the button.
  const missing: TranslationKey[] = [
    ...(values.plate === null ? ['rolls.missingPlate' as const] : []),
    ...(average === null ? ['rolls.missingThickness' as const] : []),
    ...(isAbsorbent && values.dry === null ? ['rolls.missingDry' as const] : []),
    ...(isAbsorbent && values.wet === null ? ['rolls.missingWet' as const] : []),
  ];

  async function save(): Promise<void> {
    setError(null);
    setIsSaving(true);
    try {
      const result = await productionApi.saveTest(roll.id, {
        plateWeight: values.plate,
        thicknessRs: values.rs,
        thicknessRm: values.rm,
        thicknessLm: values.lm,
        thicknessLs: values.ls,
        notes: notes.trim() === '' ? null : notes.trim(),
        ...(isAbsorbent
          ? { absorbencyDryWeight: values.dry, absorbencyWetWeight: values.wet }
          : {}),
      });
      onSaved(result);
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
      <div className="mb-5 flex flex-wrap items-center gap-3 border-b border-line pb-4 text-sm text-ink-muted">
        <span className="font-mono font-semibold text-ink">{roll.rollCode}</span>
        <span>
          · {roll.colorName} · recipe {roll.recipeNumber} {roll.recipeFamilyName}
        </span>
      </div>

      <Field label={t('rolls.plateWeightG')} htmlFor="test-plate">
        <NumberBox
          id="test-plate"
          value={plateWeight}
          onChange={setPlateWeight}
          disabled={isSaving}
        />
      </Field>

      <p className="field-label">{t('rolls.fourReadings')}</p>
      <div className="mb-4 grid grid-cols-2 gap-3 sm:grid-cols-4">
        <Reading
          id="test-rs"
          label="RS"
          value={rs}
          onChange={setRs}
          disabled={isSaving}
        />
        <Reading
          id="test-rm"
          label="RM"
          value={rm}
          onChange={setRm}
          disabled={isSaving}
        />
        <Reading
          id="test-lm"
          label="LM"
          value={lm}
          onChange={setLm}
          disabled={isSaving}
        />
        <Reading
          id="test-ls"
          label="LS"
          value={ls}
          onChange={setLs}
          disabled={isSaving}
        />
      </div>

      <div className="mb-4 rounded-control bg-canvas px-4 py-3">
        <div className="flex items-baseline justify-between gap-3">
          <span className="text-sm font-medium text-ink-soft">
            {t('rolls.averageThickness')}
          </span>
          <span className="text-lg font-bold text-ink">{average ?? '—'}</span>
        </div>
        <p className="mt-1 text-xs text-ink-muted">{t('rolls.meanOfFour')}</p>
      </div>

      {isAbsorbent && (
        <>
          <p className="field-label">{t('rolls.absorbencyTitle')}</p>
          <div className="mb-3 grid gap-3 sm:grid-cols-2">
            <Field label={t('rolls.absorbencyDry')} htmlFor="test-dry">
              <NumberBox
                id="test-dry"
                value={dry}
                onChange={setDry}
                disabled={isSaving}
              />
            </Field>
            <Field label={t('rolls.absorbencyWet')} htmlFor="test-wet">
              <NumberBox
                id="test-wet"
                value={wet}
                onChange={setWet}
                disabled={isSaving || !wetOpen}
              />
            </Field>
          </div>

          {/* Where the piece is: not in the water yet, in it, or ready to come out. */}
          {!drySaved && (
            <p className="mb-4 text-sm text-ink-muted">{t('rolls.soakDryFirst')}</p>
          )}
          {soaking && (
            <p
              role="timer"
              className="mb-4 flex items-baseline justify-between gap-3 rounded-control border border-s-4 border-brand-200 border-s-brand-600 bg-brand-50 px-4 py-3 text-sm font-medium text-brand-700"
            >
              <span>{t('rolls.soaking')}</span>
              <span className="font-mono text-xl font-bold tabular-nums">
                {formatCountdown(dueAt - now)}
              </span>
            </p>
          )}
          {drySaved && dueAt !== null && !soaking && (
            <p className="mb-4 rounded-control border border-s-4 border-ok/30 border-s-ok bg-ok-soft px-4 py-3 text-sm font-medium text-ok">
              {t('rolls.soakDone')}
            </p>
          )}

          {absorbencyBackwards && (
            <p className="mb-4 rounded-control border border-s-4 border-warn/30 border-s-warn bg-warn-soft px-4 py-3 text-sm font-medium text-warn">
              {t('rolls.absorbencyBackwards')}
            </p>
          )}

          <div className="mb-4 rounded-control bg-canvas px-4 py-3">
            <div className="flex items-baseline justify-between gap-3">
              <span className="text-sm font-medium text-ink-soft">
                {t('rolls.absorbencyPercent')}
              </span>
              <span className="text-lg font-bold text-ink">
                {absorbency === null ? '—' : `${String(absorbency)}%`}
              </span>
            </div>
            <p className="mt-1 text-xs text-ink-muted">{t('rolls.absorbencyNote')}</p>
          </div>
        </>
      )}

      <div className="mb-4">
        <label className="field-label" htmlFor="test-notes">
          {t('field.note')} <span className="font-normal text-ink-muted">(optional)</span>
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

      {anyBad && (
        <p className="mb-4 rounded-control border border-s-4 border-warn/30 border-s-warn bg-warn-soft px-4 py-3 text-sm font-medium text-warn">
          {t('refusal.roll.readingNotPositive')}
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

      <button
        type="submit"
        className="btn-primary"
        disabled={isSaving || typed.length === 0 || anyBad || absorbencyBackwards}
      >
        {isSaving ? t('common.saving') : t('common.save')}
      </button>
      <p className="mt-2 text-xs text-ink-muted">
        {missing.length === 0
          ? t('rolls.testAllIn')
          : `${t('rolls.stillToTake')} ${missing.map((key) => t(key)).join(', ')}. ${t('rolls.savedAsYouGo')}`}
      </p>
    </form>
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

function NumberBox({
  id,
  value,
  onChange,
  disabled,
  className = 'field-input',
}: {
  id: string;
  value: string;
  onChange: (value: string) => void;
  disabled: boolean;
  className?: string;
}): ReactElement {
  return (
    <input
      id={id}
      type="number"
      inputMode="decimal"
      step="0.001"
      min="0"
      className={className}
      value={value}
      disabled={disabled}
      onChange={(event) => {
        onChange(event.target.value);
      }}
    />
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
      <NumberBox
        id={id}
        value={value}
        onChange={onChange}
        disabled={disabled}
        className="field-input text-center"
      />
    </div>
  );
}
