namespace MobileGadgets.Application.Dtos;

public readonly record struct TiltedPoint(double X, double Y);

/// <summary>The 4 corners of the case's front face in the base photo's own pixel space, in
/// TL/TR/BR/BL order. Computed once at detection time as the intersection of the case's own
/// straight edges (never read directly off the ambiguous rounded corner — see
/// Geometry2D.FitLineTls) and reused for every future customer render of that HeroModel.</summary>
public readonly record struct TiltedQuad(TiltedPoint Tl, TiltedPoint Tr, TiltedPoint Br, TiltedPoint Bl);

public readonly record struct TiltedBbox(double X0, double Y0, double X1, double Y1);
