using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BoardFrame.Data;
using BoardFrame.Geometry;

namespace BoardFrame.UI
{
    /// <summary>
    /// Componente de UI que renderiza a moldura do tabuleiro como um único
    /// mesh procedural, contornando o formato real (incluindo formatos
    /// irregulares, com buracos, em L, pirâmides, etc.).
    ///
    /// Orquestra o pipeline completo:
    ///   PathBuilder → PathSmoother → TangentGenerator → PathExtrusion → UVGenerator → MeshBuilder
    ///
    /// Compatível com MaskableGraphic, RectTransform e máscaras de UI da Unity
    /// (herda de MaskableGraphic, participa do Canvas normalmente).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class BoardFrameMesh : MaskableGraphic
    {
        [Header("Configuração da Moldura")]
        [SerializeField] private FrameSettings settings = FrameSettings.Default;

        /// <summary>Acesso somente-leitura às configurações atuais, para depuração/gizmos externos.</summary>
        public FrameSettings Settings => settings;

        public override Texture mainTexture => material != null ? material.mainTexture : base.mainTexture;

        private List<List<PathPoint>> boundaryPaths = new List<List<PathPoint>>();

        /// <summary>
        /// Reconstrói a moldura para o formato de tabuleiro fornecido.
        /// Chame sempre que o nível mudar (novo gridWidth/gridHeight/buracos).
        /// </summary>
        public void Build(int gridWidth, int gridHeight, HashSet<int> activePositions,
                           System.Func<int, Vector2> cellCenterFunc, float cellSize)
        {
            boundaryPaths = PathBuilder.BuildBoundaryPaths(
                gridWidth, gridHeight, activePositions, cellCenterFunc, cellSize);

            SetVerticesDirty();
        }

        /// <summary>Atualiza os parâmetros visuais da moldura em runtime (ex.: slider de debug) e reconstrói.</summary>
        public void SetSettings(FrameSettings newSettings)
        {
            settings = newSettings;
            SetVerticesDirty();
        }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (boundaryPaths == null || boundaryPaths.Count == 0)
            return;

        Color32 vertexColor = color;

        foreach (var rawPath in boundaryPaths)
        {
            if (rawPath.Count < 3) continue;

            List<PathPoint> smoothPath = PathSmoother.Simplify(
                rawPath,
                settings.collinearAngleThresholdDegrees,
                settings.minSegmentLength);

            if (smoothPath.Count < 3) continue;

            List<PathVertexTangents> tangents = TangentGenerator.Generate(smoothPath);
            if (tangents.Count != smoothPath.Count) continue;

            MeshBuilder.AppendFrame(vh, smoothPath, tangents, settings, vertexColor);
        }
    }

#if UNITY_EDITOR
        /// <summary>No Editor, reconstrói a mesh automaticamente ao mudar valores no Inspector.</summary>
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
        }
#endif
    }
}