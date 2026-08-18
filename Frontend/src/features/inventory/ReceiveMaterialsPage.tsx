import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from '../../hooks/useTranslation';
import { PageHeader } from '../../components/ui/PageHeader';
import { ApiError } from '../../lib/apiClient';
import { inventoryApi, type MaterialStockDto } from './api';

/**
 * Booking a delivery in (specification section 6).
 *
 * Pick the material, pick the unit it arrived in, type the number counted. The system
 * converts to the base unit and posts a Receive movement — the storekeeper never does
 * the arithmetic, so a pallet is never mistaken for a kilogram.
 */
export function ReceiveMaterialsPage(): ReactElement {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  const [materialId, setMaterialId] = useState<number | null>(null);
  // Only what the storekeeper picked. The unit actually in use is derived below, so
  // choosing a different material cannot leave a stale unit selected.
  const [chosenUnitId, setChosenUnitId] = useState<number | null>(null);
  const [quantity, setQuantity] = useState('');
  const [notes, setNotes] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [received, setReceived] = useState<MaterialStockDto | null>(null);

  const stock = useQuery({
    queryKey: ['inventory', false],
    queryFn: () => inventoryApi.stock(false),
  });

  const units = useQuery({
    queryKey: ['receiving-units', materialId],
    queryFn: () => inventoryApi.receivingUnits(materialId ?? 0),
    enabled: materialId !== null,
  });

  // The unit the factory usually receives this material in, unless the storekeeper
  // picked another. Derived rather than copied into state on every material change —
  // one source of truth, and no stale selection to clear.
  const defaultUnitId =
    (units.data?.find((u) => u.isDefault) ?? units.data?.[0])?.unitId ?? null;
  const unitId =
    chosenUnitId !== null && units.data?.some((u) => u.unitId === chosenUnitId)
      ? chosenUnitId
      : defaultUnitId;

  const material = stock.data?.find((m) => m.materialId === materialId) ?? null;
  const unit = units.data?.find((u) => u.unitId === unitId) ?? null;

  const quantityNumber = Number(quantity);
  const isQuantity =
    quantity.trim() !== '' && Number.isFinite(quantityNumber) && quantityNumber > 0;
  const inBaseUnit =
    isQuantity && unit !== null ? quantityNumber * unit.quantityInBaseUnit : 0;

  const receive = useMutation({
    mutationFn: () =>
      inventoryApi.receive({
        materialId: materialId ?? 0,
        unitId: unitId ?? 0,
        quantity: quantityNumber,
        notes: notes.trim() === '' ? null : notes.trim(),
      }),
    onSuccess: (updated) => {
      setError(null);
      setReceived(updated);
      setQuantity('');
      setNotes('');
      void queryClient.invalidateQueries({ queryKey: ['inventory'] });
      void queryClient.invalidateQueries({ queryKey: ['inventory-movements'] });
    },
    onError: (caught) => {
      setError(caught instanceof ApiError ? caught.message : 'Something went wrong.');
    },
  });

  if (stock.isPending) {
    return <p className="p-6 text-ink-muted">Loading…</p>;
  }

  if (stock.isError) {
    return <p className="p-6 text-bad">Could not load the materials.</p>;
  }

  return (
    <>
      <PageHeader
        title={t('page.receive.title')}
        subtitle={t('page.receive.subtitle')}
        actions={
          <button
            type="button"
            className="min-h-touch rounded-control border border-line px-5 text-sm font-semibold text-ink-soft transition-colors hover:bg-canvas"
            onClick={() => {
              void navigate('/inventory');
            }}
          >
            Back to the store
          </button>
        }
      />

      <div className="card max-w-2xl p-6">
        <form
          onSubmit={(event) => {
            event.preventDefault();
            receive.mutate();
          }}
          noValidate
        >
          <div className="mb-4">
            <label className="field-label" htmlFor="rec-material">
              {t('term.material')}
            </label>
            <select
              id="rec-material"
              className="field-input"
              value={materialId ?? ''}
              disabled={receive.isPending}
              onChange={(event) => {
                setMaterialId(
                  event.target.value === '' ? null : Number(event.target.value),
                );
                setChosenUnitId(null);
                setReceived(null);
                setError(null);
              }}
            >
              <option value="">Choose a material…</option>
              {stock.data.map((row) => (
                <option key={row.materialId} value={row.materialId}>
                  {row.code} — {row.name}
                </option>
              ))}
            </select>
          </div>

          {material !== null && (
            <p className="mb-4 rounded-control bg-canvas px-4 py-3 text-sm text-ink-soft">
              The store holds{' '}
              <strong className="text-ink">
                {material.currentQuantity} {material.baseUnitSymbol}
              </strong>{' '}
              of {material.name}
              {material.isBelowMinimum && (
                <span className="font-semibold text-warn">
                  {' '}
                  — below its minimum of {material.minQuantity}
                </span>
              )}
            </p>
          )}

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="mb-4">
              <label className="field-label" htmlFor="rec-unit">
                Arrived as
              </label>
              <select
                id="rec-unit"
                className="field-input"
                value={unitId ?? ''}
                disabled={materialId === null || units.isPending || receive.isPending}
                onChange={(event) => {
                  setChosenUnitId(
                    event.target.value === '' ? null : Number(event.target.value),
                  );
                }}
              >
                {units.data?.map((u) => (
                  <option key={u.unitId} value={u.unitId}>
                    {u.unitName}
                    {u.quantityInBaseUnit !== 1 &&
                      ` (${String(u.quantityInBaseUnit)} ${material?.baseUnitSymbol ?? ''})`}
                  </option>
                ))}
              </select>
            </div>

            <div className="mb-4">
              <label className="field-label" htmlFor="rec-quantity">
                How many?
              </label>
              <input
                id="rec-quantity"
                type="number"
                step="0.001"
                min="0"
                className="field-input"
                value={quantity}
                disabled={materialId === null || receive.isPending}
                onChange={(event) => {
                  setQuantity(event.target.value);
                  setReceived(null);
                }}
              />
            </div>
          </div>

          {/* The conversion, shown before saving — the storekeeper sees what will
              land in the store rather than trusting it. */}
          {isQuantity &&
            unit !== null &&
            material !== null &&
            unit.quantityInBaseUnit !== 1 && (
              <p className="mb-4 rounded-control bg-brand-50 px-4 py-3 text-sm font-medium text-brand-700">
                {quantityNumber} × {unit.quantityInBaseUnit} ={' '}
                <strong>{inBaseUnit}</strong> {material.baseUnitSymbol} into the store
              </p>
            )}

          <div className="mb-4">
            <label className="field-label" htmlFor="rec-notes">
              {t('field.note')} <span className="font-normal text-ink-muted">(optional)</span>
            </label>
            <input
              id="rec-notes"
              className="field-input"
              maxLength={300}
              value={notes}
              disabled={receive.isPending}
              placeholder="Supplier, delivery note number…"
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

          {received !== null && error === null && (
            <p className="mb-4 rounded-control border border-s-4 border-ok/30 border-s-ok bg-ok-soft px-4 py-3 text-sm font-medium text-ok">
              Booked in. {received.name} now stands at {received.currentQuantity}{' '}
              {received.baseUnitSymbol}.
            </p>
          )}

          <button
            type="submit"
            className="btn-primary"
            disabled={
              receive.isPending || materialId === null || unitId === null || !isQuantity
            }
          >
            {receive.isPending ? 'Booking in…' : 'Receive'}
          </button>
        </form>
      </div>
    </>
  );
}
