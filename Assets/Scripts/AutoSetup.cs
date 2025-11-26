using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public class AutoSetup : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureAutoSetupExists()
    {
        if (FindObjectOfType<AutoSetup>() != null) return;
        var go = new GameObject("AutoSetup");
        go.AddComponent<AutoSetup>();
        Object.DontDestroyOnLoad(go);
    }

    private void OnEnable()
    {
        // シーンが読み込まれるたびに初期化を実行
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // シーン読み込み後に初期化を実行
        InitializeScene();
    }

    [Tooltip("Capsule オブジェクト名の接頭辞。該当名を持つオブジェクトを自動検出します。")]
    public string[] agentNames =
    {
        "Capsule-w-1","Capsule-w-2","Capsule-w-3",
        "Capsule-b-1","Capsule-b-2","Capsule-b-3",
        "HumanM_Model"
    };

    [Header("First Person Camera")]
    public bool setupFirstPersonCamera = true;
    public bool disableMainCamera = true;

    [Header("Ball Pass Setup")]
    public bool setupBallPass = true;
    public BallPassController.PassTeam defaultPassTeam = BallPassController.PassTeam.White;



    [Header("Rendering Settings")]
    [Tooltip("リフレクションプローブ設定を一括調整する")]
    public bool adjustReflectionProbes = true;
    [Tooltip("Reflection Probes の使用モード（Off または Simple 推奨）")]
    public ReflectionProbeUsage reflectionProbeUsage = ReflectionProbeUsage.Off;
    [Tooltip("Anchor Override として使う Transform（任意）")]
    public Transform reflectionProbeAnchorOverride;

    private void Start()
    {
        // 最初のシーン読み込み時にも初期化を実行
        InitializeScene();
    }

    private void InitializeScene()
    {
        // すべてのコンソール出力を無効化
        Debug.unityLogger.logEnabled = false;

        // 前のシーンから残っているDirectional Lightを削除（現在のシーンのもの以外）
        CleanupOldDirectionalLights();

        EnsureCourtManager();
        AttachAgents();
        if (setupFirstPersonCamera)
        {
            SetupFirstPersonCamera();
        }
        if (setupBallPass)
        {
            SetupBallPass();
        }

        if (adjustReflectionProbes)
        {
            ApplyReflectionProbeSettings();
        }

        // カメラの状態を確認（少なくとも1つのカメラが有効であることを確認）
        EnsureAtLeastOneCameraEnabled();
    }

    private void CleanupOldDirectionalLights()
    {
        // 現在のアクティブなシーンを取得
        Scene currentScene = SceneManager.GetActiveScene();

        // すべてのDirectional Lightを検索
        Light[] allLights = FindObjectsOfType<Light>();
        foreach (Light light in allLights)
        {
            // Directional Lightで、かつ現在のシーンに属していないものを削除
            if (light.type == LightType.Directional &&
                light.gameObject.name == "Directional Light" &&
                light.gameObject.scene != currentScene)
            {
                Debug.Log($"AutoSetup: 前のシーンから残っているDirectional Lightを削除: {light.gameObject.name} (シーン: {light.gameObject.scene.name})");
                Destroy(light.gameObject);
            }
        }
    }

    private void EnsureAtLeastOneCameraEnabled()
    {
        // シーン内のすべてのカメラを取得
        Camera[] allCameras = FindObjectsOfType<Camera>();
        bool hasEnabledCamera = false;

        foreach (Camera cam in allCameras)
        {
            if (cam.enabled && cam.gameObject.activeInHierarchy)
            {
                hasEnabledCamera = true;
                break;
            }
        }

        // 有効なカメラがない場合は、メインカメラを有効化
        if (!hasEnabledCamera)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                mainCamera.enabled = true;
                Debug.LogWarning("AutoSetup: 有効なカメラが見つかりませんでした。メインカメラを有効化しました。");
            }
            else
            {
                // メインカメラもない場合は、最初に見つかったカメラを有効化
                if (allCameras.Length > 0)
                {
                    allCameras[0].enabled = true;
                    Debug.LogWarning($"AutoSetup: カメラが見つかりませんでした。{allCameras[0].name}を有効化しました。");
                }
            }
        }
    }

    private void EnsureCourtManager()
    {
        if (FindObjectOfType<CourtManager>() == null)
        {
            var go = new GameObject("CourtManager");
            go.AddComponent<CourtManager>();
        }
    }

    private void AttachAgents()
    {
        foreach (var name in agentNames)
        {
            var obj = GameObject.Find(name);
            if (obj == null)
            {
                Debug.LogWarning($"AutoSetup: {name} が見つかりません");
                continue;
            }

            // HumanM_Modelの場合は特別な処理
            if (name == "HumanM_Model")
            {
                var walker = obj.GetComponent<HumanMWalker>();
                if (walker == null)
                {
                    walker = obj.AddComponent<HumanMWalker>();
                }
            }
            else
            {
                var controller = obj.GetComponent<BasketballAgentController>();
                if (controller == null)
                {
                    controller = obj.AddComponent<BasketballAgentController>();
                }
            }

            // CharacterController を付与 (存在しない場合)
            var cc = obj.GetComponent<CharacterController>();
            if (cc == null)
            {
                cc = obj.AddComponent<CharacterController>();

                // HumanM_Model用の特別な設定
                if (name == "HumanM_Model")
                {
                    // 人間キャラクター用の設定
                    cc.center = Vector3.up * 0.9f;
                    cc.height = 1.8f;
                    cc.radius = 0.3f;
                }
                else
                {
                    // Capsule のサイズに合わせる簡易設定
                    cc.center = Vector3.up * 1.0f;
                    cc.height = 2.0f;
                    cc.radius = 0.4f;
                }
            }

            // 物理衝突を避けるために Rigidbody は不要。重力も使わない前提
            var rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            // HumanM_Model用のアニメーション設定
            if (name == "HumanM_Model")
            {
                SetupHumanMAnimation(obj);
            }
        }
    }

    private void SetupFirstPersonCamera()
    {
        if (!setupFirstPersonCamera) return;

        // メインカメラを取得（メソッド全体で使用）
        Camera mainCamera = Camera.main;

        // head-cameraオブジェクトを検索
        GameObject headCamera = GameObject.Find("head-camera");
        if (headCamera == null)
        {
            Debug.LogWarning("AutoSetup: head-cameraが見つかりませんでした。メインカメラを使用します。");
            // head-cameraが見つからない場合は、メインカメラを有効のままにする
            if (mainCamera != null)
            {
                mainCamera.enabled = true;
                mainCamera.gameObject.SetActive(true);
            }
            return;
        }

        // head-cameraのGameObjectを有効化（重要！）
        headCamera.SetActive(true);

        Debug.Log($"AutoSetup: head-cameraを検出しました - 位置: {headCamera.transform.position}, ローカル位置: {headCamera.transform.localPosition}");

        // head-cameraにFirstPersonCameraをアタッチ
        FirstPersonCamera firstPersonCamera = headCamera.GetComponent<FirstPersonCamera>();
        if (firstPersonCamera == null)
        {
            firstPersonCamera = headCamera.AddComponent<FirstPersonCamera>();
        }

        // カメラコンポーネントを追加
        Camera camera = headCamera.GetComponent<Camera>();
        if (camera == null)
        {
            camera = headCamera.AddComponent<Camera>();
        }

        // head-cameraのカメラを有効化
        camera.enabled = true;

        // カメラの基本設定を確認・修正
        // Skyboxがない場合に備えて、Solid Colorにフォールバック
        if (RenderSettings.skybox == null && camera.clearFlags == CameraClearFlags.Skybox)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.192f, 0.302f, 0.475f, 0f); // メインカメラと同じ色
        }

        // カメラの深度を確認（メインカメラより高くする）
        if (mainCamera != null && mainCamera.gameObject != headCamera)
        {
            camera.depth = mainCamera.depth + 1; // メインカメラより前に描画
        }

        Debug.Log("AutoSetup: head-cameraにFirstPersonCameraをアタッチしました: " + headCamera.name);

        // メインカメラを無効化（head-cameraが見つかった場合のみ）
        if (disableMainCamera)
        {
            if (mainCamera != null && mainCamera.gameObject != headCamera)
            {
                mainCamera.enabled = false;
                Debug.Log("AutoSetup: メインカメラを無効化しました（head-cameraを使用）");
            }
        }
    }

    private void SetupBallPass()
    {
        // 共通設定関数
        void SetupBallObject(string objectName, BallPassController.PassTeam team)
        {
            GameObject ballObj = GameObject.Find(objectName);
            if (ballObj == null) return;

            BallPassController controller = ballObj.GetComponent<BallPassController>();
            if (controller == null)
            {
                controller = ballObj.AddComponent<BallPassController>();
            }

            controller.passTeam = team;
            controller.enablePreciseLanding = true;
            controller.enablePrediction = false;
            controller.passPauseDuration = 1.0f;
            controller.holdTimeAtReceiver = 0.2f;
        }

        // 既存の Ball は既定チーム
        SetupBallObject("Ball", defaultPassTeam);

        // 追加された Ball2 / Ball２ は黒チーム
        SetupBallObject("Ball2", BallPassController.PassTeam.Black);
        SetupBallObject("Ball２", BallPassController.PassTeam.Black);
    }



    private void ApplyReflectionProbeSettings()
    {
        // 対象: Ball と 各エージェント（子も含む）
        var targets = agentNames
            .Select(GameObject.Find)
            .Where(go => go != null)
            .ToList();

        var ball = GameObject.Find("Ball");
        if (ball != null) targets.Add(ball);



        foreach (var root in targets)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var mr in renderers)
            {
                mr.reflectionProbeUsage = reflectionProbeUsage;
                if (reflectionProbeAnchorOverride != null)
                {
                    mr.probeAnchor = reflectionProbeAnchorOverride;
                }
            }
        }
    }

    private void SetupHumanMAnimation(GameObject humanModel)
    {
        // Animation（Legacy）コンポーネントは使用しないため削除
        var legacyAnimation = humanModel.GetComponent<Animation>();
        if (legacyAnimation != null)
        {
            Object.Destroy(legacyAnimation);
        }

        // Animatorコンポーネントを追加
        Animator animator = humanModel.GetComponent<Animator>();
        if (animator == null)
        {
            animator = humanModel.AddComponent<Animator>();
        }

        // NavMeshAgent は未使用かつ NavMesh 非依存にするため削除
        var navMeshAgent = humanModel.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (navMeshAgent != null)
        {
            Object.Destroy(navMeshAgent);
        }

        // HumanMWalkerコンポーネントを取得
        HumanMWalker walker = humanModel.GetComponent<HumanMWalker>();

        // 歩行アニメーションコントローラーを探して設定
        RuntimeAnimatorController walkController = FindWalkAnimatorController();

        if (walkController != null)
        {
            animator.runtimeAnimatorController = walkController;

            // HumanMWalkerにも設定
            if (walker != null)
            {
                walker.SetAnimatorController(walkController);
            }

            Debug.Log($"AutoSetup: HumanM_Modelに歩行アニメーションを設定しました: {walkController.name}");
        }
        else
        {
            Debug.LogWarning("AutoSetup: 歩行アニメーションコントローラーが見つかりませんでした。手動で設定してください。");
        }

        // アニメーション速度を調整（必要に応じて）
        animator.speed = 1.0f;
    }

    private RuntimeAnimatorController FindWalkAnimatorController()
    {
        // 複数の歩行アニメーションコントローラーを試す
        string[] controllerNames = {
            "HumanM@Walk01_Forward",
            "HumanM@MaskedWalking",
            "HumanM@Walk01_ForwardRight",
            "HumanM@Walk01_ForwardLeft"
        };

        foreach (string controllerName in controllerNames)
        {
            RuntimeAnimatorController controller = Resources.Load<RuntimeAnimatorController>(controllerName);
            if (controller != null)
            {
                Debug.Log($"AutoSetup: アニメーションコントローラーを発見: {controllerName}");
                return controller;
            }
        }

        // Resourcesフォルダから見つからない場合は、シーン内のオブジェクトから検索
        Debug.LogWarning("AutoSetup: Resourcesフォルダからアニメーションコントローラーが見つかりませんでした。");
        Debug.LogWarning("AutoSetup: 手動でアニメーションコントローラーを設定してください。");

        return null;
    }

    private Transform FindChildRecursive(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                return child;
            }

            Transform result = FindChildRecursive(child, childName);
            if (result != null)
            {
                return result;
            }
        }
        return null;
    }
}


