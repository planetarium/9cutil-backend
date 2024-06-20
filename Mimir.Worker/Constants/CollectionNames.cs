using Lib9c;
using Libplanet.Types.Assets;
using Mimir.Worker.Models;
using Nekoyume.Model.State;
using AgentState = Mimir.Worker.Models.AgentState;
using AllRuneState = Mimir.Worker.Models.AllRuneState;
using CollectionState = Mimir.Worker.Models.CollectionState;

namespace Mimir.Worker.Constants;

public static class CollectionNames
{
    public static readonly Dictionary<Type, string> CollectionMappings = new();

    static CollectionNames()
    {
        CollectionMappings.Add(typeof(BalanceState), "balance");
        CollectionMappings.Add(typeof(AgentState), "agent");
        CollectionMappings.Add(typeof(AvatarState), "avatar");
        CollectionMappings.Add(typeof(InventoryState), "inventory");
        CollectionMappings.Add(typeof(QuestListState), "quest_list");
        CollectionMappings.Add(typeof(WorldInformationState), "world_information");
        CollectionMappings.Add(typeof(ActionPointState), "action_point");
        CollectionMappings.Add(typeof(SheetState), "table_sheet");
        CollectionMappings.Add(typeof(ArenaState), "arena");
        CollectionMappings.Add(typeof(AllRuneState), "all_rune");
        CollectionMappings.Add(typeof(CollectionState), "collection");
        CollectionMappings.Add(typeof(DailyRewardState), "daily_reward");
        CollectionMappings.Add(typeof(WrappedProductsState), "products");
        CollectionMappings.Add(typeof(ProductState), "product");
    }

    public static string GetCollectionName<T>()
    {
        if (!CollectionMappings.TryGetValue(typeof(T), out var collectionName))
        {
            throw new InvalidOperationException(
                $"No collection mapping found for state type: {typeof(T).Name}"
            );
        }

        return collectionName;
    }

    public static string GetCollectionName(Type type)
    {
        if (!CollectionMappings.TryGetValue(type, out var collectionName))
        {
            throw new InvalidOperationException(
                $"No collection mapping found for state type: {type.Name}"
            );
        }

        return collectionName;
    }
}
