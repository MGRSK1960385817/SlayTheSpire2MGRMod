using Godot;

namespace SlayTheSpire2MGRMod.Mechanics;

/// <summary>
/// Code-drawn music glyph vocabulary used by the Performance staff.
/// </summary>
internal enum MgrMusicSymbol
{
    QuarterNote,
    EighthNote,
    SixteenthNote,
    HalfNote,
    BeamedPair,
    BeamedTriplet,
    BeamedQuartet,
    TwoLineChord
}

internal static class MgrMusicGlyphRenderer
{
    public static float GetHalfWidth(MgrMusicSymbol symbol, float size) =>
        symbol switch
        {
            MgrMusicSymbol.BeamedPair => size * 3.5f,
            MgrMusicSymbol.BeamedTriplet => size * 5.35f,
            MgrMusicSymbol.BeamedQuartet => size * 7.1f,
            MgrMusicSymbol.TwoLineChord => size * 2.7f,
            MgrMusicSymbol.SixteenthNote => size * 1.95f,
            _ => size * 1.65f
        };

    public static bool SpansTwoStaffLines(MgrMusicSymbol symbol) =>
        symbol == MgrMusicSymbol.TwoLineChord;

    public static void Draw(
        Node2D canvas,
        MgrMusicSymbol symbol,
        Vector2 center,
        float size,
        Color color,
        float strokeWidth,
        float lineSpacing)
    {
        float headRadius = size;
        float stemHeight = size * 3.2f;
        switch (symbol)
        {
            case MgrMusicSymbol.QuarterNote:
                DrawQuarter(canvas, center, headRadius, stemHeight, color, strokeWidth);
                break;

            case MgrMusicSymbol.EighthNote:
                DrawFlaggedNote(
                    canvas,
                    center,
                    headRadius,
                    stemHeight,
                    color,
                    strokeWidth,
                    flagCount: 1);
                break;

            case MgrMusicSymbol.SixteenthNote:
                DrawFlaggedNote(
                    canvas,
                    center,
                    headRadius,
                    stemHeight * 1.12f,
                    color,
                    strokeWidth,
                    flagCount: 2);
                break;

            case MgrMusicSymbol.HalfNote:
                canvas.DrawCircle(center, headRadius, color, false, strokeWidth, true);
                canvas.DrawLine(
                    center + new Vector2(headRadius * 0.78f, 0f),
                    center + new Vector2(headRadius * 0.78f, -stemHeight),
                    color,
                    strokeWidth,
                    true);
                break;

            case MgrMusicSymbol.BeamedPair:
                DrawBeamedGroup(
                    canvas,
                    center,
                    size,
                    color,
                    strokeWidth,
                    headCount: 2,
                    totalWidth: size * 4.1f,
                    verticalSlope: size * 0.9f);
                break;

            case MgrMusicSymbol.BeamedTriplet:
                DrawBeamedGroup(
                    canvas,
                    center,
                    size,
                    color,
                    strokeWidth,
                    headCount: 3,
                    totalWidth: size * 8.2f,
                    verticalSlope: size * 1.2f);
                break;

            case MgrMusicSymbol.BeamedQuartet:
                DrawBeamedGroup(
                    canvas,
                    center,
                    size,
                    color,
                    strokeWidth,
                    headCount: 4,
                    totalWidth: size * 11.2f,
                    verticalSlope: size * 1.5f);
                break;

            case MgrMusicSymbol.TwoLineChord:
                DrawTwoLineChord(
                    canvas,
                    center,
                    size,
                    color,
                    strokeWidth,
                    lineSpacing);
                break;
        }
    }

    private static void DrawQuarter(
        Node2D canvas,
        Vector2 center,
        float radius,
        float stemHeight,
        Color color,
        float strokeWidth)
    {
        canvas.DrawCircle(center, radius, color, true, -1f, true);
        canvas.DrawLine(
            center + new Vector2(radius * 0.78f, 0f),
            center + new Vector2(radius * 0.78f, -stemHeight),
            color,
            strokeWidth,
            true);
    }

    private static void DrawFlaggedNote(
        Node2D canvas,
        Vector2 center,
        float radius,
        float stemHeight,
        Color color,
        float strokeWidth,
        int flagCount)
    {
        canvas.DrawCircle(center, radius, color, true, -1f, true);
        Vector2 stemTop = center + new Vector2(radius * 0.78f, -stemHeight);
        canvas.DrawLine(
            center + new Vector2(radius * 0.78f, 0f),
            stemTop,
            color,
            strokeWidth,
            true);
        for (int index = 0; index < flagCount; index++)
        {
            Vector2 flagStart = stemTop + new Vector2(0f, index * radius * 0.88f);
            canvas.DrawPolyline(
            [
                flagStart,
                flagStart + new Vector2(radius * 1.35f, radius * 0.70f),
                flagStart + new Vector2(radius * 1.55f, radius * 1.75f),
                flagStart + new Vector2(radius * 0.82f, radius * 2.25f)
            ],
                color,
                strokeWidth,
                true);
        }
    }

    private static void DrawBeamedGroup(
        Node2D canvas,
        Vector2 center,
        float size,
        Color color,
        float strokeWidth,
        int headCount,
        float totalWidth,
        float verticalSlope)
    {
        float stemHeight = size * 3.35f;
        Vector2 firstTop = Vector2.Zero;
        Vector2 lastTop = Vector2.Zero;
        for (int index = 0; index < headCount; index++)
        {
            float progress = headCount <= 1 ? 0.5f : (float)index / (headCount - 1);
            var head = center + new Vector2(
                Mathf.Lerp(-totalWidth * 0.5f, totalWidth * 0.5f, progress),
                Mathf.Lerp(verticalSlope * 0.5f, -verticalSlope * 0.5f, progress));
            canvas.DrawCircle(head, size * 0.82f, color, true, -1f, true);
            Vector2 stemTop = head + new Vector2(size * 0.68f, -stemHeight);
            canvas.DrawLine(
                head + new Vector2(size * 0.68f, 0f),
                stemTop,
                color,
                strokeWidth,
                true);
            if (index == 0)
                firstTop = stemTop;
            if (index == headCount - 1)
                lastTop = stemTop;
        }

        canvas.DrawLine(firstTop, lastTop, color, strokeWidth * 2.15f, true);
    }

    private static void DrawTwoLineChord(
        Node2D canvas,
        Vector2 center,
        float size,
        Color color,
        float strokeWidth,
        float lineSpacing)
    {
        float verticalSpan = MathF.Max(size * 2.8f, lineSpacing);
        Vector2 lowerHead = center + new Vector2(0f, verticalSpan * 0.5f);
        Vector2 upperHead = center + new Vector2(0f, -verticalSpan * 0.5f);
        canvas.DrawCircle(lowerHead, size * 0.88f, color, true, -1f, true);
        canvas.DrawCircle(upperHead, size * 0.88f, color, true, -1f, true);
        Vector2 lowerStemTop = lowerHead + new Vector2(size * 0.72f, -verticalSpan * 1.62f);
        Vector2 upperStemTop = upperHead + new Vector2(size * 0.72f, -verticalSpan * 0.62f);
        canvas.DrawLine(
            lowerHead + new Vector2(size * 0.72f, 0f),
            lowerStemTop,
            color,
            strokeWidth,
            true);
        canvas.DrawLine(
            upperHead + new Vector2(size * 0.72f, 0f),
            upperStemTop,
            color,
            strokeWidth,
            true);
        canvas.DrawLine(
            lowerStemTop,
            upperStemTop,
            color,
            strokeWidth * 2.05f,
            true);
    }

}
