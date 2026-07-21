using System.Collections.Generic;
using UnityEngine;
using BoardFrame.Data;

namespace BoardFrame.Geometry
{
    /// <summary>
    /// Gera a geometria extrudada (borda externa da moldura) a partir do
    /// caminho original e das tangentes pré-calculadas. Para cada vértice:
    ///   - Reto: um único ponto offset.
    ///   - Convexo: arco suave para fora (a curva "abre").
    ///   - Côncavo: fillet geométrico correto entre as duas retas offset
    ///     (a curva "aperta", sem sobreposição de triângulos).
    ///
    /// Substitui o antigo GenerateOuterContour que vivia dentro do
    /// BoardFrameMesh. Não sabe nada sobre UV, mesh ou textura — só produz
    /// a lista ordenada de ExtrudedVertex.
    /// </summary>
    public static class PathExtrusion
    {
        public static List<ExtrudedVertex> Extrude(
            IReadOnlyList<PathPoint> path,
            IReadOnlyList<PathVertexTangents> tangents,
            FrameSettings settings)
        {
            var result = new List<ExtrudedVertex>();
            int n = path.Count;

            if (n < 3 || tangents.Count != n)
            {
                Debug.LogWarning("[PathExtrusion] Caminho ou tangentes inválidos — extrusão abortada.");
                return result;
            }

            float dist = settings.OffsetDistance;

            for (int i = 0; i < n; i++)
            {
                var t = tangents[i];
                Vector2 curr = t.position;

                switch (t.turnType)
                {
                    case TurnType.Straight:
                        ExtrudeStraight(result, curr, t, dist, i);
                        break;

                    case TurnType.Convex:
                        ExtrudeConvexArc(result, curr, t, dist, settings.convexSegments, i);
                        break;

                    case TurnType.Concave:
                        ExtrudeConcaveFillet(result, curr, t, dist, settings, i);
                        break;
                }
            }

            return result;
        }

        // ────────────────────────────────────────────────────────────
        //  Vértice reto — offset simples
        // ────────────────────────────────────────────────────────────

        private static void ExtrudeStraight(List<ExtrudedVertex> result, Vector2 curr,
                                             PathVertexTangents t, float dist, int sourceIndex)
        {
            Vector2 offsetPoint = curr + t.normalIn * dist;
            result.Add(new ExtrudedVertex(curr, offsetPoint, sourceIndex));
        }

        // ────────────────────────────────────────────────────────────
        //  Canto convexo — arco simples ao redor do vértice
        // ────────────────────────────────────────────────────────────

        private static void ExtrudeConvexArc(List<ExtrudedVertex> result, Vector2 curr,
                                              PathVertexTangents t, float dist, int segments, int sourceIndex)
        {
            float angleStart = Mathf.Atan2(t.normalIn.y, t.normalIn.x);
            int segs = Mathf.Max(1, segments);

            for (int s = 0; s <= segs; s++)
            {
                float lerp = (float)s / segs;
                Vector2 offsetPoint = GeometryUtils.PointOnArc(curr, dist, angleStart, t.signedAngleDelta, lerp);
                result.Add(new ExtrudedVertex(curr, offsetPoint, sourceIndex));
            }
        }

        // ────────────────────────────────────────────────────────────
        //  Canto côncavo — fillet geométrico correto entre as duas retas offset
        // ────────────────────────────────────────────────────────────

        private static void ExtrudeConcaveFillet(List<ExtrudedVertex> result, Vector2 curr,
                                                   PathVertexTangents t, float dist, FrameSettings settings, int sourceIndex)
        {
            Vector2 offsetPrevEdge = curr + t.normalIn * dist;
            Vector2 offsetNextEdge = curr + t.normalOut * dist;

            Vector2? intersection = GeometryUtils.LineIntersection(
                offsetPrevEdge - t.dirIn, offsetPrevEdge,
                offsetNextEdge, offsetNextEdge + t.dirOut);

            Vector2 ip = intersection ?? curr;

            Vector2 u1 = -t.dirIn;
            Vector2 u2 = t.dirOut;

            float phi = GeometryUtils.AngleBetween(u1, u2);
            phi = Mathf.Max(phi, 0.05f); // evita divisão por zero em quinas quase retas

            float radius = Mathf.Min(settings.concaveCornerRadius, dist * 0.95f);
            float tangentDist = radius / Mathf.Tan(phi * 0.5f);
            float centerDist = radius / Mathf.Sin(phi * 0.5f);

            Vector2 tangentA = ip + u1 * tangentDist;
            Vector2 tangentB = ip + u2 * tangentDist;
            Vector2 bisector = (u1 + u2).normalized;
            Vector2 filletCenter = ip + bisector * centerDist;

            Vector2 dirToTangentA = (tangentA - filletCenter).normalized;
            Vector2 dirToTangentB = (tangentB - filletCenter).normalized;

            float angleStart = Mathf.Atan2(dirToTangentA.y, dirToTangentA.x);
            float angleDelta = GeometryUtils.SignedAngleDelta(dirToTangentA, dirToTangentB);

            int segs = Mathf.Max(1, settings.concaveSegments);
            for (int s = 0; s <= segs; s++)
            {
                float lerp = (float)s / segs;
                Vector2 offsetPoint = GeometryUtils.PointOnArc(filletCenter, radius, angleStart, angleDelta, lerp);
                result.Add(new ExtrudedVertex(curr, offsetPoint, sourceIndex));
            }
        }
    }
}