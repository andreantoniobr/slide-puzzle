/// <summary>
/// Tipo especial de um tile, além do comportamento numérico padrão.
/// Normal = comportamento 100% atual (compatibilidade com níveis existentes,
/// já que todo tile nasce como Normal se não for configurado no LevelData).
/// </summary>
public enum SpecialTileType
{
    Normal,
    Hole,       // buraco/pedra estática — nunca pode ser movida, nunca reage ao toque
    Rock,       // pedra rachando — precisa de N toques pra virar Normal
    Question,   // interrogação — visual muda, mecânica de movimento idêntica ao Normal
    Lock,       // cadeado — imóvel até ser destrancado por chave(s) adjacentes
    Key         // chave — móvel normalmente, destranca cadeados adjacentes ao se mover
}