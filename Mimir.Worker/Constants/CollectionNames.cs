using Mimir.Worker.Models;
using Nekoyume.Model.State;
using AgentState = Mimir.Worker.Models.AgentState;
using ProductsState = Mimir.Worker.Models.ProductsState;
using AllRuneState = Mimir.Worker.Models.AllRuneState;
using CollectionState = Mimir.Worker.Models.CollectionState;
using ItemSlotState = Mimir.Worker.Models.ItemSlotState;
using RuneSlotState = Mimir.Worker.Models.RuneSlotState;
using WorldBossState = Mimir.Worker.Models.WorldBossState;
using RaiderState = Mimir.Worker.Models.RaiderState;
using StakeState = Mimir.Worker.Models.StakeState;
using CombinationSlotState = Mimir.Worker.Models.CombinationSlotState;

namespace Mimir.Worker.Constants
{
    public static class CollectionNames
    {
        public static readonly Dictionary<Type, string> CollectionMappings = new();

        static CollectionNames()
        {
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
            CollectionMappings.Add(typeof(ProductsState), "products");
            CollectionMappings.Add(typeof(ProductState), "product");
            CollectionMappings.Add(typeof(ItemSlotState), "item_slot");
            CollectionMappings.Add(typeof(RuneSlotState), "rune_slot");
            CollectionMappings.Add(typeof(WorldBossState), "world_boss");
            CollectionMappings.Add(typeof(WorldBossKillRewardRecordState), "world_boss_kill_reward_record");
            CollectionMappings.Add(typeof(RaiderState), "raider");
            CollectionMappings.Add(typeof(StakeState), "stake");
            CollectionMappings.Add(typeof(CombinationSlotState), "combination_slot");
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
}
