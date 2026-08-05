import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { ConfirmDialog, type ConfirmRequest } from '../../components/ui/ConfirmDialog';
import { Modal } from '../../components/ui/Modal';
import { ApiError } from '../../lib/apiClient';
import type { CrudClient, LookupDto } from './api';

/** One input in the add/edit dialog, and one column in the table. */
export interface FieldDef {
  key: string;
  label: string;
  type?: 'text' | 'time' | 'checkbox';
  maxLength?: number;
  placeholder?: string;
  hint?: string;
}

/** Rows carry only strings and numbers in their listed fields; anything else shows blank. */
function cellText(value: unknown): string {
  if (typeof value === 'string') return value;
  if (typeof value === 'number') return String(value);
  if (typeof value === 'boolean') return value ? 'Yes' : 'No';
  return '';
}

/**
 * A checkbox travels through the dialog as the text "true" or "false", because the
 * dialog holds every field as text. It becomes a real boolean again on the way out.
 */
function isChecked(value: unknown): boolean {
  return value === true || value === 'true';
}

interface LookupTabProps<
  TDto extends LookupDto,
  TSave extends Record<string, string | boolean>,
> {
  /** Cache key; also invalidated after every change. */
  queryKey: string;
  client: CrudClient<TDto, TSave>;
  fields: FieldDef[];
  /** Named from the worker's point of view: "unit", "colour", "shift". */
  itemWord: string;
  /** Overrides the default "+s" plural — "category" becomes "categories". */
  itemWordPlural?: string;
}

/**
 * The whole management screen for one simple list — table, add/edit dialog,
 * activate/deactivate — driven by a field list, so eight lists are configuration
 * rather than eight screens.
 */
export function LookupTab<
  TDto extends LookupDto,
  TSave extends Record<string, string | boolean>,
