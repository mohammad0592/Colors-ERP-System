using Colors.Application.Features.MasterData;
using Microsoft.AspNetCore.Mvc;

namespace Colors.Api.Controllers;

/// <summary>
/// The master data lists. Each controller is only a route plus a service —
/// the endpoints themselves live in <see cref="MasterDataControllerBase{TDto,TUpsert}"/>.
/// </summary>
[Route("api/production-lines")]
public class ProductionLinesController(IProductionLineService service)
    : MasterDataControllerBase<ProductionLineDto, SaveProductionLineRequest>(service);

[Route("api/shifts")]
public class ShiftsController(IShiftService service)
    : MasterDataControllerBase<ShiftDto, SaveShiftRequest>(service);

[Route("api/units")]
public class UnitsController(IUnitService service)
    : MasterDataControllerBase<UnitDto, SaveUnitRequest>(service);

[Route("api/material-categories")]
public class MaterialCategoriesController(IMaterialCategoryService service)
    : MasterDataControllerBase<MaterialCategoryDto, SaveMaterialCategoryRequest>(service);

[Route("api/colors")]
public class ColorsController(IColorService service)
    : MasterDataControllerBase<ColorDto, SaveColorRequest>(service);

[Route("api/moulds")]
public class MouldsController(IMouldService service)
    : MasterDataControllerBase<LookupDto, SaveLookupRequest>(service);

[Route("api/products")]
public class ProductsController(IProductService service)
    : MasterDataControllerBase<ProductDto, SaveProductRequest>(service);

[Route("api/product-types")]
public class ProductTypesController(IProductTypeService service)
    : MasterDataControllerBase<LookupDto, SaveLookupRequest>(service);

[Route("api/materials")]
public class MaterialsController(IMaterialService service)
    : MasterDataControllerBase<MaterialDto, SaveMaterialRequest>(service);
