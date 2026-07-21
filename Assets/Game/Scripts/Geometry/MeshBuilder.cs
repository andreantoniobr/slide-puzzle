using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BoardFrame.Data;

namespace BoardFrame.Geometry
{
    /// <summary>
    /// Monta a moldura como uma ÚNICA faixa contínua (stroke do contorno com
    /// juntas arredondadas). Em cada vértice, gera um arco interno (raio =
    /// gapOffset) e um externo (raio = gapOffset + thickness), ambos centrados
    /// no vértice, varrendo o mesmo ângulo — então os dois lados ficam
    /// arredondados e a triangulação é uma tira simples entre anéis de mesma
    /// contagem de pontos. Sem peças separadas, sem bowtie, sem degeneração.
    /// UV é contínuo ao longo do perímetro externo (textura uniforme).
    /// </summary>
    public static class MeshBuilder
    {
        public static void AppendFrame(VertexHelper vh, IReadOnlyList<PathPoint> path,
                                        IReadOnlyList<PathVertexTangents> tangents,
                                        FrameSettings settings, Color32 vertexColor)
        {
            int n = path.Count;
            if (n < 3 || tangents.Count != n) return;

            float dInner = Mathf.Max(0f, settings.concaveCornerRadius);
            float dOuter = dInner + settings.thickness;
            int cornerSegs = Mathf.Max(1, settings.convexSegments);

            // ── Etapa 1: gera os dois anéis (interno e externo) ──
            // Cada vértice contribui um pequeno arco (junta arredondada).
            // Trechos retos surgem da conexão entre arcos consecutivos.
            var inner = new List<Vector2>();
            var outer = new List<Vector2>();

            for (int i = 0; i < n; i++)
            {
                var t = tangents[i];
                Vector2 v = t.position;

                float angleStart = Mathf.Atan2(t.normalIn.y, t.normalIn.x);
                float delta = t.signedAngleDelta; // de normalIn a normalOut, já no sentido correto

                // Nº de amostras proporcional ao ângulo do canto (mais ângulo = mais suave)
                int samples = Mathf.Max(1,
                    Mathf.CeilToInt(Mathf.Abs(delta) / (Mathf.PI * 0.5f) * cornerSegs));

                for (int s = 0; s <= samples; s++)
                {
                    float lerp = (float)s / samples;
                    float ang = angleStart + delta * lerp;
                    Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));

                    inner.Add(v + dir * dInner);
                    outer.Add(v + dir * dOuter);
                }
            }

            int m = outer.Count;
            if (m < 2) return;

            // ── Etapa 2: UV contínuo ao longo do comprimento real do anel externo ──
            var u = new float[m];
            u[0] = 0f;
            for (int i = 1; i < m; i++)
                u[i] = u[i - 1] + Vector2.Distance(outer[i - 1], outer[i]) / settings.tileWorldLength;

            float closingU = u[m - 1] + Vector2.Distance(outer[m - 1], outer[0]) / settings.tileWorldLength;

            // ── Etapa 3: gera vértices (inner/outer pareados) ──
            int baseIndex = vh.currentVertCount;
            for (int i = 0; i < m; i++)
            {
                vh.AddVert(inner[i], vertexColor, new Vector2(u[i], 0f));
                vh.AddVert(outer[i], vertexColor, new Vector2(u[i], 1f));
            }

            // Vértice de fechamento duplicado (mesma posição do índice 0, U contínuo)
            // — evita salto/compressão de textura na última aresta do loop.
            int closeBase = vh.currentVertCount;
            vh.AddVert(inner[0], vertexColor, new Vector2(closingU, 0f));
            vh.AddVert(outer[0], vertexColor, new Vector2(closingU, 1f));

            // ── Etapa 4: triangulação em tira (strip) entre os dois anéis ──
            for (int i = 0; i < m - 1; i++)
            {
                int i0 = baseIndex + i * 2;
                int i1 = i0 + 1;
                int i2 = baseIndex + (i + 1) * 2;
                int i3 = i2 + 1;

                vh.AddTriangle(i0, i2, i1);
                vh.AddTriangle(i1, i2, i3);
            }

            // Último quad: do ponto m-1 até o vértice de fechamento
            int last = baseIndex + (m - 1) * 2;
            vh.AddTriangle(last, closeBase, last + 1);
            vh.AddTriangle(last + 1, closeBase, closeBase + 1);
        }
    }
}