import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { ConfirmDialog, type ConfirmRequest } from '../../components/ui/ConfirmDialog';
import { Modal } from '../../components/ui/Modal';
import { ApiError } from '../../lib/apiClient';
import {
  materialCategoriesApi,
  materialsApi,
  unitsApi,
  type MaterialDto,
  type SaveMaterial,
} from './api';
import { RowButton, StatusBadge } from './LookupTab';

/**
 * Materials, with their pack sizes edited in place — the storekeeper's
 * "1 pallet = 750 kg" lives on the material itself (specification section 4).
 */
export function MaterialsTab(): ReactElement {
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState<MaterialDto | 'new' | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [confirm, setConfirm] = useState<ConfirmRequest | null>(null);

  const materials = useQuery({
    queryKey: ['materials'],
    queryFn: () => materialsApi.list(true),
  });

  // Pickers offer active rows only; an inactive category cannot gain new materials.
  const categories = useQuery({
    queryKey: ['material-categories', 'active'],
    queryFn: () => materialCategoriesApi.list(false),
  });
  const units = useQuery({
    queryKey: ['units', 'active'],
    queryFn: () => unitsApi.list(false),
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['materials'] });

  const setActive = useMutation({
    mutationFn: ({ id, isActive }: { id: number; isActive: boolean }) =>
      materialsApi.setActive(id, isActive),
    onSettled: invalidate,
  });

  const remove = useMutation({
    mutationFn: (id: number) => materialsApi.remove(id),
    onSuccess: () => {
      setActionError(null);
    },
    onError: (caught) => {
      setActionError(caught instanceof ApiError ? caught.message : 'Could not delete.');
    },
    onSettled: invalidate,
  });

  if (materials.isPending || categories.isPending || units.isPending) {
    return <p className="p-6 text-ink-muted">Loading…</p>;
  }

  if (materials.isError || categories.isError || units.isError) {
    return <p className="p-6 text-bad">Could not load materials.</p>;
  }

  return (
    <div>
      <div className="mb-4 flex items-center justify-between gap-3">
        <p className="text-sm text-ink-muted">{materials.data.length} materials</p>
        <button
          type="button"
          className="btn-primary h-touch w-auto px-5 text-base"
          onClick={() => {
            setEditing('new');
          }}
        >
          Add material
        </button>
      </div>

      {actionError !== null && (
        <p
          role="alert"
          className="mb-4 rounded-control border border-s-4 border-bad/30 border-s-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
        >
          {actionError}
        </p>
      )}

      <div className="card overflow-x-auto">
        <table className="w-full text-start text-sm">
          <thead>
            <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
              <th className="px-4 py-3 font-semibold">Code</th>
              <th className="px-4 py-3 font-semibold">Name</th>
              <th className="px-4 py-3 font-semibold">Category</th>
              <th className="px-4 py-3 font-semibold">Base unit</th>
              <th className="px-4 py-3 font-semibold">Minimum</th>
              <th className="px-4 py-3 font-semibold">Pack sizes</th>
              <th className="px-4 py-3 font-semibold">Status</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody>
            {materials.data.map((material) => (
              <tr key={material.id} className="border-b border-line last:border-0">
                <td className="px-4 py-3 font-mono text-xs font-semibold text-ink-soft">
                  {material.code}
                </td>
                <td className="px-4 py-3 font-medium text-ink">{material.name}</td>
                <td className="px-4 py-3 text-ink-soft">{material.categoryName}</td>
                <td className="px-4 py-3 text-ink-soft">{material.baseUnitSymbol}</td>
                <td className="px-4 py-3 text-ink-soft">
                  {material.minQuantity} {material.baseUnitSymbol}
                </td>
                <td className="px-4 py-3 text-ink-soft">
                  {material.packagings.length === 0
                    ? '—'
                    : material.packagings
                        .map(
                          (p) =>
                            `1 ${p.unitName} = ${String(p.quantityInBaseUnit)} ${material.baseUnitSymbol}`,
                        )
                        .join(' · ')}
                </td>
                <td className="px-4 py-3">
                  <StatusBadge isActive={material.isActive} />
                </td>
                <td className="px-4 py-3">
                  <div className="flex justify-end gap-2">
                    <RowButton
                      label="Edit"
                      onClick={() => {
                        setEditing(material);
                      }}
                    />
                    <RowButton
                      label={material.isActive ? 'Deactivate' : 'Activate'}
                      onClick={() => {
                        setActive.mutate({
                          id: material.id,
                          isActive: !material.isActive,
                        });
                      }}
                    />
                    {/* Hidden once a recipe uses the material. */}
                    {material.canDelete && (
                      <RowButton
                        label="Delete"
                        tone="danger"
                        onClick={() => {
                          setConfirm({
                            title: 'Delete material?',
                            message: (
                              <>
                                <strong>
                                  {material.code} {material.name}
                                </strong>{' '}
                                will be removed for good. No recipe uses it, so no records
                                are affected.
                              </>
                            ),
                            confirmLabel: 'Delete',
                            onConfirm: () => {
                              remove.mutate(material.id);
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
        <MaterialDialog
          material={editing === 'new' ? null : editing}
          categories={categories.data}
          units={units.data}
          onClose={() => {
            setEditing(null);
          }}
          onSaved={() => {
            void invalidate();
          }}
        />
      )}
    </div>
  );
}

interface PackRow {
  unitId: string;
  quantity: string;
  isDefault: boolean;
}

interface MaterialForm {
  code: string;
  name: string;
  categoryId: string;
  baseUnitId: string;
  minQuantity: string;
  unitWeight: string;
  notes: string;
  packagings: PackRow[];
}

function formFrom(material: MaterialDto | null): MaterialForm {
  if (material === null) {
    return {
      code: '',
      name: '',
      categoryId: '',
      baseUnitId: '',
      minQuantity: '0',
      unitWeight: '',
      notes: '',
      packagings: [],
    };
  }

  return {
    code: material.code,
    name: material.name,
    categoryId: String(material.categoryId),
    baseUnitId: String(material.baseUnitId),
    minQuantity: String(material.minQuantity),
    unitWeight: material.unitWeight === null ? '' : String(material.unitWeight),
    notes: material.notes ?? '',
    packagings: material.packagings.map((p) => ({
      unitId: String(p.unitId),
      quantity: String(p.quantityInBaseUnit),
      isDefault: p.isDefaultReceiving,
    })),
  };
}

interface MaterialDialogProps {
  material: MaterialDto | null;
  categories: { id: number; name: string }[];
  units: { id: number; name: string; symbol: string }[];
  onClose: () => void;
  onSaved: () => void;
}

function MaterialDialog({
  material,
  categories,
  units,
  onClose,
  onSaved,
}: MaterialDialogProps): ReactElement {
  const [form, setForm] = useState<MaterialForm>(() => formFrom(material));
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  const baseUnitSymbol =
    units.find((u) => String(u.id) === form.baseUnitId)?.symbol ?? 'base unit';

  function set<K extends keyof MaterialForm>(key: K, value: MaterialForm[K]): void {
    setForm((current) => ({ ...current, [key]: value }));
  }

  /** The request when the form converts cleanly, or the message to show when not. */
  function buildRequest(): SaveMaterial | string {
    const categoryId = Number(form.categoryId);
    const baseUnitId = Number(form.baseUnitId);
    if (!Number.isInteger(categoryId) || categoryId <= 0) {
      return 'Choose a category.';
    }
    if (!Number.isInteger(baseUnitId) || baseUnitId <= 0) {
      return 'Choose a base unit.';
    }

    const minQuantity = Number(form.minQuantity === '' ? '0' : form.minQuantity);
    if (!Number.isFinite(minQuantity)) {
      return 'The minimum quantity must be a number.';
    }

    const unitWeight = form.unitWeight.trim() === '' ? null : Number(form.unitWeight);
    if (unitWeight !== null && !Number.isFinite(unitWeight)) {
      return 'The unit weight must be a number.';
    }

    const packagings = [];
    for (const row of form.packagings) {
      const unitId = Number(row.unitId);
      const quantity = Number(row.quantity);
      if (!Number.isInteger(unitId) || unitId <= 0) {
        return 'Choose a unit for every pack size.';
      }
      if (!Number.isFinite(quantity) || quantity <= 0) {
        return 'Every pack size needs a quantity above zero.';
      }
      packagings.push({
        unitId,
        quantityInBaseUnit: quantity,
        isDefaultReceiving: row.isDefault,
      });
    }

    return {
      code: form.code,
      name: form.name,
      categoryId,
      baseUnitId,
      minQuantity,
      unitWeight,
      notes: form.notes.trim() === '' ? null : form.notes.trim(),
      packagings,
    };
  }

  async function save(): Promise<void> {
    const request = buildRequest();
    if (typeof request === 'string') {
      setError(request);
      return;
    }

    setError(null);
    setIsSaving(true);
    try {
      if (material === null) {
        await materialsApi.create(request);
      } else {
        await materialsApi.update(material.id, request);
      }
      onSaved();
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
    <Modal title={material === null ? 'Add material' : 'Edit material'} onClose={onClose}>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void save();
        }}
        noValidate
      >
        <div className="mb-4 grid grid-cols-2 gap-4">
          <div>
            <label className="field-label" htmlFor="mat-code">
              Code
            </label>
            <input
              id="mat-code"
              type="text"
              className="field-input"
              placeholder="MAT0015"
              value={form.code}
              onChange={(e) => {
                set('code', e.target.value);
              }}
              disabled={isSaving}
              required
            />
          </div>
          <div>
            <label className="field-label" htmlFor="mat-name">
              Name
            </label>
            <input
              id="mat-name"
              type="text"
              className="field-input"
              value={form.name}
              onChange={(e) => {
                set('name', e.target.value);
              }}
              disabled={isSaving}
              required
            />
          </div>
        </div>

        <div className="mb-4 grid grid-cols-2 gap-4">
          <div>
            <label className="field-label" htmlFor="mat-category">
              Category
            </label>
            <select
              id="mat-category"
              className="field-input"
              value={form.categoryId}
              onChange={(e) => {
                set('categoryId', e.target.value);
              }}
              disabled={isSaving}
            >
              <option value="">Choose…</option>
              {categories.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="field-label" htmlFor="mat-unit">
              Base unit
            </label>
            <select
              id="mat-unit"
              className="field-input"
              value={form.baseUnitId}
              onChange={(e) => {
                set('baseUnitId', e.target.value);
              }}
              disabled={isSaving}
            >
              <option value="">Choose…</option>
              {units.map((u) => (
                <option key={u.id} value={u.id}>
                  {u.name} ({u.symbol})
                </option>
              ))}
            </select>
            <p className="mt-1 text-xs text-ink-muted">
              Stock is always counted in this unit.
            </p>
          </div>
        </div>

        <div className="mb-4 grid grid-cols-2 gap-4">
          <div>
            <label className="field-label" htmlFor="mat-min">
              Minimum quantity
            </label>
            <input
              id="mat-min"
              type="number"
              min="0"
              step="0.0001"
              className="field-input"
              value={form.minQuantity}
              onChange={(e) => {
                set('minQuantity', e.target.value);
              }}
              disabled={isSaving}
            />
            <p className="mt-1 text-xs text-ink-muted">Below this, stock shows as low.</p>
          </div>
          <div>
            <label className="field-label" htmlFor="mat-weight">
              Unit weight ({baseUnitSymbol})
            </label>
            <input
              id="mat-weight"
              type="number"
              min="0"
              step="0.0001"
              className="field-input"
              placeholder="optional"
              value={form.unitWeight}
              onChange={(e) => {
                set('unitWeight', e.target.value);
              }}
              disabled={isSaving}
            />
            <p className="mt-1 text-xs text-ink-muted">
              Weight of one piece, when pieces are also weighed.
            </p>
          </div>
        </div>

        <div className="mb-4">
          <label className="field-label" htmlFor="mat-notes">
            Notes
          </label>
          <input
            id="mat-notes"
            type="text"
            className="field-input"
            value={form.notes}
            onChange={(e) => {
              set('notes', e.target.value);
            }}
            disabled={isSaving}
          />
        </div>

        <fieldset className="mb-4 rounded-control border border-line p-4">
          <legend className="px-1 text-sm font-semibold text-ink-soft">Pack sizes</legend>
          <p className="mb-3 text-xs text-ink-muted">
            How this material arrives. The storekeeper receives packs.
          </p>

          {form.packagings.map((row, index) => (
            <div key={`pack-${String(index)}`} className="mb-2 flex items-center gap-2">
              <select
                aria-label="Pack unit"
                className="field-input h-touch flex-1"
                value={row.unitId}
                onChange={(e) => {
                  const packagings = [...form.packagings];
                  packagings[index] = { ...row, unitId: e.target.value };
                  set('packagings', packagings);
                }}
                disabled={isSaving}
              >
                <option value="">Unit…</option>
                {units
                  .filter((u) => String(u.id) !== form.baseUnitId)
                  .map((u) => (
                    <option key={u.id} value={u.id}>
                      {u.name}
                    </option>
                  ))}
              </select>
              <span className="text-sm text-ink-muted">=</span>
              <input
                aria-label="Quantity in base unit"
                type="number"
                min="0"
                step="0.0001"
                className="field-input h-touch w-28"
                value={row.quantity}
                onChange={(e) => {
                  const packagings = [...form.packagings];
                  packagings[index] = { ...row, quantity: e.target.value };
                  set('packagings', packagings);
                }}
                disabled={isSaving}
              />
              <span className="w-10 text-sm text-ink-muted">{baseUnitSymbol}</span>
              <label className="flex items-center gap-1.5 text-xs text-ink-soft">
                <input
                  type="checkbox"
                  checked={row.isDefault}
                  onChange={(e) => {
                    // Only one pack can be the default, so ticking one clears the rest.
                    set(
                      'packagings',
                      form.packagings.map((p, i) => ({
                        ...p,
                        isDefault: i === index ? e.target.checked : false,
                      })),
                    );
                  }}
                  disabled={isSaving}
                />
                default
              </label>
              <button
                type="button"
                aria-label="Remove pack size"
                className="grid size-9 shrink-0 place-items-center rounded-control text-ink-muted hover:bg-bad-soft hover:text-bad"
                onClick={() => {
                  set(
                    'packagings',
                    form.packagings.filter((_, i) => i !== index),
                  );
                }}
                disabled={isSaving}
              >
                ✕
              </button>
            </div>
          ))}

          <button
            type="button"
            className="mt-1 min-h-9 rounded-control border border-dashed border-line px-3 text-sm font-medium text-ink-soft hover:border-brand-200 hover:text-brand-700"
            onClick={() => {
              set('packagings', [
                ...form.packagings,
                { unitId: '', quantity: '', isDefault: false },
              ]);
            }}
            disabled={isSaving}
          >
            + Add pack size
          </button>
        </fieldset>

        {error !== null && (
          <p
            role="alert"
            className="mb-4 rounded-control border border-s-4 border-bad/30 border-s-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
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
