using System.Collections.Generic;
using UnityEngine;
using BoardFrame.Data;

namespace BoardFrame.Geometry
{
    /// <summary>
    /// Para cada ponto do caminho, calcula as direções de entrada/saída,
    /// as normais "outward" (apontando para fora do polígono) de cada lado,
    /// e classifica a curvatura (reto/convexo/côncavo). Essa é a informação
    /// que PathExtrusion consome para gerar a geometria offset (arcos, fillets,
    /// retas) sem precisar recalcular nada — TangentGenerator é a única fonte
    /// de verdade sobre "para onde cada vértice aponta".
    ///
    /// Roda DEPOIS de PathSmoother (que já garantiu não haver segmentos
    /// degenerados) e ANTES de PathExtrusion.
    /// </summary>
    public static class TangentGenerator
    {
        public static List<PathVertexTangents> Generate(IReadOnlyList<PathPoint> path)
        {
            int n = path.Count;
            var result = new List<PathVertexTangents>(n);

            if (n < 3) return result;

            bool isCCW = GeometryUtils.IsCounterClockwise(ExtractPositions(path));

            for (int i = 0; i < n; i++)
            {
                Vector2 prev = path[(i - 1 + n) % n].position;
                Vector2 curr = path[i].position;
                Vector2 next = path[(i + 1) % n].position;

                Vector2 dirIn = (curr - prev).normalized;
                Vector2 dirOut = (next - curr).normalized;

                Vector2 normalIn = GeometryUtils.OutwardNormal(dirIn, isCCW);
                Vector2 normalOut = GeometryUtils.OutwardNormal(dirOut, isCCW);

                int turn = GeometryUtils.ClassifyTurn(dirIn, dirOut, isCCW);
                float signedAngleDelta = GeometryUtils.SignedAngleDelta(normalIn, normalOut);

                result.Add(new PathVertexTangents(
                    position: curr,
                    dirIn: dirIn,
                    dirOut: dirOut,
                    normalIn: normalIn,
                    normalOut: normalOut,
                    turnType: (TurnType)turn,
                    signedAngleDelta: signedAngleDelta
                ));
            }

            return result;
        }

        private static List<Vector2> ExtractPositions(IReadOnlyList<PathPoint> path)
        {
            var positions = new List<Vector2>(path.Count);
            foreach (var p in path) positions.Add(p.position);
            return positions;
        }
    }

    /// <summary>Classificação da curvatura em um vértice do caminho.</summary>
    public enum TurnType
    {
        Concave = -1,
        Straight = 0,
        Convex = 1
    }

    /// <summary>
    /// Informação de tangentes/normais pré-calculada para um único vértice
    /// do caminho. Consumida por PathExtrusion para gerar a geometria offset
    /// (reta, arco convexo, ou fillet côncavo) sem precisar reconsultar o
    /// caminho original.
    /// </summary>
    public readonly struct PathVertexTangents
    {
        /// <summary>Posição original do vértice no caminho (não extrudada).</summary>
        public readonly Vector2 position;

        /// <summary>Direção normalizada chegando neste vértice (do ponto anterior).</summary>
        public readonly Vector2 dirIn;

        /// <summary>Direção normalizada saindo deste vértice (para o próximo ponto).</summary>
        public readonly Vector2 dirOut;

        /// <summary>Normal "para fora" do polígono, relativa à aresta de entrada.</summary>
        public readonly Vector2 normalIn;

        /// <summary>Normal "para fora" do polígono, relativa à aresta de saída.</summary>
        public readonly Vector2 normalOut;

        /// <summary>Classificação da curvatura neste vértice.</summary>
        public readonly TurnType turnType;

        /// <summary>
        /// Diferença angular assinada (radianos) entre normalIn e normalOut.
        /// Positivo = giro anti-horário, negativo = horário. Usado diretamente
        /// por PathExtrusion para interpolar arcos na direção correta.
        /// </summary>
        public readonly float signedAngleDelta;

        public PathVertexTangents(Vector2 position, Vector2 dirIn, Vector2 dirOut,
                                   Vector2 normalIn, Vector2 normalOut,
                                   TurnType turnType, float signedAngleDelta)
        {
            this.position = position;
            this.dirIn = dirIn;
            this.dirOut = dirOut;
            this.normalIn = normalIn;
            this.normalOut = normalOut;
            this.turnType = turnType;
            this.signedAngleDelta = signedAngleDelta;
        }
    }
}