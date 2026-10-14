import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef, useState, type ReactElement } from 'react';
import { useTranslation } from '../../hooks/useTranslation';
import { PageHeader } from '../../components/ui/PageHeader';
import { useAuth } from '../../hooks/useAuth';
import { RoleNames } from '../../lib/roles';
import { formatDate } from '../shifts/shiftFormat';
import { productionApi, type RollDto, type RollSummaryDto } from './api';
import { RollStatusBadge } from './RollStatusBadge';
import { ThicknessVerdict } from './ThicknessVerdict';
import { RollTestDialog } from './RollTestDialog';
import { beep, formatCountdown, useNow } from './soakTimer';

/**
 * Roll tests (specification section 8).
 *
 * Rolls whose test is not finished lead, because until it is the thermo cannot touch
 * them — and once a roll is formed into plates there is nothing left to measure. A test
 * saved part-way stays here until every reading is in (section 19.15), and an absorbency
 * sample in the water counts down on its row and beeps when it is due out.
 */
export function RollTestsPage(): ReactElement {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const { hasRole } = useAuth();
  const canTest = hasRole(RoleNames.Administrator, RoleNames.ExtruderTestPerson);

  const [waitingOnly, setWaitingOnly] = useState(true);
  const [measuring, setMeasuring] = useState<RollSummaryDto | null>(null);
  const [justSaved, setJustSaved] = useState<RollDto | null>(null);

  const rolls = useQuery({
    queryKey: ['rolls', 'tests', waitingOnly],
    // Waiting is a queue and spans shifts; the other side is this shift only (19.6).
    queryFn: () => productionApi.rolls(undefined, waitingOnly, !waitingOnly),
  });

  function invalidate(): void {
    void queryClient.invalidateQueries({ queryKey: ['rolls'] });
    void queryClient.invalidateQueries({ queryKey: ['batches'] });
    void queryClient.invalidateQueries({ queryKey: ['roll'] });
  }

  // Ticks while any sample is in the water, so its row counts down.
  const now = useNow(rolls.data?.some((r) => r.absorbencyWetDueAt !== null) ?? false);

  // A beep the moment a sample's ten minutes are up — once, and only for one seen
  // soaking while this screen was open, not for every overdue row when it is opened.
  const soakingIds = useRef(new Set<number>());
  useEffect(() => {
    const due = (r: RollSummaryDto): number | null =>
      r.absorbencyWetDueAt === null ? null : Date.parse(r.absorbencyWetDueAt);
    const rows = rolls.data ?? [];
    const stillSoaking = new Set(
      rows.filter((r) => (due(r) ?? 0) > now).map((r) => r.id),
    );
    const cameOut = [...soakingIds.current].some(
      (id) => !stillSoaking.has(id) && rows.some((r) => r.id === id && due(r) !== null),
    );
    if (cameOut) {
      beep();
    }
    soakingIds.current = stillSoaking;
  }, [now, rolls.data]);

  if (rolls.isPending) {
    return <p className="p-6 text-ink-muted">{t('common.loading')}</p>;
  }

  if (rolls.isError) {
    return <p className="p-6 text-bad">{t('rolls.loadFailed')}</p>;
  }

  const waiting = rolls.data.filter((r) => r.needsTest).length;

  return (
    <>
      <PageHeader
        title={t('page.rollTests.title')}
        subtitle={t('page.rollTests.subtitle')}
      />

      <section className="mb-6 flex flex-wrap gap-2">
        <Chip
          label={`Waiting to be measured${waitingOnly ? '' : ` (${String(waiting)})`}`}
          active={waitingOnly}
          onClick={() => {
            setWaitingOnly(true);
          }}
        />
        <Chip
          label={t('common.thisShift')}
          active={!waitingOnly}
          onClick={() => {
            setWaitingOnly(false);
          }}
        />
      </section>

      {justSaved !== null && (
        <p className="mb-4 rounded-control border border-s-4 border-ok/30 border-s-ok bg-ok-soft px-4 py-3 text-sm font-medium text-ok">
          {t('term.roll')} <strong className="font-mono">{justSaved.rollCode}</strong> —{' '}
          {justSaved.testReport?.isComplete === true ? (
            <>
              {t('rolls.testFinished')} {justSaved.testReport.averageThickness} mm
              <ThicknessVerdict inSpec={justSaved.testReport.thicknessInSpec} />
            </>
          ) : justSaved.testReport?.absorbencyWetDueAt != null ? (
            t('rolls.testSavedSoaking')
          ) : (
            t('rolls.testSavedPartly')
          )}
        </p>
      )}

      <div className="card overflow-x-auto">
        <table className="w-full text-start text-sm">
          <thead>
            <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
              <th className="px-4 py-3 font-semibold">{t('term.rollCode')}</th>
              <th className="px-4 py-3 font-semibold">{t('term.recipe')}</th>
              <th className="px-4 py-3 font-semibold">{t('term.colour')}</th>
              <th className="px-4 py-3 font-semibold">{t('field.status')}</th>
              <th className="px-4 py-3 text-end font-semibold">{t('field.weight')}</th>
              <th className="px-4 py-3 text-end font-semibold">{t('field.length')}</th>
              <th className="px-4 py-3 text-end font-semibold">
                {t('rolls.avgThickness')}
              </th>
              <th className="px-4 py-3 text-end font-semibold">
                {t('rolls.absorbencyShort')}
              </th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody>
            {rolls.data.length === 0 && (
              <tr>
                <td colSpan={9} className="px-4 py-8 text-center text-ink-muted">
                  {waitingOnly ? t('rolls.allMeasured') : t('rolls.noneMade')}
                </td>
              </tr>
            )}
            {rolls.data.map((roll) => (
              <tr key={roll.id} className="border-b border-line last:border-0">
                <td className="px-4 py-3 font-mono font-semibold text-ink">
                  {roll.rollCode}
                  <span className="ms-2 font-sans text-xs font-normal text-ink-muted">
                    {formatDate(roll.productionDate)}
                  </span>
                </td>
                <td className="px-4 py-3 text-ink-soft">
                  {roll.recipeNumber}
                  <span className="ms-2 text-xs text-ink-muted">
                    {roll.recipeFamilyName}
                  </span>
                </td>
                <td className="px-4 py-3 text-ink-soft">{roll.colorName}</td>
                <td className="px-4 py-3">
                  <RollStatusBadge status={roll.status} />
                </td>
                <td className="px-4 py-3 text-end tabular-nums text-ink-soft">
                  {roll.weight ?? '—'}
                </td>
                <td className="px-4 py-3 text-end tabular-nums text-ink-soft">
                  {roll.length ?? '—'}
                </td>
                <td className="px-4 py-3 text-end tabular-nums whitespace-nowrap text-ink-soft">
                  {roll.averageThickness ?? '—'}
                  <ThicknessVerdict inSpec={roll.thicknessInSpec} />
                </td>
                <td className="px-4 py-3 text-end tabular-nums whitespace-nowrap text-ink-soft">
                  <Absorbency roll={roll} now={now} />
                </td>
                <td className="px-4 py-3">
                  <div className="flex justify-end">
                    {/* Started, finished or not yet begun, the test is open to correct until
                        the roll goes into the thermo (section 19.15). */}
                    {canTest &&
                      (roll.status === 'NeedsTest' || roll.status === 'Available') && (
                        <button
                          type="button"
                          className={[
                            'min-h-9 rounded-control border px-3 text-sm font-medium whitespace-nowrap transition-colors',
                            roll.needsTest
                              ? 'border-brand-600 bg-brand-600 text-white hover:bg-brand-700'
                              : 'border-line text-ink-soft hover:border-brand-200 hover:bg-brand-50 hover:text-brand-700',
                          ].join(' ')}
                          onClick={() => {
                            setMeasuring(roll);
                          }}
                        >
                          {roll.needsTest ? t('rolls.test') : t('action.edit')}
                        </button>
                      )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {measuring !== null && (
        <RollTestDialog
          roll={measuring}
          onClose={() => {
            setMeasuring(null);
          }}
          onSaved={(roll) => {
            setJustSaved(roll);
            invalidate();
          }}
        />
      )}
    </>
  );
}

/**
 * The absorbency column: the percentage once both weights are in, the time left while the
 * sample is in the water, and a call to weigh it when the time is up.
 */
function Absorbency({ roll, now }: { roll: RollSummaryDto; now: number }): ReactElement {
  const { t } = useTranslation();

  if (roll.absorbencyWetDueAt !== null) {
    const left = Date.parse(roll.absorbencyWetDueAt) - now;
    return left > 0 ? (
      <span role="timer" className="font-mono font-semibold text-brand-700">
        {formatCountdown(left)}
      </span>
    ) : (
      <span className="rounded-full bg-ok-soft px-2.5 py-0.5 text-xs font-semibold text-ok">
        {t('rolls.weighNow')}
      </span>
    );
  }

  return (
    <>
      {roll.absorbencyPercentage === null ? '—' : `${String(roll.absorbencyPercentage)}%`}
    </>
  );
}

function Chip({
  label,
  active,
  onClick,
}: {
  label: string;
  active: boolean;
  onClick: () => void;
}): ReactElement {
  return (
    <button
      type="button"
      onClick={onClick}
      className={[
        'min-h-9 rounded-full border px-4 text-sm font-medium transition-colors',
        active
          ? 'border-brand-600 bg-brand-50 text-brand-700'
          : 'border-line text-ink-soft hover:border-brand-200 hover:bg-brand-50',
      ].join(' ')}
    >
      {label}
    </button>
  );
}
