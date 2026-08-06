import { apiRequest } from '../../lib/apiClient';

/**
 * The recycler, mirroring Colors.Application.Features.Recycler.
 * Specification section 11.
 */

export interface RecyclerProductionDto {
  id: number;
  shiftLineId: number;
  productionLineName: string;
  shiftName: string;
  productionDate: string;
  /**
   * The whole record. What went into the grinder cannot be weighed — scrap lives in two
   * silos and is drawn out to be ground (specification section 11).
   */
  recycledMaterialWeight: number;
  recordedByName: string;
  recordedAt: string;
  notes: string | null;
}

export interface RecyclerDraftDto {
  shiftLineId: number;
  productionLineName: string;
  shiftName: string;
  productionDate: string;
  /** The material the output is added to. */
  recycledMaterialName: string | null;
  alreadyRecorded: boolean;
  recorded: RecyclerProductionDto | null;
}

export const recyclerApi = {
  list: (shiftReportId?: number): Promise<RecyclerProductionDto[]> => {
    const query = shiftReportId === undefined ? '' : `?shiftReportId=${String(shiftReportId)}`;
    return apiRequest<RecyclerProductionDto[]>(`/api/recycler${query}`);
  },

  /** The form: one box, and the name of the pile the output goes into. */
  draft: (shiftLineId: number): Promise<RecyclerDraftDto> =>
    apiRequest<RecyclerDraftDto>(`/api/recycler/draft/${String(shiftLineId)}`),

  save: (body: {
    shiftLineId: number;
    recycledMaterialWeight: number;
    notes: string | null;
  }): Promise<RecyclerProductionDto> =>
    apiRequest<RecyclerProductionDto>('/api/recycler', { method: 'POST', body }),
};
