using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace MGRMod.Mechanics;

/// <summary>
/// Normalizes presentation-only state that Tower 2's NCard pool does not reset.
/// NCard instances are shared by hands, selection grids, rewards and mod VFX, so
/// stale CanvasItem ordering must never cross a pool boundary.
/// </summary>
public static class MgrCardNodePoolSafety
{
    /// <summary>
    /// Restores Godot's default CanvasItem ordering without changing the card's
    /// model, parent, transform or visibility. Safe to run whenever an NCard is
    /// acquired from the global pool or enters an MGR presentation.
    /// </summary>
    public static void NormalizeCanvasOrdering(NCard card)
    {
        if (!GodotObject.IsInstanceValid(card))
            return;

        card.ZIndex = 0;
        card.ZAsRelative = true;
        card.ShowBehindParent = false;
    }

    /// <summary>
    /// Prepares a card for a non-interactive MGR animation. This also removes
    /// stale ordering inherited before the defensive pool patch was installed.
    /// </summary>
    public static void PrepareTemporaryPresentation(NCard card)
    {
        if (!GodotObject.IsInstanceValid(card))
            return;

        NormalizeCanvasOrdering(card);
        card.PivotOffset = Vector2.Zero;
        card.MouseFilter = Control.MouseFilterEnum.Ignore;
    }

    /// <summary>
    /// Cleans every MGR-owned presentation override before returning an NCard
    /// to Tower 2's global pool.
    /// </summary>
    public static void ReleaseTemporaryCard(NCard card)
    {
        if (!GodotObject.IsInstanceValid(card))
            return;

        card.PlayPileTween?.Kill();
        card.PlayPileTween = null;
        PrepareTemporaryPresentation(card);
        card.QueueFreeSafely();
    }
}
