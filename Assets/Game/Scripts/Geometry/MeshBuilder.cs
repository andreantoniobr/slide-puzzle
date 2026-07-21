using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BoardFrame.Data;

namespace BoardFrame.Geometry
{
    /// <summary>
    /// Extrude a borda interna (já suavizada e reamostrada por CornerRounder)
    /// em uma faixa fechada e contínua — adaptação direta da técnica do
    /// RoadCreator de referência: para cada ponto, calcula a normal "para
    /// fora" localmente (a partir dos vizinhos), desloca pela espessura, e
    /// conecta o último ponto de volta ao primeiro (loop fechado nativo).
    /// UV.x é a distância real acumulada ao longo do contorno interno,
    /// dividida pelo tamanho de repetição da textura — sempre contínuo.
    /// </summary>
    public static class MeshBuilder
    {
        public static void AppendFrame(VertexHelper vh, List<Vector2> innerPoints,
                                        FrameSettings settings, Color32 vertexColor)
        {
            int count = innerPoints.Count;
            if (count < 3) return;

            bool isCCW = GeometryUtils.IsCounterClockwise(innerPoints);
            float thickness = settings.outerBorderThickness;

            var outerPoints = new List<Vector2>(count);
            for (int i = 0; i < count; i++)
            {
                Vector2 prev = innerPoints[(i - 1 + count) % count];
                Vector2 curr = innerPoints[i];
                Vector2 next = innerPoints[(i + 1) % count];

                Vector2 dirIn = (curr - prev).normalized;
                Vector2 dirOut = (next - curr).normalized;
                Vector2 normalIn = GeometryUtils.OutwardNormal(dirIn, isCCW);
                Vector2 normalOut = GeometryUtils.OutwardNormal(dirOut, isCCW);

                Vector2 normal = (normalIn + normalOut);
                normal = normal.sqrMagnitude < GeometryUtils.Epsilon ? normalIn : normal.normalized;

                outerPoints.Add(curr + normal * thickness);
            }

            // UV contínuo: distância real acumulada ao longo do contorno interno
            var u = new float[count];
            u[0] = 0f;
            for (int i = 1; i < count; i++)
                u[i] = u[i - 1] + Vector2.Distance(innerPoints[i - 1], innerPoints[i]) / settings.tileWorldLength;

            float closingU = u[count - 1] + Vector2.Distance(innerPoints[count - 1], innerPoints[0]) / settings.tileWorldLength;

            int baseIndex = vh.currentVertCount;
            for (int i = 0; i < count; i++)
            {
                vh.AddVert(innerPoints[i], vertexColor, new Vector2(u[i], 0f));
                vh.AddVert(outerPoints[i], vertexColor, new Vector2(u[i], 1f));
            }

            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                int i0 = baseIndex + i * 2;
                int i1 = i0 + 1;

                if (next == 0)
                {
                    // Fechamento do loop: vértice duplicado com U contínuo,
                    // evitando salto/compressão de textura na última aresta
                    // (mesmo princípio do RoadCreator, que usa módulo — aqui
                    // adaptamos para manter o UV crescente sem reiniciar).
                    int closeBase = vh.currentVertCount;
                    vh.AddVert(innerPoints[0], vertexColor, new Vector2(closingU, 0f));
                    vh.AddVert(outerPoints[0], vertexColor, new Vector2(closingU, 1f));

                    vh.AddTriangle(i0, closeBase, i1);
                    vh.AddTriangle(i1, closeBase, closeBase + 1);
                }
                else
                {
                    int i2 = baseIndex + next * 2;
                    int i3 = i2 + 1;

                    vh.AddTriangle(i0, i2, i1);
                    vh.AddTriangle(i1, i2, i3);
                }
            }
        }
    }
}