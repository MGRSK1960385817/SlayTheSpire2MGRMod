using System.Reflection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;

namespace MGRMod.Compatibility;

/// <summary>
/// Keeps optional post-0.107 convenience APIs out of MGR's static call sites.
/// CrossVersionCompat can then concentrate on the unavoidable ABI changes
/// (notably CardLocation and changed virtual hook signatures).
/// </summary>
internal static class MgrCrossVersionApi
{
    private const BindingFlags InstanceMembers =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly MethodInfo? CreateCloneForPlayerMethod =
        typeof(CardModel).GetMethod(
            "CreateCloneForPlayer",
            InstanceMembers,
            binder: null,
            [typeof(Player)],
            modifiers: null);

    private static readonly MethodInfo? CreateDupeForPlayerMethod =
        typeof(CardModel).GetMethod(
            "CreateDupe",
            InstanceMembers,
            binder: null,
            [typeof(Player)],
            modifiers: null);

    private static readonly MethodInfo? CreateDupeWithoutOwnerMethod =
        typeof(CardModel).GetMethod(
            "CreateDupe",
            InstanceMembers,
            binder: null,
            Type.EmptyTypes,
            modifiers: null);

    private static readonly FieldInfo? CardOwnerField =
        typeof(CardModel).GetField("_owner", InstanceMembers);

    private static readonly MethodInfo[] NonBlockingDrawMethods =
        typeof(CardPileCmd)
            .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(method => method.Name == "DrawWithoutBlockingOnOtherPlayers")
            .ToArray();

    private static readonly MethodInfo[] DamageMethods =
        typeof(CreatureCmd)
            .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(method => method.Name == "Damage")
            .ToArray();

    private static readonly PropertyInfo? LocalCardSelectorProperty =
        typeof(CardSelectCmd).GetProperty(
            "LocalSelector",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

    public static CardModel CreateCloneForPlayer(CardModel source, Player owner)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(owner);

        if (CreateCloneForPlayerMethod is not null)
        {
            return (CardModel)(CreateCloneForPlayerMethod.Invoke(source, [owner]) ??
                throw new InvalidOperationException(
                    "CardModel.CreateCloneForPlayer returned null."));
        }

        CardModel clone = source.CreateClone();
        if (ReferenceEquals(clone.Owner, owner))
            return clone;

        if (CardOwnerField is null)
        {
            throw new MissingFieldException(
                typeof(CardModel).FullName,
                "_owner");
        }

        CardOwnerField.SetValue(clone, owner);
        return clone;
    }

    public static CardModel CreateDupeForPlayer(CardModel source, Player owner)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(owner);

        MethodInfo method = CreateDupeForPlayerMethod ??
            CreateDupeWithoutOwnerMethod ??
            throw new MissingMethodException(typeof(CardModel).FullName, "CreateDupe");
        object?[] arguments = method.GetParameters().Length == 1 ? [owner] : [];
        CardModel dupe = (CardModel)(method.Invoke(source, arguments) ??
            throw new InvalidOperationException("CardModel.CreateDupe returned null."));
        if (!ReferenceEquals(dupe.Owner, owner))
        {
            if (CardOwnerField is null)
                throw new MissingFieldException(typeof(CardModel).FullName, "_owner");
            CardOwnerField.SetValue(dupe, owner);
        }

