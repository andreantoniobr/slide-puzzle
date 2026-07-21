using UnityEngine;

namespace BoardFrame.Geometry
{
    /// <summary>
    /// Funções matemáticas puras de geometria 2D, sem dependência de nenhum
    /// outro arquivo do sistema. Base para PathBuilder, PathSmoother,
    /// TangentGenerator, PathExtrusion e UVGenerator.
    /// </summary>
    public static class GeometryUtils
    {
        public const float Epsilon = 0.0001f;

        // ────────────────────────────────────────────────────────────
        //  Interseção e distância
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// Interseção entre duas retas infinitas, cada uma definida por dois pontos.
        /// Retorna null se as retas forem paralelas (ou quase).
        /// </summary>
        public static Vector2? LineIntersection(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
        {
            float d = (p1.x - p2.x) * (p3.y - p4.y) - (p1.y - p2.y) * (p3.x - p4.x);
            if (Mathf.Abs(d) < Epsilon) return null;

            float t = ((p1.x - p3.x) * (p3.y - p4.y) - (p1.y - p3.y) * (p3.x - p4.x)) / d;
            return p1 + t * (p2 - p1);
        }

        /// <summary>Distância perpendicular de um ponto a uma reta infinita (definida por a-b).</summary>
        public static float DistancePointToLine(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            if (lengthSq < Epsilon) return Vector2.Distance(point, a);

            float t = Vector2.Dot(point - a, ab) / lengthSq;
            Vector2 projection = a + t * ab;
            return Vector2.Distance(point, projection);
        }

        // ────────────────────────────────────────────────────────────
        //  Ângulos e curvatura
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// Produto vetorial 2D (cross) entre duas direções. Sinal positivo indica
        /// giro anti-horário (esquerda), negativo indica giro horário (direita).
        /// </summary>
        public static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        /// <summary>
        /// Ângulo (em radianos) entre duas direções normalizadas, sempre positivo (0 a PI).
        /// Use Cross() separadamente se precisar do sinal/direção do giro.
        /// </summary>
        public static float AngleBetween(Vector2 dirA, Vector2 dirB)
        {
            float dot = Mathf.Clamp(Vector2.Dot(dirA.normalized, dirB.normalized), -1f, 1f);
            return Mathf.Acos(dot);
        }

        /// <summary>
        /// Diferença angular assinada (radianos) do menor caminho de dirA até dirB,
        /// já considerando o sentido correto de rotação (positivo = anti-horário).
        /// Use para interpolar arcos sem "voltas erradas".
        /// </summary>
        public static float SignedAngleDelta(Vector2 dirA, Vector2 dirB)
        {
            float angleA = Mathf.Atan2(dirA.y, dirA.x) * Mathf.Rad2Deg;
            float angleB = Mathf.Atan2(dirB.y, dirB.x) * Mathf.Rad2Deg;
            return Mathf.DeltaAngle(angleA, angleB) * Mathf.Deg2Rad;
        }

        /// <summary>
        /// Classifica a curvatura em um vértice do caminho, dado o sentido de
        /// enrolamento geral do polígono (isCCW). Retorna:
        ///   0 = reto (colinear)
        ///   1 = convexo (curva "abrindo" para fora)
        ///  -1 = côncavo (curva "fechando" para dentro, tipo reentrância em L)
        /// </summary>
        public static int ClassifyTurn(Vector2 dirIn, Vector2 dirOut, bool isCCW)
        {
            float cross = Cross(dirIn, dirOut);
            if (Mathf.Abs(cross) <= Epsilon) return 0;

            bool isConvex = isCCW ? cross > 0f : cross < 0f;
            return isConvex ? 1 : -1;
        }

        // ────────────────────────────────────────────────────────────
        //  Polígono
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// Área assinada de um polígono (shoelace formula). Positiva = sentido
        /// anti-horário (CCW), negativa = horário (CW). Usada para determinar
        /// para qual lado normais de offset devem apontar.
        /// </summary>
        public static float SignedArea(System.Collections.Generic.IReadOnlyList<Vector2> polygon)
        {
            float area = 0f;
            int n = polygon.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % n];
                area += (a.x * b.y - b.x * a.y);
            }
            return area * 0.5f;
        }

        public static bool IsCounterClockwise(System.Collections.Generic.IReadOnlyList<Vector2> polygon)
        {
            return SignedArea(polygon) > 0f;
        }

        /// <summary>
        /// Normal perpendicular a uma direção, apontando para "fora" do polígono,
        /// respeitando o sentido de enrolamento (isCCW).
        /// </summary>
        public static Vector2 OutwardNormal(Vector2 direction, bool isCCW)
        {
            return isCCW
                ? new Vector2(direction.y, -direction.x)
                : new Vector2(-direction.y, direction.x);
        }

        // ────────────────────────────────────────────────────────────
        //  Ponto em arco (usado por arcos convexos e fillets côncavos)
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// Calcula um ponto sobre um arco de círculo, dado centro, raio, ângulo
        /// inicial/final (radianos) e o parâmetro t (0 a 1) de interpolação.
        /// </summary>
        public static Vector2 PointOnArc(Vector2 center, float radius, float angleStart, float angleDelta, float t)
        {
            float angle = angleStart + angleDelta * t;
            return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        /// <summary>Número de segmentos recomendado para um arco, baseado no ângulo total e numa densidade fixa.</summary>
        public static int RecommendedArcSegments(float angleDeltaRadians, int minSegments, int maxSegments, float segmentsPerRadian = 4f)
        {
            int estimated = Mathf.CeilToInt(Mathf.Abs(angleDeltaRadians) * segmentsPerRadian);
            return Mathf.Clamp(estimated, minSegments, maxSegments);
        }

        public static Vector2 EvaluateQuadraticBezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            Vector2 p0 = Vector2.Lerp(a, b, t);
            Vector2 p1 = Vector2.Lerp(b, c, t);
            return Vector2.Lerp(p0, p1, t);
        }
    }
}