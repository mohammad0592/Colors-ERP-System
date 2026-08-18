import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { useTranslation } from '../../hooks/useTranslation';
import { PageHeader } from '../../components/ui/PageHeader';
import { useAuth } from '../../hooks/useAuth';
import { RoleNames } from '../../lib/roles';
import { formatDate } from '../shifts/shiftFormat';
import { productionApi, type RollDto, type RollSummaryDto } from './api';
import { RollStatusBadge } from './RollStatusBadge';
import { RollTestDialog } from './RollTestDialog';

/**
 * Roll measurements (specification section 8).
 *
 * Rolls waiting to be measured lead, because until they are the thermo cannot touch
 * them — and once a roll is formed into plates there is nothing left to measure.
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
    queryFn: () => productionApi.rolls(undefined, waitingOnly),
  });

  function invalidate(): void {
    void queryClient.invalidateQueries({ queryKey: ['rolls'] });
    void queryClient.invalidateQueries({ queryKey: ['batches'] });
  }

  if (rolls.isPending) {
    return <p className="p-6 text-ink-muted">Loading…</p>;
  }

  if (rolls.isError) {
    return <p className="p-6 text-bad">Could not load the rolls.</p>;
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
          label="Every roll"
          active={!waitingOnly}
          onClick={() => {
            setWaitingOnly(false);
          }}
        />
      </section>

      {justSaved !== null && (
        <p className="mb-4 rounded-control border border-s-4 border-ok/30 border-s-ok bg-ok-soft px-4 py-3 text-sm font-medium text-ok">
          {t('term.roll')} <strong className="font-mono">{justSaved.rollCode}</strong> measured —
          average thickness {justSaved.testReport?.averageThickness}. The thermo can use
          it now.
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
              <th className="px-4 py-3 text-end font-semibold">Avg thickness</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody>
            {rolls.data.length === 0 && (
              <tr>
                <td colSpan={8} className="px-4 py-8 text-center text-ink-muted">
                  {waitingOnly
                    ? 'Every roll has been measured.'
                    : 'No rolls have been made yet.'}
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
                <td className="px-4 py-3 text-end tabular-nums text-ink-soft">
                  {roll.averageThickness ?? '—'}
                </td>
                <td className="px-4 py-3">
                  <div className="flex justify-end">
                    {roll.needsTest && canTest && (
                      <button
                        type="button"
                        className="min-h-9 rounded-control border border-brand-600 bg-brand-600 px-3 text-sm font-medium whitespace-nowrap text-white transition-colors hover:bg-brand-700"
                        onClick={() => {
                          setMeasuring(roll);
                        }}
                      >
                        Measure
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
