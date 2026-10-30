import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { Modal } from '../../components/ui/Modal';
import { useTranslation } from '../../hooks/useTranslation';
import { ApiError } from '../../lib/apiClient';
import { staffApi, type StaffMemberDto } from './api';

/**
 * Staff without an account, on the Users screen (specification section 19.19): add one,
 * correct a name or number, or record that he has left.
 *
 * Nobody is deleted — the shifts he worked go on naming him. Most are added from a
 * line's shift configuration by whoever fills it in; this is where the list is kept tidy.
 */
export function StaffList(): ReactElement {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState<StaffMemberDto | 'new' | null>(null);

  const staff = useQuery({
    queryKey: ['staff', 'all'],
    queryFn: () => staffApi.list(true),
  });

  return (
    <section className="mt-8">
      <div className="mb-3 flex flex-wrap items-end justify-between gap-3">
        <div>
          <h2 className="text-lg font-bold text-ink">{t('staff.listTitle')}</h2>
          <p className="text-sm text-ink-muted">{t('staff.listNote')}</p>
        </div>
        <button
          type="button"
          className="btn-primary w-auto px-5"
          onClick={() => {
            setEditing('new');
          }}
        >
          {t('staff.addNew')}
        </button>
      </div>

      {staff.isPending && <p className="p-6 text-ink-muted">{t('common.loading')}</p>}
      {staff.isError && <p className="p-6 text-bad">{t('staff.loadFailed')}</p>}

      {staff.data !== undefined && (
        <div className="card overflow-x-auto">
          <table className="w-full text-start text-sm">
            <thead>
              <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
                <th className="px-4 py-3 font-semibold">{t('field.employeeNumber')}</th>
                <th className="px-4 py-3 font-semibold">{t('field.name')}</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody>
              {staff.data.length === 0 && (
                <tr>
                  <td colSpan={3} className="px-4 py-8 text-center text-ink-muted">
                    {t('staff.none')}
                  </td>
                </tr>
              )}
              {staff.data.map((member) => (
                <tr
                  key={member.id}
                  className={[
                    'border-b border-line last:border-0',
                    member.isActive ? '' : 'text-ink-muted',
                  ].join(' ')}
                >
                  <td className="px-4 py-3 font-mono font-semibold text-ink">
                    {member.employeeNumber ?? '—'}
                  </td>
                  <td className="px-4 py-3">
                    <span className={member.isActive ? 'text-ink' : ''}>
                      {member.fullName}
                    </span>
                    {!member.isActive && (
                      <span className="ms-2 rounded-full bg-canvas px-2 py-0.5 text-xs font-semibold text-ink-muted">
                        {t('staff.left')}
                      </span>
                    )}
                  </td>
                  <td className="px-4 py-3 text-end">
                    <button
                      type="button"
                      className="min-h-9 rounded-control border border-line px-3 text-sm font-medium whitespace-nowrap text-ink-soft transition-colors hover:border-brand-200 hover:bg-brand-50 hover:text-brand-700"
                      onClick={() => {
                        setEditing(member);
                      }}
                    >
                      {t('action.edit')}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {editing !== null && (
        <StaffDialog
          member={editing === 'new' ? null : editing}
          onClose={() => {
            setEditing(null);
          }}
          onSaved={() => {
            void queryClient.invalidateQueries({ queryKey: ['staff'] });
          }}
        />
      )}
    </section>
  );
}

function StaffDialog({
  member,
  onClose,
  onSaved,
}: {
  member: StaffMemberDto | null;
  onClose: () => void;
  onSaved: () => void;
}): ReactElement {
  const { t } = useTranslation();
  const [fullName, setFullName] = useState(member?.fullName ?? '');
  const [number, setNumber] = useState(member?.employeeNumber ?? '');
  const [isActive, setIsActive] = useState(member?.isActive ?? true);
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  async function save(): Promise<void> {
    setError(null);
    setIsSaving(true);
    try {
      const body = {
        fullName: fullName.trim(),
        employeeNumber: number.trim() === '' ? null : number.trim(),
        isActive,
      };
      if (member === null) {
        await staffApi.create(body);
      } else {
        await staffApi.update(member.id, body);
      }
      onSaved();
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
    <Modal
      title={
        member === null ? t('staff.addNew') : `${t('action.edit')} — ${member.fullName}`
      }
      onClose={onClose}
    >
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void save();
        }}
        noValidate
      >
        <div className="mb-4">
          <label className="field-label" htmlFor="staff-name">
            {t('field.name')}
          </label>
          <input
            id="staff-name"
            className="field-input"
            maxLength={100}
            value={fullName}
            disabled={isSaving}
            onChange={(event) => {
              setFullName(event.target.value);
            }}
          />
        </div>

        <div className="mb-4">
          <label className="field-label" htmlFor="staff-number">
            {t('staff.numberOptional')}
          </label>
          <input
            id="staff-number"
            className="field-input font-mono"
            maxLength={20}
            value={number}
            disabled={isSaving}
            onChange={(event) => {
              setNumber(event.target.value);
            }}
          />
        </div>

        <label className="mb-4 flex items-center gap-3 text-sm font-medium text-ink">
          <input
            type="checkbox"
            className="size-5"
            checked={isActive}
            disabled={isSaving}
            onChange={(event) => {
              setIsActive(event.target.checked);
            }}
          />
          {t('staff.stillWorksHere')}
        </label>

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
          disabled={isSaving || fullName.trim() === ''}
        >
          {isSaving ? t('common.saving') : t('common.save')}
        </button>
      </form>
    </Modal>
  );
}
