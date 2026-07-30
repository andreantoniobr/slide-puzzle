using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BoardFrame.Data;
using BoardFrame.Geometry;

namespace BoardFrame.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class BoardFrameMesh : MaskableGraphic
    {
        [SerializeField] private FrameSettings settings = FrameSettings.Default;
        public FrameSettings Settings => settings;

        public override Texture mainTexture => material != null ? material.mainTexture : base.mainTexture;

        private List<List<PathPoint>> boundaryPaths = new List<List<PathPoint>>();

        private float lastCellSize;

        public void Build(int gridWidth, int gridHeight, HashSet<int> activePositions,
                        System.Func<int, Vector2> cellCenterFunc, float cellSize)
        {
            boundaryPaths = PathBuilder.BuildBoundaryPaths(
                gridWidth, gridHeight, activePositions, cellCenterFunc, cellSize);

            lastCellSize = cellSize; // NOVO — guarda pra usar no cálculo de espessura interna

            SetVerticesDirty();
        }

        public void SetSettings(FrameSettings newSettings)
        {
            settings = newSettings;
            SetVerticesDirty();
        }

        /// <summary>
        /// Aplica uma espessura de borda customizada para o nível atual. Se
        /// customThickness for 0 (ou negativo), mantém a espessura padrão já
        /// configurada em Settings, sem alterar nada.
        /// </summary>
        public void ApplyCustomThickness(float customThickness)
        {
            if (customThickness <= 0f) return; // 0 = "não configurado", usa o padrão

            FrameSettings updated = settings;
            updated.outerBorderThickness = customThickness;
            SetSettings(updated);
        }

        private int FindLargestLoopIndex(List<List<PathPoint>> loops)
        {
            int bestIndex = 0;
            float bestArea = float.NegativeInfinity;

            for (int i = 0; i < loops.Count; i++)
            {
                var positions = new List<Vector2>();
                foreach (var p in loops[i]) positions.Add(p.position);

                float area = Mathf.Abs(GeometryUtils.SignedArea(positions));
                if (area > bestArea)
                {
                    bestArea = area;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private List<PathPoint> ReverseLoopIfNeeded(List<PathPoint> path, bool isOuterLoop, bool shouldBeCCW)
        {
            var positions = new List<Vector2>();
            foreach (var p in path) positions.Add(p.position);

            bool currentIsCCW = GeometryUtils.IsCounterClockwise(positions);

            // Loop externo deve ser CCW; loops de buraco (internos) devem ser CW —
            // essa é a convenção padrão para que "outward normal" sempre aponte
            // corretamente: para fora do formato no externo, e para dentro do
            // próprio buraco (não em direção aos tiles) nos internos.
            bool targetIsCCW = isOuterLoop;

            if (currentIsCCW == targetIsCCW)
                return path; // já está no sentido certo

            // Inverte a ordem dos pontos (mantém a mesma forma, mas troca o sentido de enrolamento)
            var reversed = new List<PathPoint>(path);
            reversed.Reverse();
            return reversed;
        }

        private FrameSettings BuildInnerHoleSettings(FrameSettings baseSettings, List<PathPoint> holePath)
        {
            FrameSettings inner = baseSettings;

            Vector2 min = holePath[0].position;
            Vector2 max = holePath[0].position;
            foreach (var p in holePath)
            {
                min = Vector2.Min(min, p.position);
                max = Vector2.Max(max, p.position);
            }
            float holeWidth = max.x - min.x;
            float holeHeight = max.y - min.y;
            float smallestSide = Mathf.Min(holeWidth, holeHeight);

            float desiredThickness = baseSettings.innerHoleThickness;
            float maxPossibleThickness = smallestSide * 0.5f - 0.01f;
            inner.outerBorderThickness = Mathf.Min(desiredThickness, maxPossibleThickness);

            inner.gapOffset = -inner.outerBorderThickness;

            // NOVO: cada tipo de canto usa seu próprio campo, sem depender um do outro
            inner.convexCornerRadius = Mathf.Min(baseSettings.innerHoleConvexCornerRadius, inner.outerBorderThickness * 0.8f);
            inner.concaveCornerRadius = Mathf.Min(baseSettings.innerHoleConcaveCornerRadius, inner.outerBorderThickness * 0.8f);

            return inner;
        }

        /// <summary>
        /// Calcula o recuo necessário para que a borda interna caiba exatamente
        /// dentro do buraco: espaço livre restante = (largura do buraco em células
        /// × cellSize) − 2 × espessura da borda. O recuo é metade da espessura,
        /// já que a borda se estende para os dois lados a partir do contorno
        /// encolhido.
        /// </summary>
        private float ComputeInnerHoleRecess(float borderThickness)
        {
            // O recuo é exatamente igual à espessura da borda: o contorno do buraco
            // encolhe por "thickness" antes de a faixa (que também tem "thickness"
            // de largura) ser desenhada — assim a borda ocupa exatamente a faixa
            // entre "encolhido por thickness" e "o próprio contorno original do buraco".
            return borderThickness;
        }   

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (boundaryPaths == null || boundaryPaths.Count == 0) return;

            Color32 vertexColor = color;
            int outerLoopIndex = FindLargestLoopIndex(boundaryPaths);

            for (int loopIdx = 0; loopIdx < boundaryPaths.Count; loopIdx++)
            {
                var rawPath = boundaryPaths[loopIdx];
                if (rawPath.Count < 3) continue;

                bool isOuterLoop = (loopIdx == outerLoopIndex);

                rawPath = ReverseLoopIfNeeded(rawPath, isOuterLoop, isOuterLoop);

                FrameSettings loopSettings = isOuterLoop
                    ? settings
                    : BuildInnerHoleSettings(settings, rawPath);

                // REMOVIDO o bloco de InsetTowardCentroid — o recuo agora acontece
                // via gapOffset negativo dentro de CornerRounder.BuildRoundedInnerEdge,
                // usando a mesma lógica de bissetriz já validada.

                List<PathPoint> smoothPath = PathSmoother.Simplify(
                    rawPath, settings.collinearAngleThresholdDegrees, settings.minSegmentLength);
                if (smoothPath.Count < 3) continue;

                List<PathVertexTangents> tangents = TangentGenerator.Generate(smoothPath);
                if (tangents.Count != smoothPath.Count) continue;

                List<Vector2> innerEdge = CornerRounder.BuildRoundedInnerEdge(smoothPath, tangents, loopSettings);
                if (innerEdge.Count < 3) continue;

                MeshBuilder.AppendFrame(vh, innerEdge, loopSettings, vertexColor);
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
        }
#endif
    }
}