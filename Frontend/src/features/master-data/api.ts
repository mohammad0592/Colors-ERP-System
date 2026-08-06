import { apiRequest } from '../../lib/apiClient';

/**
 * Types and calls for the master data lists, mirroring the C# records in
 * Colors.Application.Features.MasterData. A field renamed on the server becomes a
 * build error here, not an undefined on a tablet.
 */

export interface LookupDto {
  id: number;
  name: string;
  isActive: boolean;
  /**
   * False as soon as anything references the row. The screen hides the delete
   * button rather than offering one that always fails — only the server knows
   * what points at a row.
   */
  canDelete: boolean;
}

export interface ProductionLineDto extends LookupDto {
  /**
   * True only for the thermo line. It decides whether the shift report asks for
   * forming speed, feed distance and cycle time — the extruder has no such settings.
   */
  recordsMachineSettings: boolean;
  /**
   * What the line does. Every screen filters on these: where a batch may start, where a
   * roll may be formed, and which line appears on an issue ticket.
   */
  makesRolls: boolean;
  formsBags: boolean;
  takesRawMaterial: boolean;
}

/**
 * Something the factory makes. The thermo finds a product by mould and absorbency —
 * that pair is unique — so nobody ever picks one on screen.
 */
export interface ProductDto extends LookupDto {
  mouldId: number;
  mouldName: string;
  productTypeId: number;
  productTypeName: string;
  isAbsorbent: boolean;
  /** 500 for a plate, 250 for a meal box or clamshell. */
  piecesPerBag: number;
  /** Two for a plate — its big bag holds two small ones inside. One for a box. */
  smallBagsPerBag: number;
  bagsPerPallet: number;
}

export interface SaveProduct {
  name: string;
  mouldId: number;
  productTypeId: number;
  isAbsorbent: boolean;
  piecesPerBag: number;
  smallBagsPerBag: number;
  bagsPerPallet: number;
}

export interface MaterialCategoryDto extends LookupDto {
  /**
   * Whether its materials go out on an issue ticket. True for raw material only —
   * packaging is counted at the end of the shift, from what was produced.
   */
  issuedOnTickets: boolean;
}

export interface UnitDto extends LookupDto {
  symbol: string;
}

export interface ColorDto extends LookupDto {
  /** One capital letter used inside every roll code: W, G, Y, B. */
  code: string;
  /**
   * Decides which recipes may use this colour: a black-only recipe needs it, and every
   * other recipe refuses it. A flag, not a check on the name or the letter B — Blue
   * starts with B too.
   */
  isBlack: boolean;
}

export interface ShiftDto extends LookupDto {
  /** "HH:mm" */
  startTime: string;
  endTime: string;
}

export interface MaterialPackagingDto {
  id: number;
  unitId: number;
  unitName: string;
  quantityInBaseUnit: number;
  isDefaultReceiving: boolean;
}

export interface MaterialDto {
  id: number;
  code: string;
  name: string;
  categoryId: number;
  categoryName: string;
  baseUnitId: number;
  baseUnitName: string;
  baseUnitSymbol: string;
  minQuantity: number;
  unitWeight: number | null;
  notes: string | null;
  isActive: boolean;
  canDelete: boolean;
  packagings: MaterialPackagingDto[];
}

export interface SaveMaterialPackaging {
  unitId: number;
  quantityInBaseUnit: number;
  isDefaultReceiving: boolean;
}

export interface SaveMaterial {
  code: string;
  name: string;
  categoryId: number;
  baseUnitId: number;
  minQuantity: number;
  unitWeight: number | null;
  notes: string | null;
  packagings: SaveMaterialPackaging[];
}

/** The four endpoints every list shares. TSave is the body POST and PUT expect. */
export interface CrudClient<TDto, TSave> {
  list: (includeInactive?: boolean) => Promise<TDto[]>;
  create: (body: TSave) => Promise<TDto>;
  update: (id: number, body: TSave) => Promise<TDto>;
  setActive: (id: number, isActive: boolean) => Promise<TDto>;
  /** Allowed only while nothing references the row (specification section 4). */
  remove: (id: number) => Promise<undefined>;
}

function crudFor<TDto, TSave>(base: string): CrudClient<TDto, TSave> {
  return {
    list: (includeInactive = false) =>
      apiRequest<TDto[]>(`${base}?includeInactive=${String(includeInactive)}`),
    create: (body) => apiRequest<TDto>(base, { method: 'POST', body }),
    update: (id, body) =>
      apiRequest<TDto>(`${base}/${String(id)}`, { method: 'PUT', body }),
    setActive: (id, isActive) =>
      apiRequest<TDto>(`${base}/${String(id)}/active`, {
        method: 'PUT',
        body: { isActive },
      }),
    remove: (id) => apiRequest<undefined>(`${base}/${String(id)}`, { method: 'DELETE' }),
  };
}

export const productionLinesApi = crudFor<
  ProductionLineDto,
  {
    name: string;
    recordsMachineSettings: boolean;
    makesRolls: boolean;
    formsBags: boolean;
    takesRawMaterial: boolean;
  }
>('/api/production-lines');
export const shiftsApi = crudFor<
  ShiftDto,
  { name: string; startTime: string; endTime: string }
>('/api/shifts');
export const unitsApi = crudFor<UnitDto, { name: string; symbol: string }>('/api/units');
export const materialCategoriesApi = crudFor<
  MaterialCategoryDto,
  { name: string; issuedOnTickets: boolean }
>('/api/material-categories');
export const colorsApi = crudFor<
  ColorDto,
  { name: string; code: string; isBlack: boolean }
>('/api/colors');
export const mouldsApi = crudFor<LookupDto, { name: string }>('/api/moulds');
export const productsApi = crudFor<ProductDto, SaveProduct>('/api/products');
export const productTypesApi = crudFor<LookupDto, { name: string }>('/api/product-types');
export const materialsApi = crudFor<MaterialDto, SaveMaterial>('/api/materials');
