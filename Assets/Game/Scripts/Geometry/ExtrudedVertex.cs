using UnityEngine;

namespace BoardFrame.Data
{
    /// <summary>
    /// Um vértice já extrudado — contém tanto a posição interna (o próprio
    /// contorno do caminho original) quanto a externa (deslocada pela
    /// espessura da moldura). Produzido por PathExtrusion, consumido por
    /// UVGenerator e MeshBuilder.
    /// </summary>
    public readonly struct ExtrudedVertex
    {
        /// <summary>Posição na borda interna da moldura (= ponto do caminho original, sem offset).</summary>
        public readonly Vector2 innerPosition;

        /// <summary>Posição na borda externa da moldura (deslocada para fora pela espessura).</summary>
        public readonly Vector2 outerPosition;

        /// <summary>
        /// Índice do PathPoint original de onde este vértice deriva. Vários
        /// ExtrudedVertex podem compartilhar o mesmo sourcePathIndex quando
        /// fazem parte do mesmo arco/fillet (um vértice do caminho gera
        /// múltiplos pontos extrudados ao longo da curva).
        /// </summary>
        public readonly int sourcePathIndex;

        public ExtrudedVertex(Vector2 innerPosition, Vector2 outerPosition, int sourcePathIndex)
        {
            this.innerPosition = innerPosition;
            this.outerPosition = outerPosition;
            this.sourcePathIndex = sourcePathIndex;
        }
    }
}