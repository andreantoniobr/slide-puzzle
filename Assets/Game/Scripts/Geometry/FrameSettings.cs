using System;
using UnityEngine;

namespace BoardFrame.Data
{
    [Serializable]
    public struct FrameSettings
    {
        [Header("Espaçamento")]
        [Tooltip("Distância entre o conteúdo (tiles) e a borda interna da moldura.")]
        public float gapOffset;

        [Header("Borda Externa")]
        [Tooltip("Espessura da faixa visível da moldura.")]
        public float outerBorderThickness;

        [Header("Cantos Convexos")]
        public float convexCornerRadius;
        public int convexSegments;

        [Header("Cantos Côncavos")]
        public float concaveCornerRadius;
        public int concaveSegments;

        [Header("Reamostragem")]
        [Tooltip("Distância entre pontos após reamostrar a curva suavizada — controla a densidade final da malha.")]
        public float pointSpacing;

        [Header("Simplificação de Caminho")]
        public float collinearAngleThresholdDegrees;
        public float minSegmentLength;

        [Header("Textura")]
        public float tileWorldLength;

        public static FrameSettings Default => new FrameSettings
        {
            gapOffset = 10f,
            outerBorderThickness = 24f,
            convexCornerRadius = 20f,
            convexSegments = 6,
            concaveCornerRadius = 16f,
            concaveSegments = 6,
            pointSpacing = 8f,
            collinearAngleThresholdDegrees = 0.5f,
            minSegmentLength = 0.01f,
            tileWorldLength = 64f
        };
    }
}