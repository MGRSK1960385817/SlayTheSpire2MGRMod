using System.Reflection;
using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib;

namespace MGRMod.Telemetry;

internal sealed record MgrRunMetrics(
    JsonObject Payload,
    IReadOnlyDictionary<string, object?> IndexedProperties);

/// <summary>
/// Converts a complete save snapshot into an allow-listed balance payload.
/// Adding a field here is intentional; fields absent from this builder can never
/// leak through merely because the base game expands SerializableRun later.
/// </summary>
internal static class MgrRunMetricsBuilder
{
    internal const int SchemaVersion = 5;
    private const int MaximumEncounterDamage = 100;

    public static MgrRunMetrics Build(
        RunEndedEvent evt,
        MgrTelemetryIdentityInfo identity,
        string eventId)
    {
        SerializableRun run = evt.Run;
        SerializablePlayer player = run.Players.Single();
        int floorReached = run.MapPointHistory.Sum(act => act.Count);
        long durationSeconds = MgrTelemetryEligibility.GetDurationSeconds(evt);

        JsonObject payload = new()
        {
            ["schema_version"] = SchemaVersion,
            ["event_id"] = eventId,
            ["install_id"] = identity.InstallId,
            // Steam IDs exceed JavaScript's safe integer range. Keep the raw
            // decimal value as a string so PostHog cannot silently round it.
            ["steam_id"] = identity.SteamId,
            ["mod_version"] = GetModVersion(),
            ["game_version"] = ReleaseInfoManager.Instance.ReleaseInfo?.Version ?? "unknown",
            ["victory"] = evt.IsVictory,
            ["game_mode"] = run.GameMode.ToString(),
            ["ascension"] = run.Ascension,
            ["floor_reached"] = floorReached,
            ["duration_seconds"] = durationSeconds,
            ["reload_count"] = run.NumReloads,
            ["killed_by_encounter"] = evt.IsVictory ? null : FindLastEncounterId(run),
            ["acts"] = BuildActs(run, evt.IsVictory),
            ["final_player"] = BuildFinalPlayer(player),
            ["mgr_mechanics"] = MgrRunTelemetryAccumulator.BuildSnapshot(run.NumReloads),
            ["floors"] = BuildFloors(run, player.NetId)
        };

        Dictionary<string, object?> indexedProperties = new()
        {
            ["schema_version"] = SchemaVersion,
            ["event_id"] = eventId,
            ["install_id"] = identity.InstallId,
            ["steam_id"] = identity.SteamId,
            ["mod_version"] = GetModVersion(),
            ["victory"] = evt.IsVictory,
            ["ascension"] = run.Ascension,
            ["floor_reached"] = floorReached,
            ["duration_seconds"] = durationSeconds,
            ["reload_count"] = run.NumReloads
        };

        return new MgrRunMetrics(payload, indexedProperties);
    }

    private static JsonObject BuildFinalPlayer(SerializablePlayer player)
    {
        JsonArray deck = [];
        foreach (SerializableCard card in player.Deck)
            deck.Add(BuildCard(card));

        JsonArray relics = [];
        foreach (SerializableRelic relic in player.Relics)
        {
            relics.Add(new JsonObject
            {
                ["id"] = EntryOf(relic.Id),
                ["floor_added"] = relic.FloorAddedToDeck
            });
        }

        JsonArray potions = [];
        foreach (SerializablePotion potion in player.Potions)
        {
            potions.Add(new JsonObject
            {
                ["id"] = EntryOf(potion.Id),
                ["slot"] = potion.SlotIndex
            });
        }

        return new JsonObject
        {
            ["character_id"] = EntryOf(player.CharacterId),
            ["current_hp"] = player.CurrentHp,
            ["max_hp"] = player.MaxHp,
            ["max_energy"] = player.MaxEnergy,
            ["max_potion_slots"] = player.MaxPotionSlotCount,
            ["base_note_slots"] = player.BaseOrbSlotCount,
            ["gold"] = player.Gold,
            ["damage_dealt"] = player.ExtraFields?.DamageDealt ?? 0,
            ["debuffs_applied"] = player.ExtraFields?.DebuffsApplied ?? 0,
            ["deck"] = deck,
            ["relics"] = relics,
            ["potions"] = potions
        };
    }

    private static JsonArray BuildActs(SerializableRun run, bool victory)
    {
        JsonArray acts = [];
        for (int actIndex = 0; actIndex < run.Acts.Count; actIndex++)
        {
            acts.Add(new JsonObject
            {
                ["id"] = EntryOf(run.Acts[actIndex].Id),
                ["completed"] = actIndex < run.MapPointHistory.Count - 1 || victory
            });
        }

        return acts;
    }

    private static JsonArray BuildFloors(SerializableRun run, ulong localPlayerId)
    {
        JsonArray floors = [];
        int floorNumber = 0;

        for (int actIndex = 0; actIndex < run.MapPointHistory.Count; actIndex++)
        {
            foreach (MapPointHistoryEntry mapPoint in run.MapPointHistory[actIndex])
            {
                floorNumber++;
                PlayerMapPointHistoryEntry playerEntry = mapPoint.GetEntry(localPlayerId);
                floors.Add(BuildFloor(mapPoint, playerEntry, actIndex, floorNumber));
            }
        }

        return floors;
    }

