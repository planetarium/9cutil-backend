using System.Text.RegularExpressions;
using Bencodex.Types;
using Libplanet.Crypto;
using Mimir.Worker.Models;
using Mimir.Worker.Services;
using Nekoyume.Battle;
using Nekoyume.Helper;
using Nekoyume.Model.Item;
using Nekoyume.Model.Market;
using Nekoyume.TableData;
using Nekoyume.TableData.Crystal;
using Serilog;

namespace Mimir.Worker.Handler;

public class ProductsHandler(IStateService stateService, MongoDbService store) :
    BaseActionHandler(
        stateService,
        store,
        "^register_product[0-9]*$|^cancel_product_registration[0-9]*$|^buy_product[0-9]*$|^re_register_product[0-9]*$",
        Log.ForContext<ProductsHandler>())
{
    protected override async Task HandleAction(
        string actionType,
        long processBlockIndex,
        Dictionary actionValues
    )
    {
        var avatarAddresses = GetAvatarAddresses(actionType, actionValues);

        foreach (var avatarAddress in avatarAddresses)
        {
            Logger.Information(
                "Handle products for avatar, avatar: {AvatarAddress} ",
                avatarAddress
            );

            var productsStateAddress = ProductsState.DeriveAddress(avatarAddress);
            var productsState = await StateGetter.GetProductsState(avatarAddress);
            await SyncWrappedProductsStateAsync(avatarAddress, productsStateAddress, productsState);
        }
    }

    private List<Address> GetAvatarAddresses(string actionType, Dictionary actionValues)
    {
        var avatarAddresses = new List<Address>();

        if (Regex.IsMatch(actionType, "^buy_product[0-9]*$"))
        {
            var serialized = (List)actionValues["p"];
            var productInfos = serialized
                .Cast<List>()
                .Select(ProductFactory.DeserializeProductInfo)
                .ToList();

            foreach (var productInfo in productInfos)
            {
                avatarAddresses.Add(productInfo.AvatarAddress);
            }
        }
        else
        {
            avatarAddresses.Add(new Address(actionValues["a"]));
        }

        return avatarAddresses;
    }

    public async Task SyncWrappedProductsStateAsync(
        Address avatarAddress,
        Address productsStateAddress,
        ProductsState productsState
    )
    {
        var productIds = productsState.ProductIds.Select(id => Guid.Parse(id.ToString())).ToList();

        var existingProductIds = await GetExistingProductIds(productsStateAddress);

        var productsToAdd = productIds.Except(existingProductIds).ToList();
        var productsToRemove = existingProductIds.Except(productIds).ToList();

        await AddNewProductsAsync(avatarAddress, productsStateAddress, productsToAdd);
        await RemoveOldProductsAsync(productsToRemove);

        await Store.UpsertStateDataAsync(
            new StateData(
                productsStateAddress,
                new WrappedProductsState(productsStateAddress, avatarAddress, productsState)
            )
        );
    }

    private async Task<List<Guid>> GetExistingProductIds(Address productsStateAddress)
    {
        var existingState = await Store.GetProductsStateByAddress(productsStateAddress);
        return existingState == null
            ? new List<Guid>()
            : existingState["State"]
                ["Object"]["ProductIds"]
                .AsBsonArray.Select(p => Guid.Parse(p.AsString))
                .ToList();
    }

    private async Task AddNewProductsAsync(
        Address avatarAddress,
        Address productsStateAddress,
        List<Guid> productsToAdd
    )
    {
        foreach (var productId in productsToAdd)
        {
            var product = await StateGetter.GetProductState(productId);
            var stateData = await CreateStateDataAsync(
                avatarAddress,
                productsStateAddress,
                product
            );
            await Store.UpsertStateDataAsync(stateData);
        }
    }

    private async Task<StateData> CreateStateDataAsync(
        Address avatarAddress,
        Address productsStateAddress,
        Product product
    )
    {
        var productAddress = Product.DeriveAddress(product.ProductId);

        if (product is ItemProduct itemProduct)
        {
            var unitPrice = CalculateUnitPrice(itemProduct);
            var combatPoint = await CalculateCombatPointAsync(itemProduct);
            var (crystal, crystalPerPrice) = await CalculateCrystalMetricsAsync(itemProduct);

            return new StateData(
                productAddress,
                new ProductState(
                    productAddress,
                    avatarAddress,
                    productsStateAddress,
                    product,
                    unitPrice,
                    combatPoint,
                    crystal,
                    crystalPerPrice
                )
            );
        }
        else
        {
            return new StateData(
                productAddress,
                new ProductState(productAddress, avatarAddress, productsStateAddress, product)
            );
        }
    }

    private decimal CalculateUnitPrice(ItemProduct itemProduct)
    {
        return decimal.Parse(itemProduct.Price.GetQuantityString()) / itemProduct.ItemCount;
    }

    private async Task<int?> CalculateCombatPointAsync(ItemProduct itemProduct)
    {
        try
        {
            var costumeStatSheet = await Store.GetSheetAsync<CostumeStatSheet>();

            if (costumeStatSheet != null)
            {
                int? cp = itemProduct.TradableItem switch
                {
                    ItemUsable itemUsable => CPHelper.GetCP(itemUsable),
                    Costume costume => CPHelper.GetCP(costume, costumeStatSheet),
                    _ => null
                };
                return cp;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(
                $"Error calculating combat point for itemProduct {itemProduct.ProductId}: {ex.Message}"
            );
        }

        return null;
    }

    private async Task<(int? crystal, int? crystalPerPrice)> CalculateCrystalMetricsAsync(
        ItemProduct itemProduct
    )
    {
        try
        {
            var crystalEquipmentGrindingSheet =
                await Store.GetSheetAsync<CrystalEquipmentGrindingSheet>();
            var crystalMonsterCollectionMultiplierSheet =
                await Store.GetSheetAsync<CrystalMonsterCollectionMultiplierSheet>();

            if (
                crystalEquipmentGrindingSheet != null
                && crystalMonsterCollectionMultiplierSheet != null
                && itemProduct.TradableItem is Equipment equipment
            )
            {
                var rawCrystal = CrystalCalculator.CalculateCrystal(
                    [equipment],
                    false,
                    crystalEquipmentGrindingSheet,
                    crystalMonsterCollectionMultiplierSheet,
                    0
                );

                int crystal = (int)rawCrystal.MajorUnit;
                int crystalPerPrice = (int)
                    rawCrystal.DivRem(itemProduct.Price.MajorUnit).Quotient.MajorUnit;

                return (crystal, crystalPerPrice);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(
                $"Error calculating crystal metrics for itemProduct {itemProduct.ProductId}: {ex.Message}"
            );
        }

        return (null, null);
    }

    private async Task RemoveOldProductsAsync(List<Guid> productsToRemove)
    {
        foreach (var productId in productsToRemove)
        {
            await Store.RemoveProduct(productId);
        }
    }
}
