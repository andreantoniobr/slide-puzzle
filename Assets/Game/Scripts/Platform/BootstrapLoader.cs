using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Inicializa integrações de plataforma (SDK) e então carrega a cena principal
/// do jogo de forma aditiva. Fica apenas na cena Bootstrap, que só existe
/// nas branches de plataforma — a cena Main nunca sabe que isso existe.
/// </summary>
public class BootstrapLoader : MonoBehaviour
{
    [SerializeField] private string mainSceneName = "Main";
    [SerializeField] private string bootstrapSceneName = "Bootstrap";

    private void Start()
    {
        StartCoroutine(LoadMainScene());
    }

    private IEnumerator LoadMainScene()
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(mainSceneName, LoadSceneMode.Additive);
        yield return operation;

        Scene mainScene = SceneManager.GetSceneByName(mainSceneName);
        SceneManager.SetActiveScene(mainScene); // lighting/skybox passam a usar as configs da Main

        // PokiManager já sobrevive via DontDestroyOnLoad — pode descarregar a Bootstrap com segurança
        SceneManager.UnloadSceneAsync(bootstrapSceneName);
    }
}