>({
  queryKey,
  client,
  fields,
  itemWord,
  itemWordPlural,
}: LookupTabProps<TDto, TSave>): ReactElement {
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState<TDto | 'new' | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [confirm, setConfirm] = useState<ConfirmRequest | null>(null);

  // The management screen always shows inactive rows too — hiding them here would
  // make "bring it back" impossible.
  const query = useQuery({
    queryKey: [queryKey],
    queryFn: () => client.list(true),
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: [queryKey] });

  const setActive = useMutation({
    mutationFn: ({ id, isActive }: { id: number; isActive: boolean }) =>
      client.setActive(id, isActive),
    onSettled: invalidate,
  });

  const remove = useMutation({
    mutationFn: (id: number) => client.remove(id),
    onSuccess: () => {
      setActionError(null);
    },
    onError: (caught) => {
      // The usual refusal: the row is referenced. The server's message names
      // what uses it and points at Deactivate.
      setActionError(caught instanceof ApiError ? caught.message : 'Could not delete.');
    },
    onSettled: invalidate,
  });

  if (query.isPending) {
    return <p className="p-6 text-ink-muted">Loading…</p>;
  }

  if (query.isError) {
    return (
      <p className="p-6 text-bad">
        Could not load. {query.error instanceof ApiError ? query.error.message : ''}
      </p>
    );
  }

  return (
    <div>
      <div className="mb-4 flex items-center justify-between gap-3">
        <p className="text-sm text-ink-muted">
          {query.data.length}{' '}
          {query.data.length === 1 ? itemWord : (itemWordPlural ?? `${itemWord}s`)}
        </p>
        <button
          type="button"
          className="btn-primary h-touch w-auto px-5 text-base"
          onClick={() => {
            setEditing('new');
          }}
        >
          Add {itemWord}
        </button>
      </div>

      {actionError !== null && (
        <p
          role="alert"
          className="mb-4 rounded-control border border-l-4 border-bad/30 border-l-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
        >
          {actionError}
        </p>
      )}

      <div className="card overflow-x-auto">
        <table className="w-full text-left text-sm">
          <thead>
            <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
              {fields.map((field) => (
                <th key={field.key} className="px-4 py-3 font-semibold">
                  {field.label}
                </th>
              ))}
              <th className="px-4 py-3 font-semibold">Status</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody>
            {query.data.map((row) => (
              <tr key={row.id} className="border-b border-line last:border-0">
                {fields.map((field) => (
                  <td key={field.key} className="px-4 py-3 font-medium text-ink">
                    {cellText((row as Record<string, unknown>)[field.key])}
                  </td>
                ))}
                <td className="px-4 py-3">
                  <StatusBadge isActive={row.isActive} />
                </td>
                <td className="px-4 py-3">
                  <div className="flex justify-end gap-2">
                    <RowButton
                      label="Edit"
                      onClick={() => {
                        setEditing(row);
                      }}
                    />
                    <RowButton
                      label={row.isActive ? 'Deactivate' : 'Activate'}
                      onClick={() => {
                        setActive.mutate({ id: row.id, isActive: !row.isActive });
                      }}
                    />
                    {/* Only offered when nothing references the row — a button
                        that always fails is worse than no button. */}
                    {row.canDelete && (
                      <RowButton
                        label="Delete"
                        tone="danger"
                        onClick={() => {
                          setConfirm({
                            title: `Delete ${itemWord}?`,
                            message: (
                              <>
                                <strong>{row.name}</strong> will be removed for good.
                                Nothing uses it, so no records are affected.
                              </>
                            ),
                            confirmLabel: 'Delete',
                            onConfirm: () => {
                              remove.mutate(row.id);
                            },
                          });
                        }}
                      />
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {confirm !== null && (
        <ConfirmDialog
          request={confirm}
          onCancel={() => {
            setConfirm(null);
          }}
        />
      )}

      {editing !== null && (
        <EditDialog
          title={editing === 'new' ? `Add ${itemWord}` : `Edit ${itemWord}`}
          fields={fields}
          initial={
            editing === 'new' ? null : (editing as unknown as Record<string, unknown>)
          }
          onClose={() => {
            setEditing(null);
          }}
          onSave={async (values) => {
            // The dialog collects strings by field key; the API client's save type is
            // exactly those keys, with checkboxes turned back into real booleans. The
            // cast is localized here on purpose.
            const body = Object.fromEntries(
              fields.map((field) => [
                field.key,
                field.type === 'checkbox'
                  ? isChecked(values[field.key])
                  : (values[field.key] ?? ''),
              ]),
            ) as TSave;
            if (editing === 'new') {
              await client.create(body);
            } else {
              await client.update(editing.id, body);
            }
            await invalidate();
          }}
        />
      )}
    </div>
  );
}

export function StatusBadge({ isActive }: { isActive: boolean }): ReactElement {
  return (
    <span
      className={[
        'rounded-full px-2.5 py-0.5 text-xs font-semibold',
        isActive ? 'bg-ok-soft text-ok' : 'bg-line text-ink-muted',
      ].join(' ')}
    >
      {isActive ? 'Active' : 'Inactive'}
    </span>
  );
}

export function RowButton({
  label,
  onClick,
  tone = 'normal',
}: {
  label: string;
  onClick: () => void;
  tone?: 'normal' | 'danger';
}): ReactElement {
  return (
    <button
      type="button"
      onClick={onClick}
      className={[
        'min-h-9 rounded-control border border-line px-3 text-sm font-medium transition-colors',
        tone === 'danger'
          ? 'text-ink-muted hover:border-bad/40 hover:bg-bad-soft hover:text-bad'
          : 'text-ink-soft hover:border-brand-200 hover:bg-brand-50 hover:text-brand-700',
      ].join(' ')}
    >
      {label}
    </button>
  );
}

interface EditDialogProps {
  title: string;
  fields: FieldDef[];
  initial: Record<string, unknown> | null;
  onClose: () => void;
  onSave: (values: Record<string, string>) => Promise<void>;
}

function EditDialog({
  title,
  fields,
  initial,
  onClose,
  onSave,
}: EditDialogProps): ReactElement {
  const [values, setValues] = useState<Record<string, string>>(() =>
    Object.fromEntries(
      fields.map((f) => [
        f.key,
        f.type === 'checkbox'
          ? String(isChecked(initial?.[f.key]))
          : cellText(initial?.[f.key]),
      ]),
    ),
  );
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  async function save(): Promise<void> {
    setError(null);
    setIsSaving(true);
    try {
      await onSave(values);
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
    <Modal title={title} onClose={onClose}>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void save();
        }}
        noValidate
      >
        {fields.map((field) =>
          field.type === 'checkbox' ? (
            <div key={field.key} className="mb-4">
              <label
                className="flex items-center gap-3 text-sm font-medium text-ink"
                htmlFor={`fld-${field.key}`}
              >
                <input
                  id={`fld-${field.key}`}
                  type="checkbox"
                  className="size-5"
                  checked={isChecked(values[field.key])}
                  disabled={isSaving}
                  onChange={(event) => {
                    setValues((current) => ({
                      ...current,
                      [field.key]: String(event.target.checked),
                    }));
                  }}
                />
                {field.label}
              </label>
              {field.hint !== undefined && (
                <p className="mt-1 ml-8 text-xs text-ink-muted">{field.hint}</p>
              )}
            </div>
          ) : (
            <div key={field.key} className="mb-4">
              <label className="field-label" htmlFor={`fld-${field.key}`}>
                {field.label}
              </label>
              <input
                id={`fld-${field.key}`}
                type={field.type ?? 'text'}
                className="field-input"
                value={values[field.key] ?? ''}
                maxLength={field.maxLength}
                placeholder={field.placeholder}
                onChange={(event) => {
                  setValues((current) => ({
                    ...current,
                    [field.key]: event.target.value,
                  }));
                }}
                disabled={isSaving}
                required
              />
              {field.hint !== undefined && (
                <p className="mt-1 text-xs text-ink-muted">{field.hint}</p>
              )}
            </div>
          ),
        )}

        {error !== null && (
          <p
            role="alert"
            className="mb-4 rounded-control border border-l-4 border-bad/30 border-l-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
          >
            {error}
          </p>
        )}

        <button type="submit" className="btn-primary" disabled={isSaving}>
          {isSaving ? 'Saving…' : 'Save'}
        </button>
      </form>
    </Modal>
  );
}
