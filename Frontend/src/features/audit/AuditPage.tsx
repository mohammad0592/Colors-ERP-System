import { useQuery } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { useTranslation } from '../../hooks/useTranslation';
import { PageHeader } from '../../components/ui/PageHeader';
import { shiftReportsApi } from '../shifts/api';
import { formatDate } from '../shifts/shiftFormat';
import { auditApi, type AuditEntryDto } from './api';
import { describe, filterableThings } from './auditWords';

/**
 * The audit log (specification section 15).
 *
 * Two kinds of line, and the second is the reason a supervisor comes here:
 *
 * <b>Decisions and corrections</b> — a recipe changed, a shift reopened, a bag taken back
 * off a pallet. The record itself shows only the result, never that it was changed or by
 * whom.
 *
 * <b>Refusals</b> — which changed nothing anywhere, so without this line they never
 * happened. One is a man with the wrong bag in his hand. Twenty in an evening is a man
 * who needs help, or a label printer that is failing.
 *
 * Read-only, and deliberately so. There is nothing on this screen that writes a line and
 * nothing that removes one.
 */
export function AuditPage(): ReactElement {
  const { t } = useTranslation();
  const [shiftReportId, setShiftReportId] = useState<number | ''>('');
  const [objectType, setObjectType] = useState('');
  const [refusalsOnly, setRefusalsOnly] = useState(false);

  const shifts = useQuery({
    queryKey: ['shift-reports', 'for-audit'],
    queryFn: () => shiftReportsApi.list(),
  });

  const lines = useQuery({
    queryKey: ['audit', shiftReportId, objectType, refusalsOnly],
    queryFn: () =>
      auditApi.list({
        ...(shiftReportId === '' ? {} : { shiftReportId }),
        objectType,
        refusalsOnly,
        take: 300,
      }),
  });

  const refused = (lines.data ?? []).filter((l) => l.result === 'Rejected').length;

  return (
    <>
      <PageHeader title={t('page.audit.title')} subtitle={t('page.audit.subtitle')} />

      <section className="card mb-6 flex flex-wrap items-end gap-4 p-4">
        <div>
          <label className="field-label" htmlFor="audit-shift">
            {t('term.shift')}
          </label>
          <select
            id="audit-shift"
            className="field-input"
            value={shiftReportId}
            onChange={(event) => {
              setShiftReportId(
                event.target.value === '' ? '' : Number(event.target.value),
              );
            }}
          >
            <option value="">{t('audit.everyShift')}</option>
            {(shifts.data ?? []).map((shift) => (
              <option key={shift.id} value={shift.id}>
                {formatDate(shift.productionDate)} · shift {shift.shiftName}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label className="field-label" htmlFor="audit-thing">
            {t('audit.about')}
          </label>
          <select
            id="audit-thing"
            className="field-input"
            value={objectType}
            onChange={(event) => {
              setObjectType(event.target.value);
            }}
          >
            <option value="">{t('audit.anything')}</option>
            {filterableThings.map((thing) => (
              <option key={thing.value} value={thing.value}>
                {thing.label}
              </option>
            ))}
          </select>
        </div>

        <label className="flex min-h-11 items-center gap-2 rounded-control border border-line px-3 text-sm">
          <input
            type="checkbox"
            checked={refusalsOnly}
            onChange={(event) => {
              setRefusalsOnly(event.target.checked);
            }}
          />
          <span className="text-ink">{t('audit.refusedOnly')}</span>
        </label>
      </section>

      {lines.isPending && <p className="p-6 text-ink-muted">{t('common.loading')}</p>}
      {lines.isError && <p className="p-6 text-bad">{t('audit.loadFailed')}</p>}

      {lines.data !== undefined && (
        <>
          {/* Said plainly, because a handful of refusals is normal and a pile of them is
              the thing this screen exists to surface. */}
          {!refusalsOnly && refused > 0 && (
            <p className="mb-4 rounded-control border border-line bg-canvas px-4 py-3 text-sm text-ink-soft">
              {refused} of these {refused === 1 ? 'was' : 'were'} refused. Tick the box
              above to see only those.
            </p>
          )}

          {lines.data.length === 0 ? (
            <p className="card p-8 text-center text-ink-muted">
              {t('audit.none')}
            </p>
          ) : (
            <div className="card overflow-x-auto">
              <table className="w-full text-start text-sm">
                <thead>
                  <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
                    <th className="px-4 py-3 font-semibold">{t('field.when')}</th>
                    <th className="px-4 py-3 font-semibold">{t('audit.who')}</th>
                    <th className="px-4 py-3 font-semibold">{t('audit.what')}</th>
                    <th className="px-4 py-3 font-semibold">{t('term.shift')}</th>
                    <th className="px-4 py-3 font-semibold">{t('audit.details')}</th>
                  </tr>
                </thead>
                <tbody>
                  {lines.data.map((line) => (
                    <Line key={line.id} line={line} />
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {lines.data.length >= 300 && (
            <p className="mt-3 text-sm text-ink-muted">
              {t('audit.newest300')}
            </p>
          )}
        </>
      )}
    </>
  );
}

function Line({ line }: { line: AuditEntryDto }): ReactElement {
  const rejected = line.result === 'Rejected';
  const when = new Date(line.timestamp);

  return (
    <tr className="border-b border-line last:border-0">
      <td className="px-4 py-3 whitespace-nowrap text-ink-soft">
        {when.toLocaleDateString('en-GB')}{' '}
        <span className="tabular-nums">
          {when.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })}
        </span>
      </td>
      <td className="px-4 py-3 text-ink-soft">
        {/* Nobody signed in means a seeder or a migration, which is the truth rather
            than a gap. */}
        {line.userName ?? <span className="text-ink-muted">the system</span>}
      </td>
      <td className="px-4 py-3">
        <span className="font-medium text-ink">
          {describe(line.action, line.objectType, line.objectId, rejected)}
        </span>
        {rejected && (
          <span className="ms-2 rounded-full bg-warn-soft px-2 py-0.5 text-xs font-semibold text-warn">
            refused
          </span>
        )}
      </td>
      <td className="px-4 py-3 whitespace-nowrap text-ink-muted">
        {line.shiftLabel ?? '—'}
      </td>
      <td className="px-4 py-3 text-ink-muted">{line.details ?? '—'}</td>
    </tr>
  );
}
