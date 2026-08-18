#if STS2_V107
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Commands.Builders;

namespace MegaCrit.Sts2.Core.Commands;

/// <summary>
/// Restores the post-v0.107 fluent overload at compile time. The official
/// build has no CardPlay-aware attack context, so the extra value is ignored.
/// </summary>
internal static class MgrV107AttackCommandExtensions
{
    public static AttackCommand FromCard(
        this AttackCommand command,
        CardModel card,
        CardPlay? cardPlay) =>
        command.FromCard(card);
}
#endif
