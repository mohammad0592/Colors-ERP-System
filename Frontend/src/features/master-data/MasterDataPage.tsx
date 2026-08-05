import { useState, type ReactElement } from 'react';
import { PageHeader } from '../../components/ui/PageHeader';
import {
  colorsApi,
  materialCategoriesApi,
  mouldsApi,
  productionLinesApi,
  productTypesApi,
  shiftsApi,
  unitsApi,
} from './api';
import { LookupTab } from './LookupTab';
import { MaterialsTab } from './MaterialsTab';
import { ProductsTab } from './ProductsTab';

type TabId =
  | 'materials'
  | 'units'
  | 'categories'
  | 'colors'
  | 'products'
  | 'moulds'
  | 'productTypes'
  | 'lines'
  | 'shifts';

const tabs: { id: TabId; label: string }[] = [
  { id: 'materials', label: 'Materials' },
  { id: 'units', label: 'Units' },
  { id: 'categories', label: 'Categories' },
  { id: 'colors', label: 'Colours' },
  { id: 'products', label: 'Products' },
  { id: 'moulds', label: 'Moulds' },
  { id: 'productTypes', label: 'Product types' },
  { id: 'lines', label: 'Lines' },
  { id: 'shifts', label: 'Shifts' },
];

/**
 * Administration of the reference data every production screen depends on
 * (specification section 4). Delete works only while nothing references a row —
 * for typos and tests; anything already used can only be deactivated, so
 * historical records keep resolving.
 */
export function MasterDataPage(): ReactElement {
  const [tab, setTab] = useState<TabId>('materials');

  return (
    <>
      <PageHeader
        title="Master Data"
        subtitle="Materials and their pack sizes, units, colours, products and their moulds, lines and shifts"
      />

      <div className="mb-5 flex flex-wrap gap-2">
        {tabs.map((entry) => (
          <button
            key={entry.id}
            type="button"
            onClick={() => {
              setTab(entry.id);
            }}
            className={[
              'min-h-touch rounded-control px-4 text-sm font-semibold transition-colors',
              tab === entry.id
                ? 'bg-brand-600 text-white'
                : 'bg-surface text-ink-soft border border-line hover:border-brand-200 hover:text-brand-700',
            ].join(' ')}
          >
            {entry.label}
          </button>
        ))}
      </div>

      {tab === 'materials' && <MaterialsTab />}

      {tab === 'units' && (
        <LookupTab
          queryKey="units"
          client={unitsApi}
          itemWord="unit"
          fields={[
            { key: 'name', label: 'Name' },
            {
              key: 'symbol',
              label: 'Symbol',
              maxLength: 10,
              hint: 'Shown after every quantity — kg, pcs.',
            },
          ]}
        />
      )}

      {tab === 'categories' && (
        <LookupTab
          queryKey="material-categories"
          client={materialCategoriesApi}
          itemWord="category"
          itemWordPlural="categories"
          fields={[{ key: 'name', label: 'Name' }]}
        />
      )}

      {tab === 'colors' && (
        <LookupTab
          queryKey="colors"
          client={colorsApi}
          itemWord="colour"
          fields={[
            { key: 'name', label: 'Name' },
            {
              key: 'code',
              label: 'Code letter',
              maxLength: 1,
              hint: 'One letter A–Z, unique. It appears inside every roll code — the W in 01WN180726A.',
            },
          ]}
        />
      )}

      {tab === 'products' && <ProductsTab />}

      {tab === 'moulds' && (
        <LookupTab
          queryKey="moulds"
          client={mouldsApi}
          itemWord="mould"
          fields={[{ key: 'name', label: 'Name' }]}
        />
      )}

      {tab === 'productTypes' && (
        <LookupTab
          queryKey="product-types"
          client={productTypesApi}
          itemWord="product type"
          fields={[{ key: 'name', label: 'Name' }]}
        />
      )}

      {tab === 'lines' && (
        <LookupTab
          queryKey="production-lines"
          client={productionLinesApi}
          itemWord="line"
          fields={[
            { key: 'name', label: 'Name' },
            {
              key: 'recordsMachineSettings',
              label: 'Records machine settings',
              type: 'checkbox',
              hint: 'Only the thermo line. It is then asked for forming speed, feed distance and cycle time.',
            },
          ]}
        />
      )}

      {tab === 'shifts' && (
        <LookupTab
          queryKey="shifts"
          client={shiftsApi}
          itemWord="shift"
          fields={[
            { key: 'name', label: 'Name' },
            { key: 'startTime', label: 'Starts', type: 'time' },
            {
              key: 'endTime',
              label: 'Ends',
              type: 'time',
              hint: '00:00 means midnight at the end of the day, as shift B uses it.',
            },
          ]}
        />
      )}
    </>
  );
}
