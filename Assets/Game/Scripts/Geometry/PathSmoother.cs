using System.Collections.Generic;
using UnityEngine;
using BoardFrame.Data;

namespace BoardFrame.Geometry
{
    /// <summary>
    /// Simplifica um caminho (path) removendo pontos redundantes: vértices
    /// colineares (que não representam uma curva de verdade) e pontos
    /// duplicados/quase-coincidentes (distância abaixo de um limiar).
    ///
    /// Isso é importante porque grids muito simples (retas longas) produzem
    /// vértices intermediários que, matematicamente, não mudam a direção do
    /// caminho — mantê-los só adiciona segmentos de comprimento zero ou
    /// quase-zero, que causam NaN/instabilidade em TangentGenerator e
    /// PathExtrusion (normais calculadas a partir de direções degeneradas).
    ///
    /// Roda ANTES de TangentGenerator e PathExtrusion.
    /// </summary>
    public static class PathSmoother
    {
        /// <summary>
        /// Remove pontos duplicados/quase-coincidentes e pontos colineares
        /// (onde a direção de entrada e saída são praticamente idênticas).
        /// </summary>
        /// <param name="path">Loop de pontos, assumido fechado (não repete o primeiro ponto no final).</param>
        /// <param name="collinearAngleThresholdDegrees">
        /// Abaixo desse ângulo (graus) entre direção de entrada/saída, o vértice
        /// é considerado colinear e removido. Valores pequenos (ex.: 0.5) só
        /// removem colinearidade quase perfeita; valores maiores simplificam mais.
        /// </param>
        /// <param name="minSegmentLength">
        /// Segmentos mais curtos que isso são colapsados (o ponto seguinte é removido).
        /// Evita divisão por zero em cálculos de direção normalizada.
        /// </param>
        public static List<PathPoint> Simplify(
            IReadOnlyList<PathPoint> path,
            float collinearAngleThresholdDegrees = 0.5f,
            float minSegmentLength = 0.01f)
        {
            if (path.Count < 3) return new List<PathPoint>(path);

            // Etapa 1: remove pontos duplicados/quase-coincidentes
            var deduped = RemoveNearDuplicates(path, minSegmentLength);
            if (deduped.Count < 3) return deduped;

            // Etapa 2: remove pontos colineares
            var simplified = RemoveCollinear(deduped, collinearAngleThresholdDegrees);

            // Garantia mínima: nunca retornar menos de 3 pontos (não seria um polígono válido)
            return simplified.Count >= 3 ? simplified : deduped;
        }

        // ────────────────────────────────────────────────────────────
        //  Etapa 1: remoção de duplicados/quase-coincidentes
        // ────────────────────────────────────────────────────────────

        private static List<PathPoint> RemoveNearDuplicates(IReadOnlyList<PathPoint> path, float minSegmentLength)
        {
            var result = new List<PathPoint>(path.Count);
            int n = path.Count;

            for (int i = 0; i < n; i++)
            {
                if (result.Count == 0)
                {
                    result.Add(path[i]);
                    continue;
                }

                float dist = Vector2.Distance(result[result.Count - 1].position, path[i].position);
                if (dist >= minSegmentLength)
                    result.Add(path[i]);
                // senão: ponto descartado, considerado coincidente com o anterior
            }

            // Verifica também o fechamento do loop (último ponto vs primeiro)
            if (result.Count >= 2)
            {
                float closingDist = Vector2.Distance(result[result.Count - 1].position, result[0].position);
                if (closingDist < minSegmentLength)
                    result.RemoveAt(result.Count - 1);
            }

            return result;
        }

        // ────────────────────────────────────────────────────────────
        //  Etapa 2: remoção de vértices colineares
        // ────────────────────────────────────────────────────────────

        private static List<PathPoint> RemoveCollinear(IReadOnlyList<PathPoint> path, float angleThresholdDegrees)
        {
            int n = path.Count;
            var result = new List<PathPoint>(n);

            for (int i = 0; i < n; i++)
            {
                Vector2 prev = path[(i - 1 + n) % n].position;
                Vector2 curr = path[i].position;
                Vector2 next = path[(i + 1) % n].position;

                Vector2 dirIn = (curr - prev);
                Vector2 dirOut = (next - curr);

                float lenIn = dirIn.magnitude;
                float lenOut = dirOut.magnitude;

                // Segmento degenerado (comprimento ~0) — mantém o ponto para não
                // perder topologia; PathExtrusion trata isso separadamente se necessário.
                if (lenIn < GeometryUtils.Epsilon || lenOut < GeometryUtils.Epsilon)
                {
                    result.Add(path[i]);
                    continue;
                }

                float angleDeg = GeometryUtils.AngleBetween(dirIn.normalized, dirOut.normalized) * Mathf.Rad2Deg;

                bool isCollinear = angleDeg <= angleThresholdDegrees;
                if (!isCollinear)
                    result.Add(path[i]);
                // senão: ponto colinear, removido — a reta entre prev e next já cobre esse trecho
            }

            return result;
        }
    }
}