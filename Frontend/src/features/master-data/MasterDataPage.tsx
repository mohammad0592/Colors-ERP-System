import { useState, type ReactElement } from 'react';
import { useTranslation } from '../../hooks/useTranslation';
import type { TranslationKey } from '../../lib/i18n/en';
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

const tabs: { id: TabId; label: TranslationKey }[] = [
  { id: 'materials', label: 'term.materials' },
  { id: 'units', label: 'md.units' },
  { id: 'categories', label: 'md.categories' },
  { id: 'colors', label: 'md.colours' },
  { id: 'products', label: 'md.products' },
  { id: 'moulds', label: 'md.moulds' },
  { id: 'productTypes', label: 'md.productTypes' },
  { id: 'lines', label: 'md.lines' },
  { id: 'shifts', label: 'md.shiftsTab' },
];

/**
 * Administration of the reference data every production screen depends on
 * (specification section 4). Delete works only while nothing references a row —
 * for typos and tests; anything already used can only be deactivated, so
 * historical records keep resolving.
 */
export function MasterDataPage(): ReactElement {
  const { t } = useTranslation();
  const [tab, setTab] = useState<TabId>('materials');

  return (
    <>
      {/* No subtitle: the tabs immediately below name every one of these, and say it
          without going out of date when a tab is added. */}
      <PageHeader title={t('page.masterData.title')} />

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
            {t(entry.label)}
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
            { key: 'name', label: 'field.name' },
            {
              key: 'symbol',
              label: 'md.symbol',
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
          fields={[
            { key: 'name', label: 'field.name' },
            {
              key: 'issuedOnTickets',
              label: 'md.goesOnTicket',
              type: 'checkbox',
              hint: 'Raw material only. Packaging goes straight to the bench and is counted at the end of the shift from what was produced — putting it on a ticket would count it twice.',
            },
          ]}
        />
      )}

      {tab === 'colors' && (
        <LookupTab
          queryKey="colors"
          client={colorsApi}
          itemWord="colour"
          fields={[
            { key: 'name', label: 'field.name' },
            {
              key: 'code',
              label: 'md.codeLetter',
              maxLength: 1,
              hint: 'One letter A–Z, unique. It appears inside every roll code — the W in 01WN180726A.',
            },
            // Not read off the name or the letter B, which Blue starts with too.
            {
              key: 'isBlack',
              label: 'md.thisIsBlack',
              type: 'checkbox',
              hint: t('md.blackNote'),
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
          fields={[{ key: 'name', label: 'field.name' }]}
        />
      )}

      {tab === 'productTypes' && (
        <LookupTab
          queryKey="product-types"
          client={productTypesApi}
          itemWord="product type"
          fields={[{ key: 'name', label: 'field.name' }]}
        />
      )}

      {tab === 'lines' && (
        <LookupTab
          queryKey="production-lines"
          client={productionLinesApi}
          itemWord="line"
          fields={[
            { key: 'name', label: 'field.name' },
            {
              key: 'recordsMachineSettings',
              label: 'md.recordsSettings',
              type: 'checkbox',
              hint: t('md.settingsNote'),
            },
            // What the line does. Every screen filters on these, so a wrong tick shows
            // up as a missing line in a list rather than as a bad record.
            {
              key: 'makesRolls',
              label: 'md.mixesRolls',
              type: 'checkbox',
              hint: t('md.mixesNote'),
            },
            {
              key: 'formsBags',
              label: 'md.formsBags',
              type: 'checkbox',
              hint: t('md.formsNote'),
            },
            {
              key: 'takesRawMaterial',
              label: 'md.takesRaw',
              type: 'checkbox',
              hint: t('md.takesRawNote'),
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
            { key: 'name', label: 'field.name' },
            { key: 'startTime', label: 'md.starts', type: 'time' },
            {
              key: 'endTime',
              label: 'md.ends',
              type: 'time',
              hint: '00:00 means midnight at the end of the day, as shift B uses it.',
            },
          ]}
        />
      )}
    </>
  );
}