        return dupe;
    }

    public static async Task Damage(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        MethodInfo? method = DamageMethods
            .Where(candidate =>
            {
                ParameterInfo[] parameters = candidate.GetParameters();
                return parameters.Length is 6 or 7 &&
                    parameters[0].ParameterType.IsInstanceOfType(choiceContext) &&
                    parameters[1].ParameterType.IsInstanceOfType(target) &&
                    parameters[2].ParameterType == typeof(decimal) &&
                    parameters[3].ParameterType == typeof(ValueProp) &&
                    parameters[4].ParameterType.IsInstanceOfType(dealer) &&
                    parameters[5].ParameterType == typeof(CardModel);
            })
            .OrderByDescending(candidate => candidate.GetParameters().Length)
            .FirstOrDefault();
        if (method is null)
            throw new MissingMethodException(typeof(CreatureCmd).FullName, "Damage");

        object?[] arguments = method.GetParameters().Length == 7
            ? [choiceContext, target, amount, props, dealer, cardSource, cardPlay]
            : [choiceContext, target, amount, props, dealer, cardSource];
        if (method.Invoke(null, arguments) is not Task task)
            throw new InvalidOperationException("CreatureCmd.Damage did not return a Task.");
        await task;
    }

    public static async Task DrawWithoutBlockingOnOtherPlayers(
        PlayerChoiceContext choiceContext,
        decimal count,
        Player player,
        CardModel source)
    {
        ArgumentNullException.ThrowIfNull(choiceContext);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(source);

        foreach (MethodInfo method in NonBlockingDrawMethods
                     .OrderByDescending(candidate => candidate.GetParameters().Length))
        {
            object?[]? arguments = BuildNonBlockingDrawArguments(
                method,
                choiceContext,
                count,
                player,
                source);
            if (arguments is null)
                continue;

            if (method.Invoke(null, arguments) is not Task task)
            {
                throw new InvalidOperationException(
                    $"{method.DeclaringType?.FullName}.{method.Name} did not return a Task.");
            }

            await task;
            return;
        }

        // v0.107 has no branching draw helper. Waiting for the ordinary draw
        // preserves the card effect; only cross-player queue interleaving differs.
        await CardPileCmd.Draw(choiceContext, count, player);
    }

    public static async Task SignalPlayerChoiceBegun(
        PlayerChoiceContext context,
        Player chooser,
        PlayerChoiceOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(chooser);

        MethodInfo? method = context.GetType()
            .GetMethods(InstanceMembers)
            .Where(candidate => candidate.Name == "SignalPlayerChoiceBegun")
            .OrderByDescending(candidate => candidate.GetParameters().Length)
            .FirstOrDefault(candidate =>
            {
                ParameterInfo[] parameters = candidate.GetParameters();
                return parameters.Length switch
                {
                    2 => parameters[0].ParameterType.IsInstanceOfType(chooser) &&
                         parameters[1].ParameterType == typeof(PlayerChoiceOptions),
                    1 => parameters[0].ParameterType == typeof(PlayerChoiceOptions),
                    _ => false
                };
            });

        if (method is null)
        {
            throw new MissingMethodException(
                context.GetType().FullName,
                "SignalPlayerChoiceBegun");
        }

        object?[] arguments = method.GetParameters().Length == 2
            ? [chooser, options]
            : [options];
        if (method.Invoke(context, arguments) is not Task task)
        {
            throw new InvalidOperationException(
                $"{method.DeclaringType?.FullName}.{method.Name} did not return a Task.");
        }

        await task;
    }

    public static ICardSelector? GetLocalCardSelector() =>
        LocalCardSelectorProperty?.GetValue(null) as ICardSelector;

    private static object?[]? BuildNonBlockingDrawArguments(
        MethodInfo method,
        PlayerChoiceContext choiceContext,
        decimal count,
        Player player,
        CardModel source)
    {
        ParameterInfo[] parameters = method.GetParameters();
        if (parameters.Length < 3 ||
            !parameters[0].ParameterType.IsInstanceOfType(choiceContext) ||
            parameters[1].ParameterType != typeof(decimal) ||
            !parameters[2].ParameterType.IsInstanceOfType(player))
        {
            return null;
        }

        var arguments = new object?[parameters.Length];
        arguments[0] = choiceContext;
        arguments[1] = count;
        arguments[2] = player;
        for (int index = 3; index < parameters.Length; index++)
        {
            Type parameterType = parameters[index].ParameterType;
            if (parameterType.IsInstanceOfType(source))
                arguments[index] = source;
            else if (parameterType == typeof(bool))
                arguments[index] = false;
            else if (parameters[index].HasDefaultValue)
                arguments[index] = parameters[index].DefaultValue;
            else
                return null;
        }

        return arguments;
    }
}