    private static JsonObject BuildFloor(
        MapPointHistoryEntry mapPoint,
        PlayerMapPointHistoryEntry playerEntry,
        int actIndex,
        int floorNumber)
    {
        JsonArray rooms = [];
        foreach (MapPointRoomHistoryEntry room in mapPoint.Rooms)
        {
            rooms.Add(new JsonObject
            {
                ["type"] = room.RoomType.ToString(),
                ["id"] = EntryOf(room.ModelId),
                ["turns"] = room.TurnsTaken,
                ["monsters"] = BuildIdArray(room.MonsterIds)
            });
        }

        JsonArray cardChoices = [];
        foreach (CardChoiceHistoryEntry choice in playerEntry.CardChoices)
        {
            cardChoices.Add(new JsonObject
            {
                ["picked"] = choice.wasPicked,
                ["card"] = BuildCard(choice.Card)
            });
        }

        JsonArray relicChoices = [];
        foreach (ModelChoiceHistoryEntry choice in playerEntry.RelicChoices)
        {
            relicChoices.Add(new JsonObject
            {
                ["picked"] = choice.wasPicked,
                ["id"] = EntryOf(choice.choice)
            });
        }

        JsonArray potionChoices = [];
        foreach (ModelChoiceHistoryEntry choice in playerEntry.PotionChoices)
        {
            potionChoices.Add(new JsonObject
            {
                ["picked"] = choice.wasPicked,
                ["id"] = EntryOf(choice.choice)
            });
        }

        JsonArray ancientChoices = [];
        foreach (AncientChoiceHistoryEntry choice in playerEntry.AncientChoices)
        {
            ancientChoices.Add(new JsonObject
            {
                ["picked"] = choice.WasChosen,
                ["choice_key"] = choice.TextKey
            });
        }

        return new JsonObject
        {
            ["floor"] = floorNumber,
            ["act_index"] = actIndex,
            ["map_point_type"] = mapPoint.MapPointType.ToString(),
            ["rooms"] = rooms,
            ["current_hp"] = playerEntry.CurrentHp,
            ["max_hp"] = playerEntry.MaxHp,
            ["current_gold"] = playerEntry.CurrentGold,
            ["damage_taken"] = Math.Clamp(
                playerEntry.DamageTaken,
                0,
                Math.Min(Math.Max(playerEntry.MaxHp, 0), MaximumEncounterDamage)),
            ["hp_healed"] = Math.Max(0, playerEntry.HpHealed),
            ["gold_gained"] = Math.Max(0, playerEntry.GoldGained),
            ["gold_spent"] = Math.Max(0, playerEntry.GoldSpent),
            ["cards_gained"] = BuildCardArray(playerEntry.CardsGained),
            ["cards_removed"] = BuildCardArray(playerEntry.CardsRemoved),
            ["cards_upgraded"] = BuildIdArray(playerEntry.UpgradedCards),
            ["cards_downgraded"] = BuildIdArray(playerEntry.DowngradedCards),
            ["card_choices"] = cardChoices,
            ["relic_choices"] = relicChoices,
            ["potion_choices"] = potionChoices,
            ["relics_removed"] = BuildIdArray(playerEntry.RelicsRemoved),
            ["potions_used"] = BuildIdArray(playerEntry.PotionUsed),
            ["potions_discarded"] = BuildIdArray(playerEntry.PotionDiscarded),
            ["event_choices"] = BuildEventChoiceArray(playerEntry.EventChoices),
            ["ancient_choices"] = ancientChoices,
            ["rest_site_choices"] = BuildStringArray(playerEntry.RestSiteChoices),
            ["bought_relics"] = BuildIdArray(playerEntry.BoughtRelics),
            ["bought_potions"] = BuildIdArray(playerEntry.BoughtPotions),
            ["bought_colorless_cards"] = BuildIdArray(playerEntry.BoughtColorless)
        };
    }

    private static JsonObject BuildCard(SerializableCard card)
    {
        JsonObject result = new()
        {
            ["id"] = EntryOf(card.Id),
            ["upgrade_level"] = card.CurrentUpgradeLevel,
            ["floor_added"] = card.FloorAddedToDeck
        };

        if (card.Enchantment is { } enchantment)
        {
            result["enchantment"] = new JsonObject
            {
                ["id"] = EntryOf(enchantment.Id),
                ["amount"] = enchantment.Amount
            };
        }

        // SavedProperties is intentionally excluded. It is open-ended and can
        // contain arbitrary base-game or third-party state.
        return result;
    }

    private static JsonArray BuildCardArray(IEnumerable<SerializableCard> cards)
    {
        JsonArray result = [];
        foreach (SerializableCard card in cards)
            result.Add(BuildCard(card));
        return result;
    }

    private static JsonArray BuildIdArray(IEnumerable<ModelId> ids)
    {
        JsonArray result = [];
        foreach (ModelId id in ids)
            result.Add(id.Entry);
        return result;
    }

    private static JsonArray BuildStringArray(IEnumerable<string> values)
    {
        JsonArray result = [];
        foreach (string value in values)
            result.Add(value);
        return result;
    }

    private static JsonArray BuildEventChoiceArray(
        IEnumerable<EventOptionHistoryEntry> choices)
    {
        JsonArray result = [];
        foreach (EventOptionHistoryEntry choice in choices)
        {
            // Keep the stable localization key, not rendered text or dynamic
            // variables that may contain irrelevant implementation details.
            result.Add(choice.Title.LocEntryKey);
        }

        return result;
    }

    private static string? FindLastEncounterId(SerializableRun run)
    {
        return run.MapPointHistory
            .SelectMany(act => act)
            .SelectMany(point => point.Rooms)
            .LastOrDefault(room => room.RoomType.IsCombatRoom())
            ?.ModelId?.Entry;
    }

    private static string? EntryOf(ModelId? id) => id?.Entry;

    private static string GetModVersion()
    {
        Assembly assembly = typeof(MgrTelemetry).Assembly;
        return assembly
                   .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                   ?.InformationalVersion
               ?? assembly.GetName().Version?.ToString()
               ?? "unknown";
    }
}
