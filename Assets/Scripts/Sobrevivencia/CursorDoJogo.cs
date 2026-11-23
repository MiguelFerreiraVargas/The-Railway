using StarterAssets;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Esconde e trava o mouse quando uma cena de jogo carrega (Prólogo, CenaPrincipal). O menu deixa o
/// mouse livre e o StarterAssets só trava quando a janela ganha foco — vindo do menu (clicando em
/// JOGAR), o foco não muda e o cursor ficava solto na tela.
/// </summary>
public static class CursorDoJogo
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Iniciar()
    {
        SceneManager.sceneLoaded -= AoCarregar;
        SceneManager.sceneLoaded += AoCarregar;
        Travar();
    }

    private static void AoCarregar(Scene cena, LoadSceneMode modo) => Travar();

    /// <summary>Trava e esconde, se a cena tem o jogador em primeira pessoa.</summary>
    public static void Travar()
    {
        var jogador = Object.FindFirstObjectByType<FirstPersonController>();
        if (jogador == null) return; // menu, telas sem jogador: o mouse fica como está
        var inputs = jogador.GetComponent<StarterAssetsInputs>();
        if (inputs != null && !inputs.cursorLocked) return;
        if (ModoCamera.Instance != null && ModoCamera.Instance.Isometrico) return; // na isométrica o mouse fica livre
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
