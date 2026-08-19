using System;
using UnityEngine;

/// <summary>
/// Configuração de um tile especial dentro de um LevelData — associado por
/// tileId (a mesma identidade lógica usada em customArrangement), não por
/// posição de grid, já que a posição muda conforme o jogador move peças.
/// </summary>
[Serializable]
public class SpecialTileData
{
    public int tileId;
    public SpecialTileType type = SpecialTileType.Normal;

    [Header("Rock — quantos toques até virar Normal")]
    public int rockHitsRequired = 2;

    [Header("Lock — quantas chaves adjacentes até destravar")]
    public int lockRequiredKeys = 1;
}