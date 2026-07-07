using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

#if UNITY_EDITOR || DEVELOPMENT_BUILD

/// <summary>
/// Painel de debug para testar níveis: reiniciar, ir para um nível específico,
/// avançar/voltar, resetar progresso. A UI é criada automaticamente em runtime —
/// não precisa montar nada na cena, só arrastar o LevelManager e dar play.
/// Compilado apenas em Editor ou Development Build; some sozinho em builds finais.
/// </summary>
public class LevelDebugPanel : MonoBehaviour
{
    [SerializeField] private LevelManager levelManager;
    [SerializeField] private Key toggleKey = Key.Backquote;
    [SerializeField] private bool startVisible = false;

    private GameObject panelRoot;
    private Text currentLevelText;
    private InputField levelInputField;

    private void Awake()
    {
        if (levelManager == null)
            levelManager = FindAnyObjectByType<LevelManager>();

        EnsureEventSystem();
        BuildUI();
        panelRoot.SetActive(startVisible);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
            panelRoot.SetActive(!panelRoot.activeSelf);

        if (panelRoot.activeSelf && currentLevelText != null && levelManager != null)
            currentLevelText.text = $"Nível atual: {levelManager.CurrentLevelNumber}";
    }

    


    // ────────────────────────────────────────────────────────────────
    //  Construção da UI
    // ────────────────────────────────────────────────────────────────

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem (Debug)");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }
    }

    private void BuildUI()
    {
        // Canvas próprio, sempre por cima de tudo
        GameObject canvasGO = new GameObject("DebugCanvas");
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // acima da UI do jogo

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);

        canvasGO.AddComponent<GraphicRaycaster>();

        // Painel de fundo
        panelRoot = new GameObject("DebugPanel");
        panelRoot.transform.SetParent(canvasGO.transform, false);

        RectTransform panelRT = panelRoot.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0f, 1f);
        panelRT.anchorMax = new Vector2(0f, 1f);
        panelRT.pivot     = new Vector2(0f, 1f);
        panelRT.anchoredPosition = new Vector2(20f, -20f);
        panelRT.sizeDelta = new Vector2(420f, 480f);

        Image bg = panelRoot.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.75f);

        VerticalLayoutGroup layout = panelRoot.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 16, 16);
        layout.spacing = 10f;
        layout.childControlHeight = false;
        layout.childControlWidth  = true;
        layout.childForceExpandWidth = true;

        ContentSizeFitter fitter = panelRoot.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Título
        CreateText(panelRoot.transform, "DEBUG — Níveis", 28, FontStyle.Bold);

        // Nível atual
        currentLevelText = CreateText(panelRoot.transform, "Nível atual: -", 24, FontStyle.Normal);

        // Campo + botão "Ir para nível"
        GameObject row = new GameObject("GoToLevelRow");
        row.transform.SetParent(panelRoot.transform, false);
        RectTransform rowRT = row.AddComponent<RectTransform>();
        rowRT.sizeDelta = new Vector2(0f, 60f);
        HorizontalLayoutGroup rowLayout = row.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 8f;
        rowLayout.childControlWidth = true;
        rowLayout.childForceExpandWidth = true;

        levelInputField = CreateInputField(row.transform);
        CreateButton(row.transform, "Ir", OnGoToLevelButton);

        // Botões de ação
        CreateButton(panelRoot.transform, "Reiniciar Nível Atual", OnRestartButton);
        CreateButton(panelRoot.transform, "Próximo Nível", OnNextLevelButton);
        CreateButton(panelRoot.transform, "Nível Anterior", OnPreviousLevelButton);
        CreateButton(panelRoot.transform, "Resetar Progresso", OnResetProgressButton);
    }

    private Text CreateText(Transform parent, string content, int fontSize, FontStyle style)
    {
        GameObject go = new GameObject("Text");
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, fontSize + 12f);

        Text text = go.AddComponent<Text>();
        text.text = content;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft;

        return text;
    }

    private Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject($"Button_{label}");
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, 56f);

        Image bg = go.AddComponent<Image>();
        bg.color = new Color(0.2f, 0.5f, 0.9f, 1f);

        Button button = go.AddComponent<Button>();
        button.onClick.AddListener(onClick);

        GameObject textGO = new GameObject("Label");
        textGO.transform.SetParent(go.transform, false);
        RectTransform textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        Text text = textGO.AddComponent<Text>();
        text.text = label;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 22;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;

        return button;
    }

    private InputField CreateInputField(Transform parent)
    {
        GameObject go = new GameObject("LevelInput");
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, 56f);

        Image bg = go.AddComponent<Image>();
        bg.color = Color.white;

        InputField input = go.AddComponent<InputField>();
        input.contentType = InputField.ContentType.IntegerNumber;

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);
        RectTransform textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(10f, 0f);
        textRT.offsetMax = new Vector2(-10f, 0f);

        Text text = textGO.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 22;
        text.color = Color.black;
        text.alignment = TextAnchor.MiddleLeft;
        text.supportRichText = false;

        input.textComponent = text;

        return input;
    }

    // ────────────────────────────────────────────────────────────────
    //  Ações dos botões
    // ────────────────────────────────────────────────────────────────

    private void OnRestartButton()
    {
        levelManager.RestartLevel();
    }

    private void OnGoToLevelButton()
    {
        if (levelInputField == null) return;

        if (int.TryParse(levelInputField.text, out int levelNumber) && levelNumber > 0)
        {
            levelManager.GoToLevel(levelNumber);
        }
        else
        {
            Debug.LogWarning("[LevelDebugPanel] Número de nível inválido.");
        }
    }

    private void OnNextLevelButton()
    {
        levelManager.GoToLevel(levelManager.CurrentLevelNumber + 1);
    }

    private void OnPreviousLevelButton()
    {
        levelManager.GoToLevel(Mathf.Max(1, levelManager.CurrentLevelNumber - 1));
    }

    private void OnResetProgressButton()
    {
        levelManager.ResetProgress();
    }
}

#endif