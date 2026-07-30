using System.Collections.Generic;
using UnityEngine;
using BoardFrame.Data;

namespace BoardFrame.Geometry
{
    /// <summary>
    /// Constrói a borda interna da moldura: pega o contorno real do tabuleiro
    /// (já simplificado e com tangentes calculadas), desloca cada vértice para
    /// fora pelo gapOffset, e suaviza cada canto com uma curva de Bezier
    /// quadrática (em vez de arco/fillet trigonométrico) — a mesma técnica
    /// usada pelo RoadCreator de referência para suavizar o caminho de uma
    /// estrada. O resultado é reamostrado em pontos igualmente espaçados,
    /// pronto para ser extrudado como uma faixa fechada e contínua.
    /// </summary>
    public static class CornerRounder
    {
        public static List<Vector2> BuildRoundedInnerEdge(
            IReadOnlyList<PathPoint> path,
            IReadOnlyList<PathVertexTangents> tangents,
            FrameSettings settings)
        {
            int n = path.Count;
            var raw = new List<Vector2>();

            for (int i = 0; i < n; i++)
            {
                var t = tangents[i];
                Vector2 curr = t.position;

                Vector2 bisector = (t.normalIn + t.normalOut);
                if (bisector.sqrMagnitude < GeometryUtils.Epsilon) bisector = t.normalIn;
                bisector.Normalize();

                // Correção de miter: mover na diagonal (bissetriz) por "gapOffset" não produz
                // o mesmo recuo perpendicular que os lados retos têm — precisa dividir por
                // cos(metade do ângulo entre as normais). Sem isso, cantos recuam MENOS que
                // os lados, deixando a forma "torta"/encolhida de forma desigual — efeito
                // que fica muito visível em buracos pequenos.
                float angleBetweenNormals = GeometryUtils.AngleBetween(t.normalIn, t.normalOut);
                float halfAngle = angleBetweenNormals * 0.5f;
                float cosHalfAngle = Mathf.Cos(halfAngle);

                // Miter limit: evita que o offset "dispare" para o infinito em ângulos quase
                // 180° (cos próximo de 0) — limita a no máximo 4x a distância base.
                const float miterLimit = 4f;
                float miterMultiplier = cosHalfAngle > 0.05f
                    ? Mathf.Min(1f / cosHalfAngle, miterLimit)
                    : miterLimit;

                float miterDistance = settings.gapOffset * miterMultiplier;
                Vector2 offsetVertex = curr + bisector * miterDistance;

                if (t.turnType == TurnType.Straight)
                {
                    raw.Add(offsetVertex);
                    continue;
                }

                bool isConvex = t.turnType == TurnType.Convex;
                float radius = isConvex ? settings.convexCornerRadius : settings.concaveCornerRadius;
                int segs = Mathf.Max(1, isConvex ? settings.convexSegments : settings.concaveSegments);

                // Nunca deixa o raio "comer" mais que metade da aresta adjacente
                Vector2 prev = path[(i - 1 + n) % n].position;
                Vector2 next = path[(i + 1) % n].position;
                float lenIn = Vector2.Distance(prev, curr);
                float lenOut = Vector2.Distance(curr, next);
                radius = Mathf.Clamp(radius, 0f, Mathf.Min(lenIn, lenOut) * 0.5f);

                // Bezier quadrática: entra reto, curva suavemente, sai reto —
                // tangente às duas arestas nos pontos p0 e p2, controlada por p1 (o próprio vértice)
                Vector2 p0 = offsetVertex - t.dirIn * radius;
                Vector2 p2 = offsetVertex + t.dirOut * radius;
                Vector2 p1 = offsetVertex;

                for (int s = 0; s <= segs; s++)
                {
                    float lerp = (float)s / segs;
                    raw.Add(GeometryUtils.EvaluateQuadraticBezier(p0, p1, p2, lerp));
                }
            }

            return ResampleEvenly(raw, Mathf.Max(0.5f, settings.pointSpacing));
        }

        /// <summary>
        /// Reamostra um polígono fechado em pontos igualmente espaçados por
        /// comprimento de arco real — mesma técnica usada em
        /// Path.CalculateEvenlySpacedPoints (referência RoadCreator).
        /// </summary>
        private static List<Vector2> ResampleEvenly(List<Vector2> rawClosedLoop, float spacing)
        {
            int n = rawClosedLoop.Count;
            if (n < 3) return rawClosedLoop;

            var result = new List<Vector2> { rawClosedLoop[0] };
            Vector2 previousPoint = rawClosedLoop[0];
            float carry = 0f;

            for (int i = 1; i <= n; i++)
            {
                Vector2 current = rawClosedLoop[i % n];
                float segLen = Vector2.Distance(previousPoint, current);

                while (carry + segLen >= spacing)
                {
                    float remaining = spacing - carry;
                    Vector2 dir = (current - previousPoint).normalized;
                    Vector2 newPoint = previousPoint + dir * remaining;
                    result.Add(newPoint);
                    previousPoint = newPoint;
                    segLen = Vector2.Distance(previousPoint, current);
                    carry = 0f;
                }

                carry += segLen;
                previousPoint = current;
            }

            return result.Count >= 3 ? result : rawClosedLoop;
        }
    }
}