namespace MobileGadgets.Application.Dtos;

public readonly record struct TiltedPoint(double X, double Y);

/// <summary>The 4 corners of the case's front face in the base photo's own pixel space, in
/// TL/TR/BR/BL order. Computed once at detection time as the intersection of the case's own
/// straight edges (never read directly off the ambiguous rounded corner — see
/// Geometry2D.FitLineTls) and reused for every future customer render of that HeroModel.</summary>
public readonly record struct TiltedQuad(TiltedPoint Tl, TiltedPoint Tr, TiltedPoint Br, TiltedPoint Bl);

public readonly record struct TiltedBbox(double X0, double Y0, double X1, double Y1);

/// <summary>An admin-placed local override on the camera area, beyond what the main outline
/// covers — the two spot-fix tools an admin needs when the problem is a specific point, not the
/// whole outline: <see cref="Protect"/> true marks a small area that must NEVER get the design
/// even though it falls inside the normally-printable region (a stray uncaught hole, a logo); false
/// marks a small area that SHOULD get the design even though the outline currently excludes it
/// (part of the real body wrongly swallowed by an imperfect outline).</summary>
public readonly record struct CameraSpot(double X, double Y, double Radius, bool Protect);
