import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from '../../hooks/useTranslation';
import { useState, type ReactElement } from 'react';
import { ConfirmDialog, type ConfirmRequest } from '../../components/ui/ConfirmDialog';
import { Modal } from '../../components/ui/Modal';
import { ApiError } from '../../lib/apiClient';
import {
  mouldsApi,
  productsApi,
  productTypesApi,
  type ProductDto,
  type SaveProduct,
} from './api';
import { RowButton, StatusBadge } from './LookupTab';

/**
 * The things the factory makes (specification section 4).
 *
 * Its own tab rather than a LookupTab, because a product carries the packing numbers —
 * and those numbers are the whole point: they are what stops 500 and 15 being written
 * into the code when the factory starts making meal boxes.
 */
export function ProductsTab(): ReactElement {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState<ProductDto | 'new' | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [confirm, setConfirm] = useState<ConfirmRequest | null>(null);

  const products = useQuery({
    queryKey: ['products'],
    queryFn: () => productsApi.list(true),
  });

  const moulds = useQuery({ queryKey: ['moulds'], queryFn: () => mouldsApi.list(false) });
  const types = useQuery({
    queryKey: ['product-types'],
    queryFn: () => productTypesApi.list(false),
  });

  function invalidate(): void {
    void queryClient.invalidateQueries({ queryKey: ['products'] });
  }

  const setActive = useMutation({
    mutationFn: ({ id, isActive }: { id: number; isActive: boolean }) =>
      productsApi.setActive(id, isActive),
    onSettled: invalidate,
  });

  const remove = useMutation({
    mutationFn: (id: number) => productsApi.remove(id),
    onSuccess: () => {
      setActionError(null);
    },
    onError: (caught) => {
      setActionError(caught instanceof ApiError ? caught.message : 'Could not delete.');
    },
    onSettled: invalidate,
  });

  if (products.isPending || moulds.isPending || types.isPending) {
    return <p className="p-6 text-ink-muted">Loading…</p>;
  }

  if (products.isError || moulds.isError || types.isError) {
    return <p className="p-6 text-bad">Could not load products.</p>;
  }

  return (
    <div>
      <div className="mb-4 flex items-center justify-between gap-3">
        <p className="text-sm text-ink-muted">
          {products.data.length} product{products.data.length === 1 ? '' : 's'}
        </p>
        <button
          type="button"
          className="btn-primary h-touch w-auto px-5 text-base"
          onClick={() => {
            setEditing('new');
          }}
        >
          Add product
        </button>
      </div>

      <p className="mb-4 rounded-control border border-line bg-canvas px-4 py-3 text-sm text-ink-soft">
        A mould plus an absorbency names exactly one product.
      </p>

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
              <th className="px-4 py-3 font-semibold">{t('term.product')}</th>
              <th className="px-4 py-3 font-semibold">{t('term.mould')}</th>
              <th className="px-4 py-3 font-semibold">Type</th>
              <th className="px-4 py-3 font-semibold">{t('term.absorbent')}</th>
              <th className="px-4 py-3 font-semibold">Pieces / bag</th>
              <th className="px-4 py-3 font-semibold">Small bags / bag</th>
              <th className="px-4 py-3 font-semibold">Bags / pallet</th>
              <th className="px-4 py-3 font-semibold">{t('field.status')}</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody>
            {products.data.map((product) => (
              <tr key={product.id} className="border-b border-line last:border-0">
                <td className="px-4 py-3 font-medium text-ink">{product.name}</td>
                <td className="px-4 py-3 text-ink-soft">{product.mouldName}</td>
                <td className="px-4 py-3 text-ink-soft">{product.productTypeName}</td>
                <td className="px-4 py-3 text-ink-soft">
                  {product.isAbsorbent ? 'Yes' : 'No'}
                </td>
                <td className="px-4 py-3 text-ink-soft">{product.piecesPerBag}</td>
                <td className="px-4 py-3 text-ink-soft">{product.smallBagsPerBag}</td>
                <td className="px-4 py-3 text-ink-soft">{product.bagsPerPallet}</td>
                <td className="px-4 py-3">
                  <StatusBadge isActive={product.isActive} />
                </td>
                <td className="px-4 py-3">
                  <div className="flex justify-end gap-2">
                    <RowButton
                      label={t('action.edit')}
                      onClick={() => {
                        setEditing(product);
                      }}
                    />
                    <RowButton
                      label={product.isActive ? 'Deactivate' : 'Activate'}
                      onClick={() => {
                        setActive.mutate({
                          id: product.id,
                          isActive: !product.isActive,
                        });
                      }}
                    />
                    {product.canDelete && (
                      <RowButton
                        label={t('action.delete')}
                        tone="danger"
                        onClick={() => {
                          setConfirm({
                            title: 'Delete product?',
                            message: (
                              <>
                                <strong>{product.name}</strong> will be removed for good.
                                Nothing uses it, so no records are affected.
                              </>
                            ),
                            confirmLabel: 'Delete',
                            onConfirm: () => {
                              remove.mutate(product.id);
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
        <ProductDialog
          product={editing === 'new' ? null : editing}
          moulds={moulds.data}
          types={types.data}
          onClose={() => {
            setEditing(null);
          }}
          onSaved={invalidate}
        />
      )}
    </div>
  );
}

interface Named {
  id: number;
  name: string;
}

function ProductDialog({
  product,
  moulds,
  types,
  onClose,
  onSaved,
}: {
  product: ProductDto | null;
  moulds: Named[];
  types: Named[];
  onClose: () => void;
  onSaved: () => void;
}): ReactElement {
  const { t } = useTranslation();
  const [name, setName] = useState(product?.name ?? '');
  const [mouldId, setMouldId] = useState(product?.mouldId ?? moulds[0]?.id ?? 0);
  const [productTypeId, setProductTypeId] = useState(
    product?.productTypeId ?? types[0]?.id ?? 0,
  );
  const [isAbsorbent, setIsAbsorbent] = useState(product?.isAbsorbent ?? false);
  const [piecesPerBag, setPiecesPerBag] = useState(String(product?.piecesPerBag ?? 250));
  const [smallBagsPerBag, setSmallBagsPerBag] = useState(
    String(product?.smallBagsPerBag ?? 1),
  );
  const [bagsPerPallet, setBagsPerPallet] = useState(
    String(product?.bagsPerPallet ?? 21),
  );
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  async function save(): Promise<void> {
    setError(null);
    setIsSaving(true);
    try {
      const body: SaveProduct = {
        name,
        mouldId,
        productTypeId,
        isAbsorbent,
        piecesPerBag: Number(piecesPerBag),
        smallBagsPerBag: Number(smallBagsPerBag),
        bagsPerPallet: Number(bagsPerPallet),
      };

      if (product === null) {
        await productsApi.create(body);
      } else {
        await productsApi.update(product.id, body);
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
    <Modal title={product === null ? 'Add product' : 'Edit product'} onClose={onClose}>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void save();
        }}
        noValidate
      >
        <div className="mb-4">
          <label className="field-label" htmlFor="prod-name">
            {t('field.name')}
          </label>
          <input
            id="prod-name"
            className="field-input"
            value={name}
            maxLength={100}
            disabled={isSaving}
            onChange={(event) => {
              setName(event.target.value);
            }}
          />
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <div className="mb-4">
            <label className="field-label" htmlFor="prod-mould">
              {t('term.mould')}
            </label>
            <select
              id="prod-mould"
              className="field-input"
              value={mouldId}
              disabled={isSaving}
              onChange={(event) => {
                setMouldId(Number(event.target.value));
              }}
            >
              {moulds.map((mould) => (
                <option key={mould.id} value={mould.id}>
                  {mould.name}
                </option>
              ))}
            </select>
          </div>

          <div className="mb-4">
            <label className="field-label" htmlFor="prod-type">
              Product type
            </label>
            <select
              id="prod-type"
              className="field-input"
              value={productTypeId}
              disabled={isSaving}
              onChange={(event) => {
                setProductTypeId(Number(event.target.value));
              }}
            >
              {types.map((type) => (
                <option key={type.id} value={type.id}>
                  {type.name}
                </option>
              ))}
            </select>
          </div>
        </div>

        <div className="mb-4">
          <label
            className="flex items-center gap-3 text-sm font-medium text-ink"
            htmlFor="prod-abs"
          >
            <input
              id="prod-abs"
              type="checkbox"
              className="size-5"
              checked={isAbsorbent}
              disabled={isSaving}
              onChange={(event) => {
                setIsAbsorbent(event.target.checked);
              }}
            />
            {t('term.absorbent')}
          </label>
          <p className="mt-1 ms-8 text-xs text-ink-muted">
            Decided by what is mixed into the roll, not by the mould.
          </p>
        </div>

        <div className="grid gap-4 sm:grid-cols-3">
          <NumberField
            id="prod-pieces"
            label="Pieces per bag"
            value={piecesPerBag}
            onChange={setPiecesPerBag}
            disabled={isSaving}
          />
          <NumberField
            id="prod-small"
            label="Small bags per bag"
            value={smallBagsPerBag}
            onChange={setSmallBagsPerBag}
            disabled={isSaving}
          />
          <NumberField
            id="prod-pallet"
            label="Bags per pallet"
            value={bagsPerPallet}
            onChange={setBagsPerPallet}
            disabled={isSaving}
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

        <button type="submit" className="btn-primary" disabled={isSaving}>
          {isSaving ? 'Saving…' : 'Save'}
        </button>
      </form>
    </Modal>
  );
}

function NumberField({
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
    <div className="mb-4">
      <label className="field-label" htmlFor={id}>
        {label}
      </label>
      <input
        id={id}
        type="number"
        min="1"
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
