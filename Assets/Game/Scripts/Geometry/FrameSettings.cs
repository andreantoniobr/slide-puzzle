using System;
using UnityEngine;

namespace BoardFrame.Data
{
    /// <summary>
    /// Todos os parâmetros configuráveis da moldura, agrupados em um único
    /// struct. Passado explicitamente para PathExtrusion e UVGenerator,
    /// em vez de cada função receber uma lista longa de parâmetros soltos.
    /// </summary>
    [Serializable]
    public struct FrameSettings
    {
        [Header("Espessura")]
        public float thickness;
        public float gapOffset;

        [Header("Cantos Convexos")]
        public int convexSegments;

        [Header("Cantos Côncavos")]
        public float concaveCornerRadius;
        public int concaveSegments;

        [Header("Simplificação de Caminho")]
        public float collinearAngleThresholdDegrees;
        public float minSegmentLength;

        [Header("Textura")]
        public float tileWorldLength;

        public static FrameSettings Default => new FrameSettings
        {
            thickness = 24f,
            gapOffset = 0f,
            convexSegments = 6,
            concaveCornerRadius = 12f,
            concaveSegments = 6,
            collinearAngleThresholdDegrees = 0.5f,
            minSegmentLength = 0.01f,
            tileWorldLength = 64f
        };

        /// <summary>Distância total do offset (thickness + gapOffset), usada repetidamente pela extrusão.</summary>
        public readonly float OffsetDistance => thickness + gapOffset;
    }
}