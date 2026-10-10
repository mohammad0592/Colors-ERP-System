using Colors.Application.Features.MasterData;
using Colors.Application.Features.Recipes;
using Colors.Infrastructure.Persistence;
using Colors.Infrastructure.Services.MasterData;
using Colors.Infrastructure.Services.Recipes;
using Colors.IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;

namespace Colors.IntegrationTests.Features;

/// <summary>
/// The main recipes and the products made from them (specification section 5).
/// </summary>
[Collection(DatabaseCollection.Name)]
public class RecipeTests(DatabaseFixture fixture)
{
    private static RecipeService NewService(ColorsDbContext db) => new(db, TimeProvider.System);

    // Every test family is active and they all share one database, so each code is made
    // from the test's own suffix — two tests both asking for "N" would refuse each other.
    private static SaveRecipeFamilyRequest Family(string suffix, bool absorbent = false) =>
        new($"Main {suffix}", $"T{suffix}", absorbent, null);

    [Fact]
    public async Task A_main_recipe_is_written_from_nothing_and_put_into_production()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "RCP1");
        var service = NewService(db);

        var family = await service.CreateFamilyAsync(Family("RCP1"));
        Assert.True(family.IsSuccess, family.Message);

        // No older recipe is copied: the formula is typed in, one number per material.
        var draft = await service.CreateVersionAsync(
            new CreateRecipeVersionRequest(
                family.Value!.Id,
                null,
                [
                    new SaveRecipeIngredientRequest(ids.GppsId, true, 100m),
                    new SaveRecipeIngredientRequest(ids.TalcId, false, 1m),
                ]),
            ids.UserId);
        Assert.True(draft.IsSuccess, draft.Message);
        Assert.Equal(1, draft.Value!.VersionNumber);

        var promoted = await service.PromoteVersionAsync(draft.Value.Id);
        Assert.True(promoted.IsSuccess, promoted.Message);

        var families = await service.GetFamiliesAsync();
        Assert.Equal(
            draft.Value.RecipeNumber,
            families.Single(f => f.Id == family.Value.Id).CurrentRecipeNumber);
    }

    [Fact]
    public async Task Two_main_recipes_in_use_cannot_share_a_code()
    {
        await using var db = fixture.CreateContext();
        await FactoryData.CreateAsync(db, "RCP2");
        var service = NewService(db);

        var first = await service.CreateFamilyAsync(Family("RCP2"));
        Assert.True(first.IsSuccess, first.Message);

        // With black an ordinary colour, the code is what tells two rolls' recipes apart.
        var clash = await service.CreateFamilyAsync(
            new SaveRecipeFamilyRequest("Main RCP2 again", "trcp2", false, null));
        Assert.False(clash.IsSuccess);

        // A retired family keeps its code for the rolls made with it, but no longer
        // stands in the way of a new one.
        await service.SetFamilyActiveAsync(first.Value!.Id, false);
        var reuse = await service.CreateFamilyAsync(
            new SaveRecipeFamilyRequest("Main RCP2 again", "TRCP2", false, null));
        Assert.True(reuse.IsSuccess, reuse.Message);
    }

    [Fact]
    public async Task Every_material_needs_a_percentage()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "RCP3");
        var service = NewService(db);
        var family = await service.CreateFamilyAsync(Family("RCP3"));

        var draft = await service.CreateVersionAsync(
            new CreateRecipeVersionRequest(
                family.Value!.Id,
                null,
                [
                    new SaveRecipeIngredientRequest(ids.GppsId, true, 100m),
                    new SaveRecipeIngredientRequest(ids.TalcId, false, 0m),
                ]),
            ids.UserId);

        Assert.False(draft.IsSuccess);
    }

    [Fact]
    public async Task A_retired_main_recipe_leaves_the_list_of_recipes()
    {
        await using var db = fixture.CreateContext();
        var ids = await FactoryData.CreateAsync(db, "RCP4");
        var service = NewService(db);
        var family = await service.CreateFamilyAsync(Family("RCP4"));
        await service.CreateVersionAsync(
            new CreateRecipeVersionRequest(
                family.Value!.Id,
                null,
                [new SaveRecipeIngredientRequest(ids.GppsId, true, 100m)]),
            ids.UserId);

        Assert.NotEmpty(await service.GetVersionsAsync(family.Value.Id));

        // The roll screen and the recipes page both read this list, so a retired recipe
        // is not offered for new rolls. Its versions stay in the database.
        await service.SetFamilyActiveAsync(family.Value.Id, false);

        Assert.Empty(await service.GetVersionsAsync(family.Value.Id));
        Assert.True(await db.RecipeVersions.AnyAsync(v => v.RecipeFamilyId == family.Value.Id));
    }

    [Fact]
    public async Task A_product_is_absorbent_when_its_main_recipe_is()
    {
        await using var db = fixture.CreateContext();
        await FactoryData.CreateAsync(db, "RCP5");
        var recipes = NewService(db);
        var absorbent = await recipes.CreateFamilyAsync(Family("RCP5", absorbent: true));

        var productType = await db.ProductTypes.FirstAsync();

        var products = new ProductService(db);
        var saved = await products.CreateAsync(
            new SaveProductRequest("Plate RCP5", productType.Id, absorbent.Value!.Id, 500, 2, 15, null, null));

        // Never typed in, so it cannot disagree with what the rolls are mixed from.
        Assert.True(saved.IsSuccess, saved.Message);
        Assert.True(saved.Value!.IsAbsorbent);
        Assert.Equal(absorbent.Value.Id, saved.Value.RecipeFamilyId);
    }

    [Fact]
    public async Task A_product_needs_a_main_recipe()
    {
        await using var db = fixture.CreateContext();
        await FactoryData.CreateAsync(db, "RCP6");
        var productType = await db.ProductTypes.FirstAsync();

        var saved = await new ProductService(db).CreateAsync(
            new SaveProductRequest("Plate RCP6", productType.Id, 0, 500, 2, 15, null, null));

        Assert.False(saved.IsSuccess);
    }
}
