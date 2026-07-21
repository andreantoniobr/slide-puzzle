using System.Collections.Generic;
using UnityEngine;
using BoardFrame.Data;

namespace BoardFrame.Geometry
{
    public static class UVGenerator
    {
        public readonly struct UVResult
        {
            public readonly List<ExtrudedVertex> orderedVertices;
            public readonly List<float> u; 
            public readonly ExtrudedVertex closingVertex;
            public readonly float closingU;

            public UVResult(List<ExtrudedVertex> orderedVertices, List<float> u,
                             ExtrudedVertex closingVertex, float closingU)
            {
                this.orderedVertices = orderedVertices;
                this.u = u;
                this.closingVertex = closingVertex;
                this.closingU = closingU;
            }
        }

        public static UVResult Generate(List<ExtrudedVertex> extrudedVertices, FrameSettings settings)
        {
            if (extrudedVertices == null || extrudedVertices.Count < 2)
            {
                Debug.LogWarning("[UVGenerator] Vértices extrudados insuficientes — UV abortado.");
                return new UVResult(extrudedVertices ?? new List<ExtrudedVertex>(), new List<float>(), default, 0f);
            }

            int seamIndex = FindDiscreteSeamIndex(extrudedVertices);
            List<ExtrudedVertex> ordered = Rotate(extrudedVertices, seamIndex);

            int m = ordered.Count;
            var u = new List<float>(m);
            u.Add(0f);

            for (int i = 1; i < m; i++)
            {
                float segmentLength = Vector2.Distance(ordered[i - 1].outerPosition, ordered[i].outerPosition);
                
                if (ordered[i].innerPosition == ordered[i - 1].innerPosition)
                {
                    segmentLength *= 0.5f; 
                }

                u.Add(u[i - 1] + segmentLength / settings.tileWorldLength);
            }

            float closingLength = Vector2.Distance(ordered[m - 1].outerPosition, ordered[0].outerPosition);
            if (ordered[0].innerPosition == ordered[m - 1].innerPosition)
            {
                closingLength *= 0.5f;
            }

            float closingU = u[m - 1] + closingLength / settings.tileWorldLength;
            ExtrudedVertex closingVertex = ordered[0];

            return new UVResult(ordered, u, closingVertex, closingU);
        }

        private static int FindDiscreteSeamIndex(List<ExtrudedVertex> vertices)
        {
            int m = vertices.Count;
            if (m == 0) return 0;

            float minY = float.PositiveInfinity;
            foreach (var v in vertices) minY = Mathf.Min(minY, v.outerPosition.y);

            int bestIndex = 0;
            float bestScore = float.NegativeInfinity;
            const float baseProximityWindowFactor = 8f;

            for (int i = 0; i < m; i++)
            {
                Vector2 a = vertices[i].outerPosition;
                Vector2 b = vertices[(i + 1) % m].outerPosition;

                Vector2 edge = b - a;
                float length = edge.magnitude;
                if (length < GeometryUtils.Epsilon) continue;

                float horizontality = Mathf.Abs(edge.x) / length; 
                float avgY = (a.y + b.y) * 0.5f;

                float proximityWindow = Mathf.Max(length, 1f) * baseProximityWindowFactor;
                float bottomCloseness = 1f - Mathf.InverseLerp(minY, minY + proximityWindow, avgY);
                bottomCloseness = Mathf.Clamp01(bottomCloseness);

                float score = length * (0.3f + 0.7f * horizontality) * (0.3f + 0.7f * bottomCloseness);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private static List<ExtrudedVertex> Rotate(List<ExtrudedVertex> vertices, int startIndex)
        {
            int m = vertices.Count;
            if (startIndex <= 0 || startIndex >= m) return new List<ExtrudedVertex>(vertices);

            var rotated = new List<ExtrudedVertex>(m);
            for (int k = 0; k < m; k++)
            {
                rotated.Add(vertices[(startIndex + k) % m]);
            }
            return rotated;
        }
    }
}