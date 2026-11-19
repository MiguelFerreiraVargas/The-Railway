/// <summary>
/// Qualquer sistema da cena que demora para "ficar pronto" (gerar vegetação,
/// montar chunks, etc.) implementa isso. A tela de carregamento só some quando
/// todos os sistemas ativos disserem que estão prontos.
/// </summary>
public interface ILoadingProgress
{
    /// <summary>Terminou de carregar o que precisa para o jogador começar.</summary>
    bool IsLoaded { get; }

    /// <summary>Progresso de 0 a 1 (só para a barra).</summary>
    float LoadingProgress { get; }

    /// <summary>Texto curto da etapa, ex.: "GERANDO A FLORESTA".</summary>
    string LoadingLabel { get; }

    /// <summary>A tela de carregamento liga/desliga isso para o sistema trabalhar mais rápido escondido.</summary>
    void SetLoadingBoost(bool on);
}
