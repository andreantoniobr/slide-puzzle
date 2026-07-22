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

        public void Build(int gridWidth, int gridHeight, HashSet<int> activePositions,
                           System.Func<int, Vector2> cellCenterFunc, float cellSize)
        {
            boundaryPaths = PathBuilder.BuildBoundaryPaths(
                gridWidth, gridHeight, activePositions, cellCenterFunc, cellSize);

            SetVerticesDirty();
        }

        public void SetSettings(FrameSettings newSettings)
        {
            settings = newSettings;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (boundaryPaths == null || boundaryPaths.Count == 0) return;

            Color32 vertexColor = color;

            foreach (var rawPath in boundaryPaths)
            {
                if (rawPath.Count < 3) continue;

                List<PathPoint> smoothPath = PathSmoother.Simplify(
                    rawPath, settings.collinearAngleThresholdDegrees, settings.minSegmentLength);
                if (smoothPath.Count < 3) continue;

                List<PathVertexTangents> tangents = TangentGenerator.Generate(smoothPath);
                if (tangents.Count != smoothPath.Count) continue;

                List<Vector2> innerEdge = CornerRounder.BuildRoundedInnerEdge(smoothPath, tangents, settings);
                if (innerEdge.Count < 3) continue;

                MeshBuilder.AppendFrame(vh, innerEdge, settings, vertexColor);
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