using UnityEngine;

namespace BoardFrame.Data
{
    /// <summary>
    /// Um ponto do caminho (contorno) do tabuleiro, antes da extrusão.
    /// Representa um vértice puro do polígono de contorno, na ordem em que
    /// aparece ao redor da forma.
    /// </summary>
    [System.Serializable]
    public struct PathPoint
    {
        /// <summary>Posição do vértice no espaço local do RectTransform.</summary>
        public Vector2 position;

        /// <summary>
        /// Índice original desse vértice na extração de contorno (grid vr,vc
        /// codificado). Útil para depuração; não usado pela extrusão em si.
        /// </summary>
        public int sourceIndex;

        public PathPoint(Vector2 position, int sourceIndex)
        {
            this.position = position;
            this.sourceIndex = sourceIndex;
        }
    }
}